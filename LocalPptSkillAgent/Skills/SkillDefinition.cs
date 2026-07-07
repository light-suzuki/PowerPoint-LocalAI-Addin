using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using LocalPptSkillAgent.Models;
using LocalPptSkillAgent.Services;

namespace LocalPptSkillAgent.Skills
{
    public class SkillInput
    {
        public DeckContext Context { get; set; }
        public TargetSelector Target { get; set; }
        public string Instruction { get; set; }
        public IReadOnlyList<SlideModel> Slides { get; set; }
        public DeckStyleProfile Style { get; set; }
        public IReadOnlyList<ChatMessage> RecentMessages { get; set; }
        public ChatSummary ConversationSummary { get; set; }
        public IReadOnlyList<LayoutIssue> RuleBasedIssues { get; set; }
    }

    public interface ISkill
    {
        string Name { get; }
        Task<LayoutActionPlan> RunAsync(SkillInput input, string prompt, LmStudioClient llm, IVisionClient vision, CancellationToken cancellationToken = default(CancellationToken));
    }

    public abstract class LlmSkillBase : ISkill
    {
        public abstract string Name { get; }
        protected virtual bool UsesVision => false;
        public virtual async Task<LayoutActionPlan> RunAsync(SkillInput input, string prompt, LmStudioClient llm, IVisionClient vision, CancellationToken cancellationToken = default(CancellationToken))
        {
            var system = prompt;
            var merged = new LayoutActionPlan { Skill = Name, Target = input.Target.Raw };
            var assembler = new ContextAssemblerService(); var structured = new StructuredOutputService();
            var outputSchema = StructuredSchemas.ForSkill(Name);
            var policy = SkillPromptService.ParseContextPolicy(system);
            var visionSlides = UsesVision && policy.IncludeSlideImages ? (input.Slides ?? new List<SlideModel>()).Where(x => !string.IsNullOrWhiteSpace(x.ExportedImagePath)).ToList() : new List<SlideModel>();
            if (visionSlides.Count > 0)
            {
                foreach (var slide in visionSlides)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var localInput = CopyForSlide(input, slide);
                    var assembly = assembler.Build(localInput, Name, system, llm.Settings, policy);
                    Func<CancellationToken, Task<string>> request;
                    if (vision is LmStudioClient client)
                        request = token => llm.Settings.UseStructuredOutput
                            ? client.AnalyzeSlideStructuredAsync(system, assembly.UserPayload, slide.ExportedImagePath, string.Empty, outputSchema, Name + "_action_plan", token)
                            : client.AnalyzeSlideAsync(system, assembly.UserPayload, slide.ExportedImagePath, string.Empty, token);
                    else request = token => vision.AnalyzeSlideAsync(system, assembly.UserPayload, slide.ExportedImagePath, string.Empty);
                    var response = await request(cancellationToken).ConfigureAwait(false);
                    var parsed = await structured.ParseActionPlanAsync(response, llm, cancellationToken, request, outputSchema, Name + "_action_plan").ConfigureAwait(false); parsed.ContextReport = assembly.Report;
                    Normalize(parsed, input);
                    if (llm.LastUsage != null) { parsed.ContextReport.ActualInputTokens = llm.LastUsage.PromptTokens; parsed.ContextReport.ActualOutputTokens = llm.LastUsage.CompletionTokens; }
                    Merge(merged, parsed);
                }
            }
            else
            {
                var assembly = assembler.Build(input, Name, system, llm.Settings, policy);
                Func<CancellationToken, Task<string>> request = token => llm.Settings.UseStructuredOutput
                    ? llm.ChatStructuredAsync(system, assembly.UserPayload, outputSchema, Name + "_action_plan", token)
                    : llm.ChatAsync(system, assembly.UserPayload, token);
                var response = await request(cancellationToken).ConfigureAwait(false);
                var parsed = await structured.ParseActionPlanAsync(response, llm, cancellationToken, request, outputSchema, Name + "_action_plan").ConfigureAwait(false); parsed.ContextReport = assembly.Report;
                Normalize(parsed, input);
                if (llm.LastUsage != null) { parsed.ContextReport.ActualInputTokens = llm.LastUsage.PromptTokens; parsed.ContextReport.ActualOutputTokens = llm.LastUsage.CompletionTokens; }
                Merge(merged, parsed);
            }
            if (Name == "visual_check" && input.RuleBasedIssues != null)
                merged.Issues.InsertRange(0, input.RuleBasedIssues.Where(rule => !merged.Issues.Any(existing => SameIssue(existing, rule))));
            return merged;
        }

        private void Normalize(LayoutActionPlan plan, SkillInput input)
        {
            plan.Skill = Name;
            plan.Target = input.Target?.Raw ?? "@current";
            plan.Actions = plan.Actions ?? new List<LayoutAction>();
        }

        private static SkillInput CopyForSlide(SkillInput input, SlideModel slide) => new SkillInput
        {
            Context = input.Context, Target = input.Target, Instruction = input.Instruction, Style = input.Style,
            Slides = new[] { slide }, RecentMessages = input.RecentMessages, ConversationSummary = input.ConversationSummary
            , RuleBasedIssues = input.RuleBasedIssues?.Where(x => x.Slide == slide.SlideIndex).ToList()
        };

        private static bool SameIssue(LayoutIssue a, LayoutIssue b) => a.Slide == b.Slide && string.Equals(a.IssueType, b.IssueType) &&
            (a.ShapeIds ?? new List<int>()).OrderBy(x => x).SequenceEqual((b.ShapeIds ?? new List<int>()).OrderBy(x => x));

        private static void Merge(LayoutActionPlan destination, LayoutActionPlan source)
        {
            if (source == null) return;
            if (source.Issues != null) destination.Issues.AddRange(source.Issues);
            if (source.Actions != null) destination.Actions.AddRange(source.Actions);
            if (!string.IsNullOrWhiteSpace(source.Summary)) destination.Summary = string.IsNullOrWhiteSpace(destination.Summary) ? source.Summary : destination.Summary + "\r\n" + source.Summary;
            if (source.ContextReport != null) destination.ContextReport = source.ContextReport;
        }
    }
}
