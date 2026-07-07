using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using LocalPptSkillAgent.Models;
using LocalPptSkillAgent.Skills;
using LocalPptSkillAgent.Utils;

namespace LocalPptSkillAgent.Services
{
    public class SlideContentPlanningService
    {
        private static readonly HashSet<string> Layouts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "title_only", "title_body", "title_two_column", "title_diagram", "title_diagram_two_column", "section_header", "summary", "method_flow", "result_with_figure", "hypothesis_model" };

        // 小型ローカルモデルはenumを守らず「それっぽい別名」を出す。strict:falseでは弾けないため、
        // C#側で正規enumへ寄せる。マップできない場合だけ最も無難なtitle_bodyへ倒す。
        private static string MapLayout(string value)
        {
            var key = Canonical(value);
            if (Layouts.Contains(value ?? string.Empty)) return value;
            if (key.Length == 0) return "title_body";
            if (Contains(key, "twocolumn", "2column", "sidebyside", "leftright")) return "title_two_column";
            if (Contains(key, "diagramtwocolumn", "figuretwocolumn")) return "title_diagram_two_column";
            if (Contains(key, "process", "flow", "pipeline", "steps", "workflow", "methodflow")) return "method_flow";
            if (Contains(key, "result", "figure", "data", "chart", "graph", "evidence")) return "result_with_figure";
            if (Contains(key, "hypothesis", "model", "concept", "mechanism", "pathway")) return "hypothesis_model";
            if (Contains(key, "section", "divider", "agenda", "chapter")) return "section_header";
            if (Contains(key, "summary", "conclusion", "takeaway", "closing", "wrapup", "keymessage")) return "summary";
            if (Contains(key, "diagram", "schematic", "illustration")) return "title_diagram";
            if (Contains(key, "titleonly", "cover", "single", "focus", "statement", "quote")) return "title_only";
            if (Contains(key, "body", "content", "text", "bullet", "standard", "default")) return "title_body";
            return "title_body";
        }

        private static string MapComposition(string value)
        {
            var key = Canonical(value);
            if (key.Length == 0) return null;
            if (Contains(key, "textvisualsplit", "splitview", "figuretext")) return "text_visual_split";
            if (Contains(key, "twocolumn", "2column")) return "two_column";
            if (Contains(key, "threestep", "3step", "process", "flow", "steps")) return "three_step";
            if (Contains(key, "comparison", "versus", "vs", "beforeafter")) return "comparison";
            if (Contains(key, "evidence", "takeaway")) return "evidence_plus_takeaway";
            if (Contains(key, "threecards", "3card", "cards", "grid")) return "three_cards";
            if (Contains(key, "single", "focus", "textheavy", "statement")) return "single_focus";
            return null;
        }

        private static string MapVisual(string value)
        {
            var key = Canonical(value);
            if (key.Length == 0) return null;
            if (Contains(key, "none", "textonly")) return "none";
            if (Contains(key, "diagram", "pathway", "schematic", "concept", "model", "flowchart")) return "native_diagram";
            if (Contains(key, "process", "flow", "steps", "pipeline")) return "process";
            if (Contains(key, "comparison", "versus", "beforeafter")) return "comparison";
            if (Contains(key, "evidence", "figure", "result", "frame")) return "evidence_frame";
            if (Contains(key, "metric", "number", "stat", "kpi")) return "metric";
            if (Contains(key, "takeaway", "keymessage", "card", "summary")) return "key_takeaway";
            return null;
        }

        private static string MapRole(string value)
        {
            var key = Canonical(value);
            if (key.Length == 0) return null;
            if (Contains(key, "cover", "title")) return "cover";
            if (Contains(key, "context", "background", "intro")) return "context";
            if (Contains(key, "question", "problem", "aim", "objective")) return "question";
            if (Contains(key, "method", "material", "approach", "experiment")) return "method";
            if (Contains(key, "evidence", "result", "data", "finding")) return "evidence";
            if (Contains(key, "comparison", "versus")) return "comparison";
            if (Contains(key, "interpretation", "discussion", "insight")) return "interpretation";
            if (Contains(key, "model", "hypothesis", "mechanism")) return "model";
            if (Contains(key, "summary", "conclusion", "closing", "wrapup")) return "summary";
            if (Contains(key, "next", "future", "outlook", "step")) return "next_step";
            return null;
        }

