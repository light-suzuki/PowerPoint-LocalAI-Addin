using System.Collections.Generic;
using Newtonsoft.Json;

namespace LocalPptSkillAgent.Models
{
    public class DiagramPlan
    {
        [JsonProperty("diagram_type")] public string DiagramType { get; set; }
        [JsonProperty("slide")] public int Slide { get; set; }
        [JsonProperty("placement")] public string Placement { get; set; }
        [JsonProperty("objects")] public List<DiagramObject> Objects { get; set; } = new List<DiagramObject>();
    }
}
