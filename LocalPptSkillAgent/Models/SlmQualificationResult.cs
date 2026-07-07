using System;
using System.Collections.Generic;

namespace LocalPptSkillAgent.Models
{
    public class SlmQualificationResult
    {
        public DateTime CheckedAt { get; set; } = DateTime.Now;
        public string Model { get; set; }
        public bool Passed { get; set; }
        public List<string> Checks { get; set; } = new List<string>();
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
        public override string ToString() => $"SLM適合テスト: {(Passed ? "PASS" : "FAIL")}\r\nModel: {Model}\r\n" + string.Join("\r\n", Checks);
    }
}
