using System.Collections.Generic;
using System.Text.RegularExpressions;
using LocalPptSkillAgent.Models;

namespace LocalPptSkillAgent.Services
{
    public class SlashCommand
    {
        public string Skill { get; set; }
        public string Instruction { get; set; }
    }

    /// <summary>「/skill名 指示文」形式のコマンド。意図判定を飛ばして指定スキルを直接実行する。</summary>
    public static class SlashCommandService
    {
        public static SlashCommand TryParse(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var trimmed = text.TrimStart();
            if (!trimmed.StartsWith("/")) return null;
            trimmed = trimmed.Substring(1);
            var space = trimmed.IndexOf(' ');
            var name = (space < 0 ? trimmed : trimmed.Substring(0, space)).Trim().ToLowerInvariant();
            var rest = space < 0 ? string.Empty : trimmed.Substring(space + 1).Trim();
            if (!Regex.IsMatch(name, "^[a-z0-9_]+$")) return null;
            return new SlashCommand { Skill = name, Instruction = rest };
        }

        public static PlannerResult ToPlan(SlashCommand command, bool needsVision, string targetRaw)
        {
            return new PlannerResult
            {
                Intent = command.Skill,
                Skills = new List<string> { command.Skill },
                Target = targetRaw,
                Instruction = string.IsNullOrWhiteSpace(command.Instruction) ? command.Skill : command.Instruction,
                RequiresPreview = true,
                RequiresUserConfirmation = true,
                NeedsVision = needsVision,
                NeedsMcp = command.Skill == "fact_check",
                Explanation = $"コマンド /{command.Skill} を実行します。"
            };
        }
    }
}
