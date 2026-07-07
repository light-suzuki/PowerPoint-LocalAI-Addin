namespace LocalPptSkillAgent.Models
{
    public class ProofreadSuggestion
    {
        public int Slide { get; set; }
        public int? ShapeId { get; set; }
        public string OriginalText { get; set; }
        public string SuggestedText { get; set; }
        public string Reason { get; set; }
        public string Severity { get; set; }
    }
}
