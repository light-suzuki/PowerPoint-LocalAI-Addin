using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using LocalPptSkillAgent.Models;
using LocalPptSkillAgent.Skills;

namespace LocalPptSkillAgent.Services
{
    public class SkillRouter
    {
        private readonly Dictionary<string, ISkill> _skills;
        private readonly LmStudioClient _llm;
        private readonly IVisionClient _vision;
        private readonly string _promptDirectory;
        private readonly SkillPromptService _prompts;

        public SkillRouter(LmStudioClient llm, IVisionClient vision, string promptDirectory = null)
        {
            _llm = llm; _vision = vision;
            _promptDirectory = promptDirectory ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "skills");
            _prompts = new SkillPromptService(_promptDirectory);
            _skills = new Dictionary<string, ISkill>(StringComparer.OrdinalIgnoreCase)
            {
                ["visual_check"] = new VisualCheckSkill(), ["visual_polish"] = new VisualPolishSkill(),
                ["align_shapes"] = new AlignShapesSkill(), ["diagram_create"] = new DiagramCreateSkill(),
                ["slide_generate"] = new SlideGenerateSkill(), ["proofread"] = new ProofreadSkill(),
                ["fact_check"] = new FactCheckSkill(), ["timing_5min"] = new TimingFiveMinSkill(),
                ["style_match"] = new StyleMatchSkill(), ["research_slide_writer"] = new ResearchSlideWriterSkill(),
                ["native_shape_diagram"] = new NativeShapeDiagramSkill(), ["translate"] = new TranslateSkill(),
                ["deck_summary"] = new DeckSummarySkill(), ["deck_qa"] = new DeckQaSkill()
            };
            ReloadCustomSkills();
        }

        public string PromptDirectory => _promptDirectory;
        public IReadOnlyCollection<string> SkillNames => _skills.Keys;
        public bool Has(string name) => _skills.ContainsKey(name ?? string.Empty);

        /// <summary>skillsフォルダを再走査し、C#実装のない*.mdを汎用スキルとして登録する。</summary>
        public void ReloadCustomSkills()
        {
            if (!Directory.Exists(_promptDirectory)) return;
            foreach (var path in Directory.GetFiles(_promptDirectory, "*.md"))
            {
                var name = Path.GetFileNameWithoutExtension(path);
                if (string.IsNullOrWhiteSpace(name) || _skills.ContainsKey(name)) continue;
                if (!System.Text.RegularExpressions.Regex.IsMatch(name, "^[a-z0-9_]+$")) continue;
                _skills[name] = new CustomMarkdownSkill(name);
            }
        }

        /// <summary>スキルのContext Policyを返す。読めない場合は既定値。</summary>
        public Models.SkillContextPolicy GetContextPolicy(string name)
        {
            try { return SkillPromptService.ParseContextPolicy(_prompts.Load(name)); }
            catch { return new Models.SkillContextPolicy(); }
        }

        public async Task<LayoutActionPlan> RunAsync(string name, SkillInput input, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (!_skills.TryGetValue(name ?? string.Empty, out var skill)) throw new InvalidOperationException("不明なSkillです: " + name);
            return await skill.RunAsync(input, _prompts.Load(name), _llm, _vision, cancellationToken).ConfigureAwait(false);
        }

        public async Task<LayoutActionPlan> RunPlannedAsync(PlannerResult planner, SkillInput input, CancellationToken cancellationToken = default(CancellationToken))
        {
            var merged = new LayoutActionPlan { Skill = planner.Intent, Target = planner.Target, Summary = planner.Explanation };
            foreach (var name in planner.Skills)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!_skills.ContainsKey(name)) continue;
                var plan = await RunAsync(name, input, cancellationToken).ConfigureAwait(false);
                if (plan?.Issues != null) merged.Issues.AddRange(plan.Issues);
                if (plan?.Actions != null) merged.Actions.AddRange(plan.Actions);
                if (plan?.ContextReport != null) merged.ContextReport = plan.ContextReport;
                if (!string.IsNullOrWhiteSpace(plan?.Summary)) merged.Summary += "\r\n" + plan.Summary;
            }
            return merged;
        }
    }
}
