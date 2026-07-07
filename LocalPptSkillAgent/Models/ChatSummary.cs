using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace LocalPptSkillAgent.Models
{
    public class ChatSummary
    {
        [JsonProperty("created_at")] public DateTime CreatedAt { get; set; } = DateTime.Now;
        [JsonProperty("deck_purpose")] public string DeckPurpose { get; set; }
        [JsonProperty("editing_policy")] public string EditingPolicy { get; set; }
        [JsonProperty("main_edits")] public List<string> MainEdits { get; set; } = new List<string>();
        [JsonProperty("pending_tasks")] public List<string> PendingTasks { get; set; } = new List<string>();
        [JsonProperty("cautions")] public List<string> Cautions { get; set; } = new List<string>();
        [JsonProperty("model")] public string Model { get; set; }
        [JsonProperty("skill_settings")] public string SkillSettings { get; set; }
        [JsonProperty("raw_summary")] public string RawSummary { get; set; }
    }
}
