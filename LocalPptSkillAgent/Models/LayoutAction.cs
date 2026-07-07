using System.Collections.Generic;
using Newtonsoft.Json;

namespace LocalPptSkillAgent.Models
{
    public class LayoutAction
    {
        [JsonProperty("type")] public string Type { get; set; }
        [JsonProperty("slide")] public int Slide { get; set; }
        [JsonProperty("shape_id")] public int? ShapeId { get; set; }
        [JsonProperty("shape_ids")] public List<int> ShapeIds { get; set; } = new List<int>();
        [JsonProperty("dx")] public float? Dx { get; set; }
        [JsonProperty("dy")] public float? Dy { get; set; }
        [JsonProperty("left")] public float? Left { get; set; }
        [JsonProperty("top")] public float? Top { get; set; }
        [JsonProperty("width")] public float? Width { get; set; }
        [JsonProperty("height")] public float? Height { get; set; }
        [JsonProperty("font_size")] public float? FontSize { get; set; }
        [JsonProperty("text")] public string Text { get; set; }
        [JsonProperty("align_mode")] public string AlignMode { get; set; }
        [JsonProperty("distribute_mode")] public string DistributeMode { get; set; }
        [JsonProperty("diagram")] public DiagramPlan Diagram { get; set; }
        [JsonProperty("slide_plan")] public SlideGenerationPlan SlidePlan { get; set; }
        [JsonProperty("reason")] public string Reason { get; set; }
    }
}
