using System.Collections.Generic;
using Newtonsoft.Json;

namespace LocalPptSkillAgent.Models
{
    public class SlideContentPlan
    {
        [JsonProperty("summary")] public string Summary { get; set; }
        [JsonProperty("slides")] public List<SlideContentItem> Slides { get; set; } = new List<SlideContentItem>();
    }

    public class SlideContentItem
    {
        [JsonProperty("slide_role")] public string SlideRole { get; set; }
        [JsonProperty("layout")] public string Layout { get; set; }
        [JsonProperty("composition")] public string Composition { get; set; }
        [JsonProperty("visual_type")] public string VisualType { get; set; }
        [JsonProperty("visual_brief")] public string VisualBrief { get; set; }
        [JsonProperty("title")] public string Title { get; set; }
        [JsonProperty("message")] public string Message { get; set; }
        [JsonProperty("body")] public List<string> Body { get; set; } = new List<string>();
        [JsonProperty("notes")] public string Notes { get; set; }
        [JsonProperty("citation_note")] public string CitationNote { get; set; }
    }
}
