using System.Collections.Generic;
using Newtonsoft.Json;

namespace LocalPptSkillAgent.Models
{
    public class PlannerResult
    {
        [JsonProperty("target")] public string Target { get; set; } = "@current";
        [JsonProperty("intent")] public string Intent { get; set; } = "unknown";
        [JsonProperty("skills")] public List<string> Skills { get; set; } = new List<string>();
        [JsonProperty("needs_vision")] public bool NeedsVision { get; set; }
        [JsonProperty("needs_mcp")] public bool NeedsMcp { get; set; }
        [JsonProperty("requires_preview")] public bool RequiresPreview { get; set; } = true;
        [JsonProperty("requires_user_confirmation")] public bool RequiresUserConfirmation { get; set; } = true;
        [JsonProperty("instruction")] public string Instruction { get; set; }
        [JsonProperty("explanation")] public string Explanation { get; set; }
    }
}
