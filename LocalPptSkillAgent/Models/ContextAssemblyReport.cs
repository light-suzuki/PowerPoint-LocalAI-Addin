using System.Collections.Generic;

namespace LocalPptSkillAgent.Models
{
    public class ContextAssemblyReport
    {
        public int EstimatedInputTokens { get; set; }
        public int? ActualInputTokens { get; set; }
        public int? ActualOutputTokens { get; set; }
        public int InputBudgetTokens { get; set; }
        public int ReservedOutputTokens { get; set; }
        public int SafetyMarginTokens { get; set; }
        public int IncludedSlides { get; set; }
        public int IncludedMessages { get; set; }
        public List<string> OmittedOrTruncated { get; set; } = new List<string>();
        public string SkillName { get; set; }
    }
}
