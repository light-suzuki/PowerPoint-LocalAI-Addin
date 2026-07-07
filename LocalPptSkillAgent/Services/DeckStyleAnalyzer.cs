using System.Collections.Generic;
using System.Linq;
using LocalPptSkillAgent.Models;

namespace LocalPptSkillAgent.Services
{
    public class DeckStyleAnalyzer
    {
        public DeckStyleProfile Analyze(IReadOnlyList<SlideModel> slides)
        {
            if (slides == null || slides.Count == 0) return new DeckStyleProfile();
            var textShapes = slides.SelectMany(x => x.Shapes).Where(x => x.HasText).ToList();
            var fonts = textShapes.Where(x => !string.IsNullOrWhiteSpace(x.FontName)).GroupBy(x => x.FontName).OrderByDescending(x => x.Count()).FirstOrDefault();
            var sizes = textShapes.Where(x => x.FontSize.HasValue && x.FontSize > 0).Select(x => x.FontSize.Value).OrderBy(x => x).ToList();
            return new DeckStyleProfile
            {
                SlideWidth = slides[0].Width, SlideHeight = slides[0].Height,
                DominantFont = fonts?.Key, CommonBodyFontSize = sizes.Count == 0 ? 18 : sizes[sizes.Count / 2],
                CommonTitleFontSize = sizes.Count == 0 ? 28 : sizes[System.Math.Min(sizes.Count - 1, (int)(sizes.Count * .85))],
                TypicalLeftMargin = textShapes.Count == 0 ? 36 : textShapes.Select(x => x.Left).OrderBy(x => x).ElementAt(textShapes.Count / 2),
                LayoutSummary = $"{slides.Count}枚から抽出。テキスト図形{textShapes.Count}個。"
            };
        }
    }
}
