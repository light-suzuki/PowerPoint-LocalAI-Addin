using System;
using System.Collections.Generic;
using System.Linq;
using LocalPptSkillAgent.Models;
using LocalPptSkillAgent.Skills;
using LocalPptSkillAgent.Utils;
using Newtonsoft.Json.Linq;

namespace LocalPptSkillAgent.Services
{
    public class ContextAssembly
    {
        public string UserPayload { get; set; }
        public ContextAssemblyReport Report { get; set; }
    }

    public class ContextAssemblerService
    {
        private readonly ContextBudgetService _tokens = new ContextBudgetService();

        public ContextAssembly Build(SkillInput input, string skillName, string compiledSystemPrompt, LmStudioSettings settings, SkillContextPolicy policy)
        {
            var margin = Math.Max(256, settings.InputSafetyMarginTokens);
            var budget = Math.Max(512, settings.ContextLength - settings.MaxTokens - margin);
            var report = new ContextAssemblyReport
            {
                SkillName = skillName, InputBudgetTokens = budget, ReservedOutputTokens = settings.MaxTokens, SafetyMarginTokens = margin
            };
            var allowedActions = SkillActionPolicyService.GetPreferredActions(skillName);
            var taskContract = new JObject
            {
                ["domain"] = "powerpoint_slide_editing_only",
                ["skill"] = skillName,
                ["target"] = input.Target?.Raw ?? "@current",
                ["preferred_actions"] = JArray.FromObject(allowedActions),
                ["rules"] = new JArray("Use supplied slide/deck facts as the evidence boundary", "Do not invent citations or factual results", "Do not delete existing slides or shapes", "Use professional presentation judgment", "Return JSON only")
            };
            var finalContract = new JObject
            {
                ["skill"] = skillName,
                ["target"] = input.Target?.Raw ?? "@current",
                ["preferred_actions"] = JArray.FromObject(allowedActions),
                ["must_return"] = "valid JSON for this skill; no prose or Markdown"
            };
            var root = new JObject { ["task_contract"] = taskContract, ["target"] = input.Target == null ? null : JObject.FromObject(input.Target) };
            var used = _tokens.EstimateTokens(compiledSystemPrompt) + _tokens.EstimateTokens(root.ToString(Newtonsoft.Json.Formatting.None));
            used += _tokens.EstimateTokens(finalContract.ToString(Newtonsoft.Json.Formatting.None));
            if (used >= budget) throw new InvalidOperationException("Skill promptだけで入力コンテキスト予算を超えています。Skill定義を短くするかContext lengthを増やしてください。");
            AddText(root, "instruction", input.Instruction, budget, ref used, report, Math.Max(128, budget / 8));
            AddText(root, "deck_context", input.Context?.RawText, budget, ref used, report, Math.Max(128, budget / 5));
            AddText(root, "prior_chat_summary", input.ConversationSummary?.RawSummary, budget, ref used, report, Math.Max(96, budget / 10));
            if (policy.IncludeDeckStyle) AddJson(root, "deck_style", input.Style == null ? null : JObject.FromObject(input.Style), budget, ref used, report);
            AddJson(root, "rule_based_layout_facts", input.RuleBasedIssues == null ? null : JArray.FromObject(input.RuleBasedIssues), budget, ref used, report);

            var slideArray = new JArray(); root["slides"] = slideArray;
            var slides = input.Slides ?? new List<SlideModel>();
            for (var index = 0; index < slides.Count; index++)
            {
                var remainingSlides = Math.Max(1, slides.Count - index);
                var remaining = Math.Max(64, budget - used);
                var perSlide = Math.Max(64, remaining / remainingSlides);
                var compact = CompactSlide(slides[index], policy, perSlide);
                var serialized = compact.ToString(Newtonsoft.Json.Formatting.None);
                var cost = _tokens.EstimateTokens(serialized);
                if (used + cost > budget)
                {
                    compact = new JObject { ["slide_index"] = slides[index].SlideIndex, ["title"] = Truncate(slides[index].Title, 160) };
                    cost = _tokens.EstimateTokens(compact.ToString(Newtonsoft.Json.Formatting.None));
                    report.OmittedOrTruncated.Add("slide_" + slides[index].SlideIndex + "_details");
                }
                if (used + cost <= budget) { slideArray.Add(compact); used += cost; report.IncludedSlides++; }
                else report.OmittedOrTruncated.Add("slide_" + slides[index].SlideIndex);
            }

            var messages = new JArray(); root["recent_conversation"] = messages;
            var recentLimit = policy.IncludeRecentChat ? Math.Min(Math.Max(0, settings.RecentChatMessages), policy.RecentChatMessages) : 0;
            foreach (var message in (input.RecentMessages ?? new List<ChatMessage>()).Reverse().Take(recentLimit).Reverse())
            {
                var item = new JObject { ["role"] = message.Role, ["content"] = Truncate(message.Content, 2000) };
                var cost = _tokens.EstimateTokens(item.ToString(Newtonsoft.Json.Formatting.None));
                if (used + cost > budget) { report.OmittedOrTruncated.Add("older_chat_messages"); break; }
                messages.Add(item); used += cost; report.IncludedMessages++;
            }
            root["final_response_contract"] = finalContract;
            report.EstimatedInputTokens = Math.Min(budget, used);
            return new ContextAssembly { UserPayload = root.ToString(Newtonsoft.Json.Formatting.None), Report = report };
        }

