namespace LocalPptSkillAgent.Models
{
    public class TargetSelector
    {
        public string Raw { get; set; }
        public string Type { get; set; }
        public int? StartSlide { get; set; }
        public int? EndSlide { get; set; }
        public int? SlideIndex { get; set; }
        public int? InsertAfter { get; set; }
        public int? InsertBefore { get; set; }
        public bool IncludeSelectedShapes { get; set; }
        public bool IncludeText { get; set; }
        public bool IncludeFigures { get; set; }
        public bool IncludeNotes { get; set; }
    }
}
