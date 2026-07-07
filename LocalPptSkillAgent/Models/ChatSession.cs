using System;
using System.Collections.Generic;

namespace LocalPptSkillAgent.Models
{
    public class ChatSession
    {
        public string ChatSessionId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastUpdatedAt { get; set; }
        public string PresentationPath { get; set; }
        public string DeckContext { get; set; }
        public List<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
        public ChatSummary Summary { get; set; }
    }
}
