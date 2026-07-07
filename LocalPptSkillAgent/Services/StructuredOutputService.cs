using System;
using System.Threading;
using System.Threading.Tasks;
using LocalPptSkillAgent.Models;
using LocalPptSkillAgent.Utils;
using Newtonsoft.Json.Linq;

namespace LocalPptSkillAgent.Services
{
    public class StructuredOutputService
    {
        public async Task<LayoutActionPlan> ParseActionPlanAsync(string response, LmStudioClient client, CancellationToken cancellationToken, Func<CancellationToken, Task<string>> regenerate = null, JObject schema = null, string schemaName = null)
        {
            schema = schema ?? StructuredSchemas.LayoutActionPlan;
            schemaName = string.IsNullOrWhiteSpace(schemaName) ? "layout_action_plan_repair" : schemaName + "_repair";
            var current = response;
            Exception last = null;
            for (var attempt = 0; attempt <= Math.Max(0, client.Settings.StructuredOutputRetryCount); attempt++)
            {
                try
                {
                    var plan = JsonUtils.Deserialize<LayoutActionPlan>(current) ?? throw new InvalidOperationException("ActionPlanがnullです。");
                    plan.Issues = plan.Issues ?? new System.Collections.Generic.List<LayoutIssue>();
                    plan.Actions = plan.Actions ?? new System.Collections.Generic.List<LayoutAction>();
                    return plan;
                }
                catch (Exception ex)
                {
                    last = ex;
                    if (attempt >= client.Settings.StructuredOutputRetryCount) break;
                    cancellationToken.ThrowIfCancellationRequested();
                    var extracted = JsonUtils.ExtractJson(current);
                    if (regenerate != null && (string.IsNullOrWhiteSpace(extracted) || string.Equals(extracted.Trim(), "null", StringComparison.OrdinalIgnoreCase)))
                        current = await regenerate(cancellationToken).ConfigureAwait(false);
                    else
                    {
                        var broken = current == null ? string.Empty : current.Substring(0, Math.Min(current.Length, 50000));
                        current = await client.ChatStructuredAsync(
                            SkillPromptService.SafetyContract + "\r\n壊れたJSONを意味を変えずLayoutActionPlanへ修復する。",
                            broken, schema, schemaName, cancellationToken).ConfigureAwait(false);
                    }
                }
            }
            throw new InvalidOperationException("ローカルモデルの構造化JSONを解釈できませんでした。Structured output対応モデルを選ぶか、再試行してください。", last);
        }
    }
}
