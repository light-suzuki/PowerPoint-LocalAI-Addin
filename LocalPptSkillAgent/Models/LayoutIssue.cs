using System.Collections.Generic;
using Newtonsoft.Json;

namespace LocalPptSkillAgent.Models
{
    public class LayoutIssue
    {
        [JsonProperty("issue_type")] public string IssueType { get; set; }
        [JsonProperty("severity")] public string Severity { get; set; }
        [JsonProperty("message")] public string Message { get; set; }
        [JsonProperty("slide")] public int Slide { get; set; }
        [JsonProperty("shape_ids")] public List<int> ShapeIds { get; set; } = new List<int>();
    }
}
