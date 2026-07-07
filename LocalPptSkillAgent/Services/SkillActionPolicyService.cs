using System;
using System.Collections.Generic;

namespace LocalPptSkillAgent.Services
{
    public static class SkillActionPolicyService
    {
        private static readonly Dictionary<string, string[]> Preferred = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["visual_check"] = Array.Empty<string>(),
            ["visual_polish"] = new[] { "move", "resize", "align", "distribute", "set_font_size", "set_text", "create_shape", "create_textbox", "create_line", "create_arrow", "tag_shape" },
            ["align_shapes"] = new[] { "move", "align", "distribute" },
            ["diagram_create"] = new[] { "create_diagram" },
            ["native_shape_diagram"] = new[] { "create_diagram" },
            ["slide_generate"] = new[] { "create_slide" },
            ["research_slide_writer"] = new[] { "create_slide" },
            ["proofread"] = new[] { "set_text" },
            ["fact_check"] = new[] { "add_citation_note", "set_text" },
            ["timing_5min"] = new[] { "create_slide", "reorder_slide", "set_text", "add_notes" },
            ["style_match"] = new[] { "move", "resize", "set_font_size", "create_shape", "create_textbox", "create_diagram", "create_slide" },
            ["translate"] = new[] { "set_text", "add_notes" },
            ["deck_summary"] = Array.Empty<string>(),
            ["deck_qa"] = Array.Empty<string>()
        };

        public static IReadOnlyList<string> GetPreferredActions(string skill) =>
            Preferred.TryGetValue(skill ?? string.Empty, out var actions) ? actions : Array.Empty<string>();
    }
}
