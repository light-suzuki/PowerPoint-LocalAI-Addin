using System.Collections.Generic;
using Newtonsoft.Json;

namespace LocalPptSkillAgent.Models
{
    public class FactCheckClaim
    {
        [JsonProperty("slide")] public int Slide { get; set; }
        [JsonProperty("shape_id")] public int? ShapeId { get; set; }
        [JsonProperty("claim")] public string Claim { get; set; }
        [JsonProperty("status")] public string Status { get; set; }
        [JsonProperty("reason")] public string Reason { get; set; }
        [JsonProperty("evidence")] public List<string> Evidence { get; set; } = new List<string>();
        [JsonProperty("suggested_slide_text")] public string SuggestedSlideText { get; set; }
    }
}
