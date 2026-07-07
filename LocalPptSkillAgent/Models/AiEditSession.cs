using System;
using System.Collections.Generic;

namespace LocalPptSkillAgent.Models
{
    public class AiEditSession
    {
        public string SessionId { get; set; }
        public DateTime CreatedAt { get; set; }
        public string PresentationPath { get; set; }
        public string DeckId { get; set; }
        public string Skill { get; set; }
        public string Target { get; set; }
        public string UserInstruction { get; set; }
        public LayoutActionPlan ActionPlan { get; set; }
        public List<ShapeSnapshot> BeforeSnapshots { get; set; } = new List<ShapeSnapshot>();
        public List<ShapeSnapshot> AfterSnapshots { get; set; } = new List<ShapeSnapshot>();
        public List<int> CreatedSlideIds { get; set; } = new List<int>();
        public string BeforeNotesJson { get; set; }
        public string AfterNotesJson { get; set; }
        public bool RolledBack { get; set; }
        public string Status { get; set; } = "completed";
        public string FailureMessage { get; set; }
    }
}
