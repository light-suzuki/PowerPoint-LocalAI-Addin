# LocalPptSkillAgent

**完全ローカルで動く、PowerPoint用のAIアシスタント アドイン。**
研究データや社外秘のスライドを、クラウドに一切出さずにローカルLLMで編集・相談できます。

![.NET Framework 4.8](https://img.shields.io/badge/.NET%20Framework-4.8-512BD4)
![VSTO](https://img.shields.io/badge/PowerPoint-VSTO%20Add--in-B7472A)
![License: MIT](https://img.shields.io/badge/License-MIT-green)

Windows版 Microsoft 365 PowerPoint の中で動く、.NET Framework 4.8 / C# / VSTO ベースのアドインです。右側のタスクペインに Copilot 風のチャットUIを追加し、自然文での相談とスライド編集の両方をローカルLLMで行います。

> UI・ドキュメントは日本語です。ローカルLLMとして [LM Studio](https://lmstudio.ai/) + Qwen3.5-9B での動作を確認しています。

---

## なぜこれを使うのか

### 🔒 機微データを外に出さない
推論はすべてローカルのLLM(既定は LM Studio)で完結します。研究データ、未発表データ、社外秘、患者・被験者情報を含むスライドでも、内容がクラウドAPIへ送信されません。ネットワークに出るのは、あなたが明示的にファクトチェックでMCP検索を使ったときだけです。

### 🧱 AIにPowerPointを直接触らせない安全設計
AIは JSON の `LayoutActionPlan`(編集案)を**提案するだけ**で、PowerPoint COM を直接操作しません。実際の編集は C# の `ActionValidator`(許可Action・対象範囲・shape ID・スライド境界・最小文字サイズ等を検証)と `ActionExecutor` が担います。適用前に必ず修正案を確認でき、before/after スナップショットで session 単位のロールバックも可能です。削除系Actionはそもそも許可リストにありません。

### 🔁 バックエンドは固定していない(差し替え可能)
既定は LM Studio の OpenAI互換エンドポイントですが、**OpenAI互換のchat/vision APIならエンドポイントとモデル名を設定タブで変えるだけ**で動きます。それ以外のバックエンド(自前サーバー、Ollama、llama.cpp、社内API等)も、`Services/LmStudioClient.cs` を差し替えれば接続できます。MITなので改造に制約はありません。

### 🧩 小型ローカルモデルを「縛って」使い物にする設計
9B級の小さいモデルでも実用になるよう、Skill(Markdown定義)・Context Policy・JSON Schema・C#側の enum 正規化・検証付き再試行で出力を制御しています。賢さの足りない部分を仕組みで補う方針です。

---

## 主な機能

- **チャットファーストUI**: 相談・質問・雑談はそのまま会話として応答し、`@current 見た目を整えて` のような明確な依頼だけを編集として処理。バブル型表示・提案チップ・コンテキスト使用量バー。
- **対象指定 `@`**: `@current` `@selected` `@all` `@3` `@1-5`(a枚目〜b枚目)`@slide:3` `@after:2` `@before:4` `@figures` `@text` `@notes`
- **コマンド `/`**: `/proofread @all 誤字を直して` のようにスキルを直接実行。「コマンド」タブに一覧。
- **GUIでスキル作成**: コマンドタブ/スキルタブから、コード無しで独自スキル(Markdown定義)を作成・保存。即 `/スキル名` で実行可能。
- **同梱スキル(17種)**: スライド生成、ネイティブ図形での図解、見た目調整、レイアウト診断、整列、校閲、翻訳、要約、資料Q&A、ファクトチェック(MCP)、5分再構成、スタイル統一、文章の簡潔化、結論型タイトル化 など。
- **スライド生成**: 内容計画(`SlideContentPlan`)→ C#検証 → レイアウト変換に分離し、モデルにPowerPoint操作と内容作成を同時にやらせない構造。1スライド1メッセージ・結論型タイトル等、公開されているプレゼン作成の定石を取り込み。
- **安全網**: session単位ロールバック、Action途中失敗時の自動復元、トークンを伏せる14日保持の診断ログ、多重送信防止、ローカル推論の中止。
- **設定の保護**: APIキーは Windows DPAPI(CurrentUser)で暗号化して保存。

詳細な設計は [`LocalPptSkillAgent/README.md`](LocalPptSkillAgent/README.md) を参照してください。

---

## アーキテクチャ概要

```
ユーザー発話
  → 意図ルーティング(既定=キーワード / 任意でLLM)
  → Skill(Markdown定義 + Context Policy)
  → LLM(LM Studio, JSON Schema強制)
  → LayoutActionPlan(JSON, 編集案)
  → ActionValidator(許可Action・範囲・境界・最小文字サイズ)
  → ユーザーが確認して適用
  → ActionExecutor(PowerPoint COM操作) + before/afterスナップショット
```

- `LocalPptSkillAgent/` — サービス・UI・Skillの共有ロジック(SDK形式 csproj、テスト対象)
- `LocalPptSkillAgent.Vsto/` — 実際のPowerPointホスト(VSTOプロジェクト)
- `LocalPptSkillAgent.Tests/` — xUnit テスト

---

## ビルド

1. Visual Studio 2022 で「Office/SharePoint 開発」ワークロードと .NET Framework 4.8 targeting pack を有効にする。
2. `LocalPptSkillAgent.sln` を開く。
3. `Any CPU` でビルドし、`LocalPptSkillAgent.Vsto` を実行(F5)すると PowerPoint が起動してアドインが読み込まれます。

共有ロジックとテストは CLI でもビルド/実行できます:

```
dotnet build LocalPptSkillAgent/LocalPptSkillAgent.csproj
dotnet test  LocalPptSkillAgent.Tests/LocalPptSkillAgent.Tests.csproj
```

> VSTOホスト(`LocalPptSkillAgent.Vsto`)は Visual Studio の MSBuild でのみビルドできます(`dotnet` CLI では Office 開発ターゲットが解決されません)。ClickOnce配布で署名する場合は各自の証明書で `SignManifests` を有効化してください(既定は無効)。

## 使い方(最短)

1. LM Studio を起動し、ローカルサーバーを開始してモデル(例: Qwen3.5-9B)をロード。
2. PowerPoint で「Local AI」→「Open Panel」。
3. 設定タブで Base URL・エンドポイント・(必要なら)APIキー・Text/Visionモデルを合わせる。
4. チャット欄に相談や `@current 見た目を整えて` などを入力。

---

## 改造・貢献について

**改造は歓迎です。** MITライセンスなので、フォーク・改変・再配布・商用利用いずれも自由です。特に歓迎する方向:

- **バックエンドの差し替え**(Ollama / llama.cpp / vLLM / 社内API など)
- **新しいSkillの追加**(Markdownを置くだけ、またはGUIから作成)
- **レンダリング/レイアウトの改善**、**他言語UI対応**

Issue・PR も気軽にどうぞ。設計の意図は各READMEとコード内コメントに日本語で書いています。

---

## クレジット

このプロジェクトは **[Claude](https://www.anthropic.com/claude)(Anthropic)と [Codex](https://openai.com/)(OpenAI)を使って開発**しました。スライド設計の方針は、公開されているプレゼン作成スキル(OpenAI/Codex の Presentations、Anthropic の pptx スキル等)の考え方を参考にしています。

## ライセンス

[MIT License](LICENSE) © 2026 light-suzuki
