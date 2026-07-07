namespace LocalPptSkillAgent.Models
{
    public class DeckContext
    {
        public string Purpose { get; set; }
        public string Audience { get; set; }
        public string TimeLimit { get; set; }
        public string StyleInstruction { get; set; }
        public string FactCheckPolicy { get; set; }
        public string RawText { get; set; }
    }
}
