using System.Collections.Generic;

namespace LocalPptSkillAgent.Models
{
    public class UserFriendlyError
    {
        public string Title { get; set; }
        public string Message { get; set; }
        public List<string> PossibleCauses { get; set; } = new List<string>();
        public List<string> SuggestedActions { get; set; } = new List<string>();
        public string TechnicalDetails { get; set; }

        public override string ToString()
        {
            var causes = PossibleCauses.Count == 0 ? string.Empty : "\r\n\r\n考えられる原因:\r\n・" + string.Join("\r\n・", PossibleCauses);
            var actions = SuggestedActions.Count == 0 ? string.Empty : "\r\n\r\n確認してください:\r\n・" + string.Join("\r\n・", SuggestedActions);
            return Title + "\r\n" + Message + causes + actions;
        }
    }
}
