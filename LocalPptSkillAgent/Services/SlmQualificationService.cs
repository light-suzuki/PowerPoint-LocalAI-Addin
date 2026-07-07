using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LocalPptSkillAgent.Models;
using LocalPptSkillAgent.Skills;

namespace LocalPptSkillAgent.Services
{
    public class SlmQualificationService
    {
        private readonly LmStudioClient _client;
        private readonly string _skillsDirectory;
        public SlmQualificationService(LmStudioClient client, string skillsDirectory = null)
        {
            _client = client;
            _skillsDirectory = skillsDirectory ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "skills");
        }

        public async Task<SlmQualificationResult> RunAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            var result = new SlmQualificationResult { Model = _client.Settings.TextModel };
            try
            {
                await _client.TestStructuredOutputAsync(cancellationToken).ConfigureAwait(false);
                result.Checks.Add("PASS JSON Schema応答");

                var input = new SkillInput
                {
                    Context = new DeckContext { RawText = "研究発表。1スライド1メッセージ。入力にない事実は作らない。" },
                    Target = new TargetSelector { Raw = "@current", Type = "current" },
                    Instruction = "光合成の概要を説明する2枚のスライド作って",
                    Slides = new[] { new SlideModel { SlideIndex = 1, Title = "開始" } },
                    Style = new DeckStyleProfile(), RecentMessages = new List<ChatMessage>()
                };
                var router = new SkillRouter(_client, _client, _skillsDirectory);
                var slides = await router.RunAsync("research_slide_writer", input, cancellationToken).ConfigureAwait(false);
                var creates = slides.Actions.Where(x => string.Equals(x.Type, "create_slide", StringComparison.OrdinalIgnoreCase)).ToList();
                if (creates.Count != 2 || slides.Actions.Count != 2 || creates.Any(x => string.IsNullOrWhiteSpace(x.SlidePlan?.Title)))
                    throw new InvalidOperationException("2枚指定に対するcreate_slide契約を満たしませんでした。");
                result.Checks.Add("PASS 指定枚数とcreate_slide限定");

                input.Instruction = "このデッキを要約して";
                input.Target = new TargetSelector { Raw = "@all", Type = "all" };
                input.Slides = new[] { new SlideModel { SlideIndex = 1, Title = "目的", Shapes = new List<ShapeModel> { new ShapeModel { Id = 1, HasText = true, Text = "光条件が成長に与える影響を調べる" } } } };
                var summary = await router.RunAsync("deck_summary", input, cancellationToken).ConfigureAwait(false);
                if (summary.Actions.Count != 0 || string.IsNullOrWhiteSpace(summary.Summary)) throw new InvalidOperationException("要約SkillがActionを生成したか、summaryが空です。");
                result.Checks.Add("PASS 読み取り専用SkillのAction禁止");
                result.Passed = true;
            }
            catch (Exception ex)
            {
                result.Checks.Add("FAIL " + ex.Message);
                result.Passed = false;
            }
            if (_client.LastUsage != null) { result.PromptTokens = _client.LastUsage.PromptTokens; result.CompletionTokens = _client.LastUsage.CompletionTokens; }
            return result;
        }
    }
}
