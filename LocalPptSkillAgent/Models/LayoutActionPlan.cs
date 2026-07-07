using System.Collections.Generic;

namespace LocalPptSkillAgent.Models
{
    public class LayoutActionPlan
    {
        public string Skill { get; set; }
        public string Target { get; set; }
        public string Summary { get; set; }
        public List<LayoutIssue> Issues { get; set; } = new List<LayoutIssue>();
        public List<LayoutAction> Actions { get; set; } = new List<LayoutAction>();
        public ContextAssemblyReport ContextReport { get; set; }
    }
}
