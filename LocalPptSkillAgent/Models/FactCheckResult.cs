using System.Collections.Generic;
using Newtonsoft.Json;

namespace LocalPptSkillAgent.Models
{
    public class FactCheckResult
    {
        [JsonProperty("target")] public string Target { get; set; }
        [JsonProperty("summary")] public string Summary { get; set; }
        [JsonProperty("external_evidence_used")] public bool ExternalEvidenceUsed { get; set; }
        [JsonProperty("claims")] public List<FactCheckClaim> Claims { get; set; } = new List<FactCheckClaim>();
        public ContextAssemblyReport ContextReport { get; set; }
    }
}