        private static string Canonical(string value) => new string((value ?? string.Empty).ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
        private static bool Contains(string key, params string[] needles) => needles.Any(key.Contains);

        public async Task<LayoutActionPlan> CreateAsync(SkillInput input, string prompt, LmStudioClient llm, CancellationToken cancellationToken)
        {
            var requested = ResearchSlideWriterSkill.RequestedSlideCount(input?.Instruction);
            var schema = StructuredSchemas.SlideContentPlan(requested);
            var policy = SkillPromptService.ParseContextPolicy(prompt);
            var system = prompt + "\r\n\r\n# Small-model output stage\r\n"
                + "今はPowerPoint操作を考えず、スライド内容だけをSlideContentPlan JSONで返す。"
                + "各slideで役割、構成、視覚表現を選び、slide_role,layout,composition,visual_type,visual_brief,title,message,body,notes,citation_noteを返す。\r\n"
                + "次の語彙だけを使う(別名を作らない):\r\n"
                + "- layout: title_only / title_body / title_two_column / title_diagram / title_diagram_two_column / section_header / summary / method_flow / result_with_figure / hypothesis_model\r\n"
                + "- composition: single_focus / text_visual_split / two_column / three_step / comparison / evidence_plus_takeaway / three_cards\r\n"
                + "- visual_type: none / native_diagram / process / comparison / evidence_frame / metric / key_takeaway\r\n"
                + "- slide_role: cover / context / question / method / evidence / comparison / interpretation / model / summary / next_step\r\n"
                + "titleは内容名でなく結論または問いにする(例:「Faは分裂組織を拡大する」)。messageは1文の要点。\r\n"
                + "入力にない事実・数値・引用は作らない。本文は原則3〜5項目だが、強い1メッセージのスライドは短くしてよい。"
                + "連続スライドで同じ構図を機械的に反復せず、物語の流れと内容に合わせて変える。";
            if (requested.HasValue) system += $" slides配列は必ず{requested.Value}件。";
            var assembly = new ContextAssemblerService().Build(input, "research_slide_writer", system, llm.Settings, policy);
            Exception last = null;
            for (var attempt = 0; attempt <= Math.Max(0, llm.Settings.StructuredOutputRetryCount); attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var retry = attempt == 0 ? string.Empty : $"\n前回は契約違反: {last?.Message}\n全体を作り直し、指定schemaだけを返す。";
                try
                {
                    string response;
                    try { response = await llm.ChatStructuredAsync(system + retry, assembly.UserPayload, schema, "slide_content_plan", cancellationToken).ConfigureAwait(false); }
                    catch (HttpRequestException ex) when (ex.Message.IndexOf("peg-native", StringComparison.OrdinalIgnoreCase) >= 0 || ex.Message.IndexOf("schema", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        response = await llm.ChatAsync(system + retry + "\r\nStructured grammarが使えないため、同じSlideContentPlanを通常のJSONとして返す。", assembly.UserPayload, cancellationToken).ConfigureAwait(false);
                    }
                    var content = JsonUtils.Deserialize<SlideContentPlan>(response) ?? throw new InvalidOperationException("SlideContentPlanがnullです。");
                    Normalize(content, requested);
                    Validate(content, requested);
                    return Convert(content, input, assembly.Report, llm.LastUsage);
                }
                catch (Exception ex) { last = ex; }
            }
            throw new InvalidOperationException("スライド内容計画を生成できませんでした: " + (last?.Message ?? "不明な応答エラー"), last);
        }

        public static void Normalize(SlideContentPlan plan, int? exactCount)
        {
            if (plan?.Slides == null) return;
            if (exactCount.HasValue && plan.Slides.Count > exactCount.Value)
                plan.Slides = plan.Slides.Take(exactCount.Value).ToList();
            for (var i = 0; i < plan.Slides.Count; i++)
            {
                var slide = plan.Slides[i];
                // enumドリフトを正規enumへ寄せる(意図を保持したままレンダラが正しい構図を選べるようにする)
                slide.Layout = MapLayout(slide.Layout);
                slide.Composition = MapComposition(slide.Composition) ?? slide.Composition;
                slide.VisualType = MapVisual(slide.VisualType) ?? slide.VisualType;
                slide.SlideRole = MapRole(slide.SlideRole) ?? slide.SlideRole;
                if (string.IsNullOrWhiteSpace(slide.Title)) slide.Title = string.IsNullOrWhiteSpace(slide.Message) ? $"スライド {i + 1}" : slide.Message;
                slide.Message = slide.Message ?? string.Empty;
                slide.Body = (slide.Body ?? new List<string>()).Where(x => !string.IsNullOrWhiteSpace(x)).Take(6).ToList();
                if (slide.Body.Count == 0 && !string.IsNullOrWhiteSpace(slide.Message)) slide.Body.Add(slide.Message);
                slide.Notes = slide.Notes ?? string.Empty;
                slide.CitationNote = slide.CitationNote ?? string.Empty;
            }
        }

        public static void Validate(SlideContentPlan plan, int? exactCount)
        {
            if (plan?.Slides == null || plan.Slides.Count == 0) throw new InvalidOperationException("slidesが空です。");
            if (plan.Slides.Count > 12) throw new InvalidOperationException("slidesは12件以下にしてください。");
            if (exactCount.HasValue && plan.Slides.Count != exactCount.Value) throw new InvalidOperationException($"slidesは{exactCount.Value}件必要ですが{plan.Slides.Count}件です。");
            for (var i = 0; i < plan.Slides.Count; i++)
            {
                var slide = plan.Slides[i];
                if (!Layouts.Contains(slide.Layout ?? string.Empty)) throw new InvalidOperationException($"slide {i + 1}: layoutが不正です。");
                if (string.IsNullOrWhiteSpace(slide.Title)) throw new InvalidOperationException($"slide {i + 1}: titleがありません。");
                if (slide.Title.Length > 80) throw new InvalidOperationException($"slide {i + 1}: titleが長すぎます。");
                if (slide.Body == null || slide.Body.Count < 1 || slide.Body.Count > 6) throw new InvalidOperationException($"slide {i + 1}: bodyは1〜6項目です。");
                if (slide.Body.Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 300)) throw new InvalidOperationException($"slide {i + 1}: body項目が空または長すぎます。");
                if (string.IsNullOrWhiteSpace(slide.SlideRole)) slide.SlideRole = i == 0 ? "context" : i == plan.Slides.Count - 1 ? "summary" : "evidence";
                if (string.IsNullOrWhiteSpace(slide.Composition)) slide.Composition = CompositionFor(slide.Layout);
                if (string.IsNullOrWhiteSpace(slide.VisualType)) slide.VisualType = VisualFor(slide.Composition);
                if (slide.VisualBrief == null) slide.VisualBrief = string.Empty;
            }
        }

        private static string CompositionFor(string layout)
        {
            switch ((layout ?? string.Empty).ToLowerInvariant())
            {
                case "title_two_column": case "title_diagram_two_column": case "result_with_figure": return "text_visual_split";
                case "method_flow": return "three_step";
                case "summary": return "three_cards";
                case "title_only": case "section_header": return "single_focus";
                default: return "single_focus";
            }
        }

        private static string VisualFor(string composition)
        {
            switch ((composition ?? string.Empty).ToLowerInvariant())
            {
                case "three_step": return "process";
                case "comparison": case "two_column": return "comparison";
                case "three_cards": return "key_takeaway";
                case "text_visual_split": return "evidence_frame";
                default: return "none";
            }
        }

        private static LayoutActionPlan Convert(SlideContentPlan content, SkillInput input, ContextAssemblyReport report, LmStudioUsage usage)
        {
            var target = input.Target ?? new TargetSelector { Raw = "@current", Type = "current" };
            var anchor = target.InsertAfter ?? target.InsertBefore ?? target.SlideIndex ?? input.Slides?.FirstOrDefault()?.SlideIndex ?? 0;
            var result = new LayoutActionPlan { Skill = "research_slide_writer", Target = target.Raw, Summary = content.Summary, ContextReport = report };
            if (usage != null) { report.ActualInputTokens = usage.PromptTokens; report.ActualOutputTokens = usage.CompletionTokens; }
            foreach (var slide in content.Slides)
            {
                var slidePlan = new SlideGenerationPlan
                {
                    Layout = slide.Layout, SlideRole = slide.SlideRole, Composition = slide.Composition,
                    VisualType = slide.VisualType, VisualBrief = slide.VisualBrief, Title = slide.Title, Message = slide.Message,
                    Body = string.Join("\r\n", slide.Body.Select(x => "• " + x)), Notes = slide.Notes, CitationNote = slide.CitationNote
                };
                if (target.InsertBefore.HasValue) slidePlan.InsertBefore = target.InsertBefore;
                else slidePlan.InsertAfter = target.InsertAfter ?? anchor;
                result.Actions.Add(new LayoutAction { Type = "create_slide", Slide = Math.Max(1, anchor), SlidePlan = slidePlan, Reason = "検証済みSlideContentPlanからC#で変換" });
            }
            return result;
        }
    }
}
