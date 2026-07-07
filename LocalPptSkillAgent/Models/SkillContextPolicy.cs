namespace LocalPptSkillAgent.Models
{
    public class SkillContextPolicy
    {
        public bool IncludeGeometry { get; set; }
        public bool IncludeNotes { get; set; }
        public bool IncludeDeckStyle { get; set; } = true;
        public bool IncludeRecentChat { get; set; } = true;
        public bool IncludeSlideImages { get; set; }
        public int RecentChatMessages { get; set; } = 8;
    }
}
