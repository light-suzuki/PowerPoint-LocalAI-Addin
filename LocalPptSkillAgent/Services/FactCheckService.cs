using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using System.Linq;
using LocalPptSkillAgent.Models;
using LocalPptSkillAgent.Utils;

namespace LocalPptSkillAgent.Services
{
    public class FactCheckService
    {
        private readonly LmStudioClient _llm;
        private readonly IMcpClient _mcp;
        public FactCheckService(LmStudioClient llm, IMcpClient mcp) { _llm = llm; _mcp = mcp; }

        public async Task<FactCheckResult> CheckAsync(string target, IReadOnlyList<SlideModel> slides, DeckContext context, CancellationToken cancellationToken = default(CancellationToken))
        {
            var prompt = new SkillPromptService(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "skills")).Load("fact_check");
            var selector = new SelectorParser().Parse(target);
            var input = new LocalPptSkillAgent.Skills.SkillInput { Context = context, Target = selector, Instruction = "検証可能な主張を抽出する。外部根拠取得前はneeds_source。", Slides = slides, Style = new DeckStyleAnalyzer().Analyze(slides) };
            var policy = SkillPromptService.ParseContextPolicy(prompt);
            var assembly = new ContextAssemblerService().Build(input, "fact_check", prompt, _llm.Settings, policy);
            var extracted = _llm.Settings.UseStructuredOutput
                ? await _llm.ChatStructuredAsync(prompt, assembly.UserPayload, StructuredSchemas.FactCheckResult, "fact_check_result", cancellationToken).ConfigureAwait(false)
                : await _llm.ChatAsync(prompt, assembly.UserPayload, cancellationToken).ConfigureAwait(false);
            var result = JsonUtils.Deserialize<FactCheckResult>(extracted) ?? new FactCheckResult { Target = target };
            result.Claims = result.Claims ?? new List<FactCheckClaim>(); result.ContextReport = assembly.Report;
            if (_llm.LastUsage != null) { result.ContextReport.ActualInputTokens = _llm.LastUsage.PromptTokens; result.ContextReport.ActualOutputTokens = _llm.LastUsage.CompletionTokens; }
            foreach (var claim in result.Claims)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var web = await _mcp.SearchWebAsync(claim.Claim).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                var papers = await _mcp.SearchPapersAsync(claim.Claim).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                var local = await _mcp.SearchLocalDocsAsync(claim.Claim).ConfigureAwait(false);
                var evidenceAvailable = !IsMissing(web) || !IsMissing(papers) || !IsMissing(local);
                if (!evidenceAvailable)
                {
                    claim.Status = "needs_source";
                    claim.Reason = "外部検索MCPが未設定、または根拠候補を取得できませんでした。";
                    continue;
                }
                var evidencePayload = LimitEvidence(claim, web, papers, local);
                var classifySystem = SkillPromptService.SafetyContract + "\r\n主張と外部証拠候補を比較する。statusはsupported/needs_source/overstated/uncertain/contradicted/out_of_scopeのみ。証拠候補にない内容を補わない。";
                var classify = _llm.Settings.UseStructuredOutput
                    ? await _llm.ChatStructuredAsync(classifySystem, evidencePayload, StructuredSchemas.FactCheckClaim, "fact_check_claim", cancellationToken).ConfigureAwait(false)
                    : await _llm.ChatAsync(classifySystem, evidencePayload, cancellationToken).ConfigureAwait(false);
                var checkedClaim = JsonUtils.Deserialize<FactCheckClaim>(classify);
                if (checkedClaim != null)
                {
                    claim.Status = checkedClaim.Status;
                    claim.Reason = checkedClaim.Reason;
                    claim.Evidence = checkedClaim.Evidence ?? new List<string>();
                    claim.SuggestedSlideText = checkedClaim.SuggestedSlideText;
                    result.ExternalEvidenceUsed = true;
                }
            }
            result.Target = target;
            return result;
        }

        private static bool IsMissing(string value) => string.IsNullOrWhiteSpace(value) ||
            value.StartsWith("MCP_NOT_CONFIGURED", StringComparison.Ordinal) ||
            value.StartsWith("MCP_NO_TOOL_RESULT", StringComparison.Ordinal);

        private string LimitEvidence(FactCheckClaim claim, string web, string papers, string local)
        {
            var maxChars = Math.Max(4000, (_llm.Settings.ContextLength - _llm.Settings.MaxTokens - _llm.Settings.InputSafetyMarginTokens) * 2);
            var perSource = Math.Max(1000, maxChars / 3);
            return JsonUtils.Serialize(new
            {
                claim,
                web = Truncate(web, perSource), papers = Truncate(papers, perSource), local = Truncate(local, perSource)
            });
        }
        private static string Truncate(string value, int length) => string.IsNullOrEmpty(value) || value.Length <= length ? value : value.Substring(0, length) + "…";
    }
}
