namespace LocalPptSkillAgent.Models
{
    public class ContextBudget
    {
        public int EstimatedUsedTokens { get; set; }
        public int MaxContextTokens { get; set; }
        public int EstimatedRemainingTokens { get; set; }
        public double UsageRatio { get; set; }
        public string Status { get; set; }
        public string WarningMessage { get; set; }
    }
}
