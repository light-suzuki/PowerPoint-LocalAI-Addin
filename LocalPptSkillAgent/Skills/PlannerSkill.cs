using System;
using System.Collections.Generic;
using System.Linq;
using LocalPptSkillAgent.Models;
using LocalPptSkillAgent.Services;
using LocalPptSkillAgent.Utils;

namespace LocalPptSkillAgent.Skills
{
    public class PlannerSkill
    {
        public static readonly string[] KnownIntents =
        {
            "chat", "visual_polish", "visual_check", "align_shapes", "diagram_create", "slide_generate",
            "proofread", "translate", "deck_summary", "deck_qa", "fact_check", "timing_5min", "style_match",
            "simplify_text", "action_titles",
            "rollback", "settings"
        };

        private readonly SelectorParser _selectors = new SelectorParser();

        public PlannerResult Plan(string userInput, string mode = "auto")
        {
            var text = userInput ?? string.Empty;
            var target = _selectors.Parse(text);
            var intent = ModeIntent(mode) ?? DetectIntent(text);
            return BuildResult(intent, target.Raw, text);
        }

        public string PlanJson(string userInput, string mode = "auto") => JsonUtils.Serialize(Plan(userInput, mode));

        public static PlannerResult BuildResult(string intent, string targetRaw, string instruction)
        {
            if (!KnownIntents.Contains(intent ?? string.Empty, StringComparer.OrdinalIgnoreCase)) intent = "chat";
            intent = intent.ToLowerInvariant();
            if (targetRaw == "@current" && (intent == "deck_summary" || intent == "deck_qa") && Has(instruction, "デッキ", "資料全体", "プレゼン全体", "全スライド")) targetRaw = "@all";
            var result = new PlannerResult
            {
                Target = targetRaw, Intent = intent, Instruction = instruction,
                RequiresPreview = intent != "rollback" && intent != "settings" && intent != "chat",
                RequiresUserConfirmation = intent != "visual_check" && intent != "proofread" && intent != "fact_check" && intent != "rollback" && intent != "chat",
                NeedsVision = new[] { "visual_check", "visual_polish", "diagram_create", "style_match", "timing_5min" }.Contains(intent),
                NeedsMcp = intent == "fact_check"
            };
            result.Skills = SkillsFor(intent);
            result.Explanation = Explain(intent, result.Skills);
            return result;
        }

        public static string ModeIntent(string mode)
        {
            switch ((mode ?? "auto").ToLowerInvariant())
            {
                case "見た目": case "visual": return "visual_polish";
                case "図解": case "diagram": return "diagram_create";
                case "校閲": case "proofread": return "proofread";
                case "ファクトチェック": case "fact_check": return "fact_check";
                case "スライド生成": case "slide_generate": return "slide_generate";
                case "5分再構成": case "timing_5min": return "timing_5min";
                case "翻訳": case "translate": return "translate";
                case "要約": case "deck_summary": return "deck_summary";
                case "会話": case "chat": return "chat";
                default: return null;
            }
        }

        private static string DetectIntent(string text)
        {
            if (Has(text, "戻して", "取り消", "undo", "rollback")) return "rollback";
            if (Has(text, "設定", "endpoint", "api key", "モデル選択")) return "settings";
            if (Has(text, "ファクトチェック", "引用", "根拠", "出典")) return "fact_check";
            if (Has(text, "5分", "時間内", "削る", "再構成")) return "timing_5min";
            if (Has(text, "翻訳", "英語にして", "日本語にして", "translate")) return "translate";
            if (Has(text, "要約", "まとめて", "全体像", "概要")) return "deck_summary";
            if (Has(text, "新しいスライド", "スライドを追加", "追加して", "スライド作って", "スライドを作って", "スライド作成", "枚のスライド", "プレゼン作って", "資料作って")) return "slide_generate";
            if (Has(text, "図にして", "模式図", "powerpoint図形", "図解")) return "diagram_create";
            if (Has(text, "タイトルを直", "タイトルを改善", "見出しを直", "結論型", "アクションタイトル")) return "action_titles";
            if (Has(text, "短くして", "簡潔に", "冗長", "端的に", "文章を減ら")) return "simplify_text";
            if (Has(text, "校閲", "誤字", "表記ゆれ", "文章を直")) return "proofread";
            if (Has(text, "整列", "等間隔", "中央揃え", "右に寄せ", "左に寄せ")) return "align_shapes";
            if (Has(text, "見た目", "整えて", "余白", "読みやすく")) return "visual_polish";
            if (Has(text, "スタイル", "デザインに合わせ")) return "style_match";
            if (Has(text, "確認して", "チェックして", "診断して")) return "visual_check";
            if (Has(text, "どのスライド", "弱点")) return "deck_qa";
            return "chat";
        }

        private static List<string> SkillsFor(string intent)
        {
            switch (intent)
            {
                case "visual_polish": return new List<string> { "visual_check", "visual_polish", "proofread" };
                case "diagram_create": return new List<string> { "native_shape_diagram" };
                case "slide_generate": return new List<string> { "research_slide_writer" };
                case "timing_5min": return new List<string> { "timing_5min", "research_slide_writer" };
                case "translate": return new List<string> { "translate" };
                case "deck_summary": return new List<string> { "deck_summary" };
                case "deck_qa": return new List<string> { "deck_qa" };
                case "chat": case "rollback": case "settings": case "fact_check": return new List<string>();
                default: return new List<string> { intent };
            }
        }

        private static string Explain(string intent, IList<string> skills) => intent == "chat"
            ? "会話として応答します。"
            : $"{intent}として解釈し、{(skills.Count == 0 ? "専用処理" : string.Join("、", skills))}を実行します。";
        private static bool Has(string text, params string[] values) => values.Any(x => text.IndexOf(x, StringComparison.OrdinalIgnoreCase) >= 0);
    }
}
