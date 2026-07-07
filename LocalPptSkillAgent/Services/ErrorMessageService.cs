using System;
using System.Collections.Generic;
using System.Net.Http;
using LocalPptSkillAgent.Models;

namespace LocalPptSkillAgent.Services
{
    public class ErrorMessageService
    {
        public UserFriendlyError Convert(Exception exception)
        {
            var text = exception?.Message ?? "不明なエラーです。";
            if (exception is TimeoutException || text.IndexOf("秒以内に完了", StringComparison.OrdinalIgnoreCase) >= 0)
                return Error("Textモデルの応答待ちがタイムアウトしました", "ローカル推論が設定時間内に完了しませんでした。PowerPointは変更されていません。", null,
                    new[] { "設定タブのRequest timeoutを300秒程度にしてください", "Text reasoning effortをlowにしてください", "対象スライド範囲を狭めてください" }, exception);
            if (text.IndexOf("本文を返しませんでした", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("reasoning_chars", StringComparison.OrdinalIgnoreCase) >= 0)
                return Error("Textモデルが回答本文を返しませんでした", "モデルが思考だけで出力枠を使い切った可能性があります。PowerPointは変更されていません。", null,
                    new[] { "設定タブのText reasoning effortをlowにしてください", "Reasoning budgetを短くするかMax tokensを増やしてください", "同じ指示をもう一度送ってください" }, exception);
            if (text.IndexOf("構造化JSON", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("ActionPlanがnull", StringComparison.OrdinalIgnoreCase) >= 0)
                return Error("AIの修正案を読み取れませんでした", "ローカルモデルが必要なJSON形式を返しませんでした。PowerPointは変更されていません。", null,
                    new[] { "同じ指示をもう一度送ってください", "設定タブでJSON構造テストを実行してください", "指示を短く具体的にしてください" }, exception);
            if (text.IndexOf("vision", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("画像", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("image", StringComparison.OrdinalIgnoreCase) >= 0)
                return Error("スライド画像の解析に失敗しました", "Visionモデルが画像入力に対応していない可能性があります。", null,
                    new[] { "Vision対応モデルを選んでください", "Vision input modeを確認してください", "スライドPNGを書き出せるか確認してください" }, exception);
            if (exception is HttpRequestException || text.IndexOf("LM Studio", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("connection", StringComparison.OrdinalIgnoreCase) >= 0)
                return Error("LM Studioに接続できません", "ローカルAIサーバーから応答がありません。",
                    new[] { "LM Studioが起動していない", "Local ServerがStartされていない", "EndpointまたはAPI keyが間違っている", "モデルがロードされていない" },
                    new[] { "LM Studioを起動してください", "Developer / Local ServerをStartしてください", "設定タブでEndpointとAPI keyを確認してください", "接続確認をもう一度押してください" }, exception);
            if (text.IndexOf("モデルを選択", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("model", StringComparison.OrdinalIgnoreCase) >= 0 && text.IndexOf("load", StringComparison.OrdinalIgnoreCase) >= 0)
                return Error("モデルがロードされていません", "TextモデルまたはVisionモデルを利用できません。", null,
                    new[] { "設定タブでモデルを選択してください", "LM Studio側でモデルをロードしてください", "モデル一覧更新を押してください" }, exception);
            if (text.IndexOf("ActionPlan", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("適用できません", StringComparison.OrdinalIgnoreCase) >= 0)
                return Error("修正案を適用できません", "AIが作成した修正案に、安全に適用できない操作が含まれています。", null,
                    new[] { "修正案を再生成してください", "対象範囲を狭めてください", "問題のあるActionを外して部分適用してください" }, exception);
            return Error("処理を完了できませんでした", "操作中に問題が発生しました。", null,
                new[] { "入力内容と対象範囲を確認してください", "同じ操作をもう一度実行してください" }, exception);
        }
        public UserFriendlyError ContextLimit() => Error("コンテキストが上限に近づいています", "過去の会話やスライド情報を十分に参照できなくなる可能性があります。", null,
            new[] { "新しいチャットに切り替えてください", "現在のチャットを要約して新規チャットを開始してください", "対象スライド範囲を狭めてください" }, null);
        private static UserFriendlyError Error(string title, string message, IEnumerable<string> causes, IEnumerable<string> actions, Exception technical) => new UserFriendlyError
        {
            Title = title, Message = message,
            PossibleCauses = causes == null ? new List<string>() : new List<string>(causes),
            SuggestedActions = actions == null ? new List<string>() : new List<string>(actions),
            TechnicalDetails = technical?.ToString()
        };
    }
}
