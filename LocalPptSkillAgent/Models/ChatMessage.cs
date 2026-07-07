using System;

namespace LocalPptSkillAgent.Models
{
    public class ChatMessage
    {
        public string Role { get; set; }
        public string Content { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string Kind { get; set; } = "message";
        public string MetadataJson { get; set; }
    }
}
