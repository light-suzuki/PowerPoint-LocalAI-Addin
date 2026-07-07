using Newtonsoft.Json;

namespace LocalPptSkillAgent.Models
{
    public class SlideGenerationPlan
    {
        [JsonProperty("insert_after")] public int? InsertAfter { get; set; }
        [JsonProperty("insert_before")] public int? InsertBefore { get; set; }
        [JsonProperty("layout")] public string Layout { get; set; }
        [JsonProperty("slide_role")] public string SlideRole { get; set; }
        [JsonProperty("composition")] public string Composition { get; set; }
        [JsonProperty("visual_type")] public string VisualType { get; set; }
        [JsonProperty("visual_brief")] public string VisualBrief { get; set; }
        [JsonProperty("title")] public string Title { get; set; }
        [JsonProperty("message")] public string Message { get; set; }
        [JsonProperty("body")] public string Body { get; set; }
        [JsonProperty("notes")] public string Notes { get; set; }
        [JsonProperty("citation_note")] public string CitationNote { get; set; }
        [JsonProperty("diagram")] public DiagramPlan Diagram { get; set; }
    }
}
