using System.Collections.Generic;

namespace LocalPptSkillAgent.Models
{
    public class DeckStyleProfile
    {
        public float SlideWidth { get; set; }
        public float SlideHeight { get; set; }
        public string DominantFont { get; set; }
        public float CommonTitleFontSize { get; set; }
        public float CommonBodyFontSize { get; set; }
        public List<string> CommonColors { get; set; } = new List<string>();
        public float TypicalLeftMargin { get; set; }
        public float TypicalRightMargin { get; set; }
        public float TypicalTopMargin { get; set; }
        public float TypicalBottomMargin { get; set; }
        public string LayoutSummary { get; set; }
    }
}
