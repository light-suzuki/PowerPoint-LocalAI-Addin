using System.Threading.Tasks;
using LocalPptSkillAgent.Models;
using LocalPptSkillAgent.Utils;

namespace LocalPptSkillAgent.Services
{
    public class ChatSummarizer
    {
        private readonly LmStudioClient _client;
        public ChatSummarizer(LmStudioClient client) { _client = client; }
        public async Task<ChatSummary> SummarizeAsync(ChatSession session, AppSettings settings)
        {
            var prompt = "会話を次の項目で簡潔に要約し、ChatSummary JSONだけを返す: deck_purpose, editing_policy, main_edits, pending_tasks, cautions, model, skill_settings, raw_summary。";
            var response = await _client.ChatAsync(prompt, JsonUtils.Serialize(session)).ConfigureAwait(false);
            var summary = JsonUtils.Deserialize<ChatSummary>(response) ?? new ChatSummary { RawSummary = response };
            summary.Model = settings.TextModelName;
            if (string.IsNullOrWhiteSpace(summary.RawSummary)) summary.RawSummary = response;
            return summary;
        }
    }
}
