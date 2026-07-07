using Newtonsoft.Json;

namespace LocalPptSkillAgent.Models
{
    public class DiagramObject
    {
        [JsonProperty("id")] public string Id { get; set; }
        [JsonProperty("type")] public string Type { get; set; }
        [JsonProperty("text")] public string Text { get; set; }
        [JsonProperty("x")] public float X { get; set; }
        [JsonProperty("y")] public float Y { get; set; }
        [JsonProperty("w")] public float W { get; set; }
        [JsonProperty("h")] public float H { get; set; }
        [JsonProperty("from")] public string From { get; set; }
        [JsonProperty("to")] public string To { get; set; }
        [JsonProperty("style_role")] public string StyleRole { get; set; }
    }
}
