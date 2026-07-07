using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LocalPptSkillAgent.Models;
using LocalPptSkillAgent.Skills;
using LocalPptSkillAgent.Utils;
using Newtonsoft.Json.Linq;

namespace LocalPptSkillAgent.Services
{
    /// <summary>
    /// 意図分類のルーター。既定はキーワードによる決定的な高速判定(PlannerSkill)を使う。
    /// LM Studioのローカル小型モデルは分類が遅く(1回数十秒)不安定なため、LLM分類は
    /// 「キーワードでchat(=編集指示と判別できない)」かつ設定で明示的に有効化された場合だけ、
    /// 編集依頼を取りこぼしていないかの二次判定として使う。rollback/settings/明確な編集は常にキーワード優先。
    /// </summary>
    public class LlmRouterService
    {
        private readonly LmStudioClient _llm;
        private readonly Func<bool> _useLlmRouter;
        private readonly PlannerSkill _planner = new PlannerSkill();
        private readonly SelectorParser _selectors = new SelectorParser();

        public bool LastUsedLlm { get; private set; }

        public LlmRouterService(LmStudioClient llm, Func<bool> useLlmRouter = null)
        {
            _llm = llm;
            _useLlmRouter = useLlmRouter ?? (() => false);
        }

        // LLMに委ねてよい候補(編集系のみ)。rollback/settingsやchatはLLMで上書きしない。
        private static readonly string[] EscalationIntents =
        {
            "visual_polish", "visual_check", "align_shapes", "diagram_create", "slide_generate",
            "proofread", "translate", "deck_summary", "deck_qa", "fact_check", "timing_5min", "style_match"
        };

        private static readonly JObject RouteSchema = new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["intent"] = new JObject { ["type"] = "string", ["enum"] = new JArray(new object[] { "chat" }.Concat(EscalationIntents.Cast<object>()).ToArray()) },
                ["target"] = new JObject { ["type"] = "string", ["description"] = "@current/@selected/@all/@3/@1-5 など" }
            },
            ["required"] = new JArray("intent", "target")
        };

        private const string RouterSystemPrompt =
            "あなたはPowerPoint編集アシスタントの意図分類器。ユーザー発話が『スライドの編集依頼』か『会話』かを判定しJSONだけを返す。\n" +
            "明確な編集・作成・変換の依頼のときだけ編集intentにする。質問・相談・雑談・感想・説明依頼はchat。少しでも迷えばchat。\n" +
            "編集intent: visual_polish(見た目調整) / visual_check(問題点の診断) / align_shapes(整列) / diagram_create(図解作成) / " +
            "slide_generate(スライド作成) / proofread(校閲) / translate(翻訳) / deck_summary(要約) / deck_qa(内容を参照する質問) / " +
            "fact_check(出典確認) / timing_5min(発表時間に合わせる) / style_match(デザイン統一)。\n" +
            "target: 発話中の@セレクタがあればそのまま。なければ@current。デッキ全体なら@all。";

        public async Task<PlannerResult> PlanAsync(string userInput, string mode, IReadOnlyList<ChatMessage> recentMessages, CancellationToken cancellationToken)
        {
            LastUsedLlm = false;
            var text = userInput ?? string.Empty;
            var keywordPlan = _planner.Plan(text, mode);

            // モード強制時、または決定的に判別できた場合はキーワード判定を信頼する
            if (PlannerSkill.ModeIntent(mode) != null) return keywordPlan;
            if (keywordPlan.Intent != "chat") return keywordPlan;
            if (!_useLlmRouter() || string.IsNullOrWhiteSpace(_llm.Settings.TextModel)) return keywordPlan;

            // ここに来るのは「キーワードでは編集指示と判別できなかった」発話のみ。
            // 編集依頼の取りこぼしがないかLLMに二次判定させる。失敗時はchatのまま。
            try
            {
                var messages = new JArray { new JObject { ["role"] = "system", ["content"] = RouterSystemPrompt } };
                foreach (var message in (recentMessages ?? new List<ChatMessage>())
                    .Where(x => x.Kind == "message" && (x.Role == "user" || x.Role == "assistant"))
                    .Reverse().Take(2).Reverse())
                    messages.Add(new JObject { ["role"] = message.Role, ["content"] = Truncate(message.Content, 200) });
                messages.Add(new JObject { ["role"] = "user", ["content"] = text });

                var response = await _llm.ChatConversationStructuredAsync(messages, RouteSchema, "intent_route", cancellationToken).ConfigureAwait(false);
                var parsed = JObject.Parse(JsonUtils.ExtractJson(response));
                var intent = (string)parsed["intent"];
                LastUsedLlm = true;

                // LLMがchatと言えばchatのまま。編集intentのときだけ昇格する。
                if (string.IsNullOrWhiteSpace(intent) || !EscalationIntents.Contains(intent, StringComparer.OrdinalIgnoreCase))
                    return keywordPlan;

                var target = SafeTarget((string)parsed["target"], text);
                var upgraded = PlannerSkill.BuildResult(intent, target, text);
                upgraded.Explanation = $"(自動判定) {upgraded.Explanation}";
                return upgraded;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                DiagnosticLogService.Error("llm_router_fallback", ex);
                return keywordPlan;
            }
        }

        private string SafeTarget(string fromLlm, string userText)
        {
            // ユーザーが明示した@セレクタを最優先。LLMのtargetは@始まりで構文が通る場合だけ採用。
            var explicitSelector = _selectors.Parse(userText);
            if (userText.IndexOf(explicitSelector.Raw, StringComparison.OrdinalIgnoreCase) >= 0 && explicitSelector.Raw != "@current") return explicitSelector.Raw;
            if (string.IsNullOrWhiteSpace(fromLlm) || !fromLlm.Trim().StartsWith("@")) return "@current";
            try { return _selectors.Parse(fromLlm.Trim()).Raw; }
            catch { return "@current"; }
        }

        private static string Truncate(string value, int maxChars) => string.IsNullOrEmpty(value) || value.Length <= maxChars ? value : value.Substring(0, maxChars) + "…";
    }
}