        private void AddText(JObject root, string name, string value, int budget, ref int used, ContextAssemblyReport report, int componentLimit)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            var available = Math.Min(Math.Max(0, budget - used), Math.Max(0, componentLimit));
            var text = value; var cost = _tokens.EstimateTokens(text);
            if (cost > available)
            {
                text = TruncateToTokenBudget(value, available); cost = _tokens.EstimateTokens(text);
                report.OmittedOrTruncated.Add(name);
            }
            if (cost > 0) { root[name] = text; used += cost; }
        }

        private void AddJson(JObject root, string name, JToken value, int budget, ref int used, ContextAssemblyReport report)
        {
            if (value == null) return;
            var cost = _tokens.EstimateTokens(value.ToString(Newtonsoft.Json.Formatting.None));
            if (used + cost <= budget) { root[name] = value; used += cost; }
            else report.OmittedOrTruncated.Add(name);
        }

        private JObject CompactSlide(SlideModel slide, SkillContextPolicy policy, int tokenBudget)
        {
            var shapes = new JArray(); var shapeCount = Math.Max(1, slide.Shapes?.Count ?? 0);
            var textChars = Math.Max(80, Math.Min(1200, tokenBudget * 3 / shapeCount));
            foreach (var shape in slide.Shapes ?? new List<ShapeModel>())
            {
                var item = new JObject { ["id"] = shape.Id, ["name"] = shape.Name, ["type"] = shape.Type };
                if (shape.HasText) item["text"] = Truncate(shape.Text, textChars);
                if (shape.FontSize.HasValue) item["font_size"] = shape.FontSize;
                if (policy.IncludeGeometry) { item["left"] = shape.Left; item["top"] = shape.Top; item["width"] = shape.Width; item["height"] = shape.Height; item["z"] = shape.ZOrderPosition; }
                shapes.Add(item);
            }
            var result = new JObject { ["slide_index"] = slide.SlideIndex, ["title"] = slide.Title, ["shapes"] = shapes };
            if (policy.IncludeNotes) result["notes"] = Truncate(slide.NotesText, Math.Max(200, tokenBudget * 2));
            return result;
        }

        private string TruncateToTokenBudget(string value, int tokens)
        {
            if (tokens <= 0) return string.Empty;
            var low = 0; var high = value.Length;
            while (low < high) { var mid = (low + high + 1) / 2; if (_tokens.EstimateTokens(value.Substring(0, mid)) <= tokens) low = mid; else high = mid - 1; }
            return Truncate(value, low);
        }
        private static string Truncate(string value, int maxChars) => string.IsNullOrEmpty(value) || value.Length <= maxChars ? value : value.Substring(0, Math.Max(0, maxChars)) + "…";
    }
}
