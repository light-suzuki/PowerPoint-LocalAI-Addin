namespace LocalPptSkillAgent.Skills
{
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using LocalPptSkillAgent.Models;
    using LocalPptSkillAgent.Services;

    public class ResearchSlideWriterSkill : LlmSkillBase
    {
        public override string Name => "research_slide_writer";
        protected override bool UsesVision => false;

        public override async Task<LayoutActionPlan> RunAsync(SkillInput input, string prompt, LmStudioClient llm, IVisionClient vision, CancellationToken cancellationToken = default(CancellationToken))
        {
            return await new SlideContentPlanningService().CreateAsync(input, prompt, llm, cancellationToken).ConfigureAwait(false);
        }

        public static int? RequestedSlideCount(string instruction)
        {
            var match = Regex.Match(instruction ?? string.Empty, @"(?<!\d)(\d{1,2})\s*枚");
            if (!match.Success || !int.TryParse(match.Groups[1].Value, out var count) || count < 1 || count > 20) return null;
            return count;
        }
    }
}
