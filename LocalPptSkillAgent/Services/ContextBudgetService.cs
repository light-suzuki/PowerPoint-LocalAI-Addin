using System;
using System.Collections.Generic;
using System.Linq;
using LocalPptSkillAgent.Models;

namespace LocalPptSkillAgent.Services
{
    public class ContextBudgetService
    {
        public ContextBudget Calculate(IEnumerable<string> parts, int maxContextTokens)
        {
            var used = (parts ?? Enumerable.Empty<string>()).Sum(EstimateTokens);
            var max = Math.Max(1024, maxContextTokens);
            var ratio = Math.Min(1d, (double)used / max);
            var status = ratio < .6 ? "安全" : ratio < .8 ? "注意" : ratio < .95 ? "危険" : "上限付近";
            return new ContextBudget
            {
                EstimatedUsedTokens = used, MaxContextTokens = max, EstimatedRemainingTokens = Math.Max(0, max - used),
                UsageRatio = ratio, Status = status,
                WarningMessage = ratio < .8 ? string.Empty : "コンテキストが上限に近づいています。\r\n新しいチャットに切り替えるか、過去の会話を要約してください。"
            };
        }

        public int EstimateTokens(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            var japanese = 0; var ascii = 0; var other = 0;
            foreach (var c in text)
            {
                if (IsJapanese(c)) japanese++;
                else if (c <= 127) { if (!char.IsWhiteSpace(c)) ascii++; }
                else other++;
            }
            return (int)Math.Ceiling(japanese * 1.2 + other + ascii / 4d);
        }
        private static bool IsJapanese(char c) => (c >= '\u3040' && c <= '\u30ff') || (c >= '\u3400' && c <= '\u9fff');
    }
}
