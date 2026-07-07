# LocalPptSkillAgent

Windows版 Microsoft 365 PowerPoint内で動く、.NET Framework 4.8 / C# / VSTOベースのローカルLLM/VLM編集アドインです。AIはJSONの`LayoutActionPlan`だけを提案し、実編集は`ActionValidator`と`ActionExecutor`が行います。

## 実装済み

- チャットを初期表示する5タブ式の右側WinFormsタスクペイン（チャット、設定、コンテキスト、Skills、履歴）
- Ribbon XML（`Local AI` → `LocalPptSkillAgent` → `Open Panel`）
- `@current`、`@selected`、`@all`、`@3`、範囲、挿入位置、figures/text/notes selector
- 自然言語を`PlannerSkill`で解釈し、Planner → Skill → ActionPlan → Validator → Executorの順で処理
- 修正案の全件適用・部分適用・ロールバック
- Shape/Slide/Notes抽出、PNG export、Deck style抽出
- LM Studio OpenAI-compatible chat/vision、現行`/api/v1/models/load`、models取得、Text/Vision個別load/unload
- 9B級ローカルモデル向けTask Contract、Skill別推奨Action、先頭/末尾の応答契約、短い検証付き再試行
- スライド生成を`SlideContentPlan → C#検証 → LayoutActionPlan`へ分離し、モデルにPowerPoint操作と内容作成を同時実行させない構造
- TextモデルのJSON、指定枚数、読み取り専用Skillを実モデルで検査するSLM適合テスト
- JSON Schema Structured Output、JSON修復再試行、モデル適合・Vision capability・実ロードcontext確認
- Markdown Skill 15種とC# Skill router。研究スライド記述、ネイティブ図形、要約、資料Q&A、翻訳を含む
- 研究発表、植物科学、輪読、進捗報告、学位審査、5分発表の初期コンテキスト
- チャット履歴の保存、新規チャット、要約付き新規チャット
- 文字数ベースのコンテキスト予算推定と4段階警告
- Instruction、Deck Context、要約、Slide、直近会話を別枠で配分するContext Assembler
- SkillごとのContext Policy、固定Safety Contract、Skill定義検証と更新キャッシュ
- ユーザー向け説明・原因候補・対処方法・技術詳細に分けたエラー表示
- 座標ベース整列・等間隔化
- Action allow-list、対象範囲、shape ID、12pt、スライド境界検証
- 新規Shape・Diagram object・接続先・異常数値・Action件数を含む深い事前検証
- ネイティブShape/TextBox/Line/Arrow/Diagram/Slide作成
- `%LOCALAPPDATA%\LocalPptSkillAgent\sessions`へのbefore/after snapshot、PowerPoint tags、session単位rollback
- 未保存デッキでも一意なDeck ID、Action途中失敗時の自動復元と失敗監査履歴
- 多重送信防止、ローカル推論の中止、14日保持のJSONL診断ログ
- LM Studio native `/api/v1/chat`経由のMCP検索。`~/.lmstudio/mcp.json`から検索・fetch・filesystem系だけを自動検出
- MCP interface、mock、delegate adapter。MCP未設定またはtool call不成立時はfact checkを`needs_source`に固定

## 開発環境

1. Visual Studio 2022で「Office/SharePoint 開発」ワークロードと.NET Framework 4.8 targeting packを有効にする。
2. `LocalPptSkillAgent.sln`を開く。実際のPowerPointホストは隣接する`LocalPptSkillAgent.Vsto`プロジェクトである。
3. VSTOホストは`LocalPptSkillAgent`のサービス/UIロジックを参照し、署名済みVSTO manifestを生成する。
4. x64 PowerPoint向けに`Any CPU`か`x64`でビルドし、F5でPowerPointを起動する。

SDK形式csprojはサービス/UIロジックとテスト用です。VSTO manifest発行・配置はVisual Studio Office Developer Tools側の`LocalPptSkillAgent.Vsto`を使います。

## Chat-first UI

Copilot/GPT/Claudeのデスクトップアシスタントに寄せた、バブル型のチャットファーストUIです。ユーザーはSkill名を指定せず、単一の入力欄に自然文を書きます。

- **会話も編集も同じ入力欄**: `このデッキの改善点を教えて`のような相談・質問はそのまま会話として答え、`@current 見た目を整えて`、`@1-5 5分発表用に再構成して`、`@3 図解にして`のような明確な依頼だけを編集として処理します。編集は必ず修正案を提示し、確認後に適用します。
- **バブル表示**: ユーザーは右・AIは左の角丸バブル。処理中はタイピングインジケータを表示します。
- **入力補助**: 対象チップ(現在/選択中/全スライド)とモード選択(自動判定・会話のみ・見た目調整など)、空状態での発話例チップで「どこに何を入れるか」を明示します。
- **ヘッダーの`＋新規`と`⋯`メニュー**に、新規チャット・要約して新規・履歴・直前の編集を戻す、をまとめました。

内部の安全構造は従来どおり維持され、AIがPowerPoint COMを直接操作することはありません。

### コマンド

チャット欄では2種類の記法が使えます。「コマンド」タブに一覧があり、ダブルクリックで入力欄へ挿入できます。

- `@` 対象指定: `@current` `@selected` `@all` `@3` `@1-5` `@slide:3` `@after:2` `@before:4` `@figures` `@text` `@notes`
- `/` コマンド: `/スキル名 指示文` で意図判定を飛ばして指定スキルを直接実行します(例: `/proofread @all 誤字を直して`)。不明なコマンドは利用可能な一覧を返します。

### GUIでのスキル作成

「コマンド」タブの`＋新しいスキルを作成`から、スキル名・目的・指示・禁止事項・Context PolicyをGUIで入力してスキルを作成できます。生成されるMarkdownは必須セクション検証を通してから保存され、即座に`/スキル名`で実行可能になります。C#実装のないMarkdown定義スキルは`CustomMarkdownSkill`として共通ハーネス(Context Assembler・JSON Schema・ActionValidator)で実行されます。

### 小型モデル向けのenum正規化(重要)

`response_format`の`strict=false`ではLM Studioがenumを強制しないため、9B級モデルは`layout:"process_flow"`や`slide_role:"conclusion"`のような「正規enumの言い換え」を出します。これを放置すると未知レイアウトが平板な`title_body`へ潰れ、モデルが意図した2カラム/プロセス図/カード構成が失われます。

`SlideContentPlanningService`は`MapLayout/MapComposition/MapVisual/MapRole`で言い換えを最も近い正規enumへ寄せます(例: `process_flow→method_flow`、`title_single_focus→title_only`、`conceptual_model→hypothesis_model`)。マップできない場合だけ`title_body`へ倒します。あわせて生成プロンプトに正規語彙を明示し、ドリフト自体を減らしています。実機のQwen3.5-9Bで、内容(結論型タイトル・1スライド1メッセージ・遺伝子名保持)は実用水準、レイアウト選択も正しく反映されることを確認しています。

### 追加スキル(公開デッキの原則ベース)

- `simplify_text`(`/simplify_text`、「短くして」): 冗長な箇条書きを意味・数値・固有名詞を保ったまま1行1アイデアへ圧縮する`set_text`提案。
- `action_titles`(`/action_titles`、「タイトルを結論型にして」): 「背景」「結果」等の話題ラベルを、そのスライドの主張・発見を述べるアクションタイトル(So-What)へ書き換える`set_text`提案。タイトルのみ対象。

いずれも既存の安全ハーネス(Context Assembler・JSON Schema・ActionValidator)で実行され、shape_idを入力から正確にコピーすることを実機確認済みです。

### モデルのロード

チャットヘッダーの`⋯`メニューから「Textモデルをロード」「Visionモデルをロード」「モデルの状態を確認」を実行できます(設定タブのボタン群も従来どおり)。

### 高DPI対応

Per-Monitor V2のDPIコンテキストでタスクペインを生成し、`AutoScaleMode.Dpi`でスケールします。高DPIディスプレイでの文字のぼやけを防ぎます。

### 会話パス(ConversationService)

編集を伴わない発話は`ConversationService`が処理します。デッキ文脈・スライド本文・会話履歴を「命令ではない参考情報」として渡し、通常のマルチターンチャットとして日本語で応答します。参考情報は入力予算の6割を上限に圧縮し、履歴と応答の余地を確保します。Qwen3等の`<think>…</think>`ブロックは`LmStudioClient.StripThinkBlocks`で除去してから表示します。

### 意図ルーティング(LlmRouterService)

意図判定は既定でキーワードベースの決定的判定(`PlannerSkill`)を使います。高速・確実で、`戻して`→rollback、`@3 校閲して`→proofreadのように判別し、いずれにも当てはまらない発話はchat(会話)へフォールバックします。

設定タブの`意図判定をLLMに任せる`を有効にすると、**キーワードで会話と判定された発話に限って**LLMへ二次判定を投げ、取りこぼした編集依頼だけを昇格させます。rollback・settings・明確な編集は常にキーワード優先で、LLMに上書きさせません。ローカル小型モデルでは1分類に数十秒かかり誤判定もあるため、既定は無効です。

## Settings Persistence

LM Studio設定は次へ保存します。

```text
%LOCALAPPDATA%\LocalPptSkillAgent\settings.json
```

API keyはWindows DPAPIのCurrentUserスコープで暗号化して保存します。同じWindowsユーザーだけが復号できます。共有Windowsアカウントでは、同一ユーザーとして動く他プロセスから完全に隔離される仕組みではない点に注意してください。設定ファイルの破損や保存失敗があっても、既定値へフォールバックしてアドインを起動します。

## Context Budget

チャット履歴、Deck Context、スライド情報、Skill promptの推定使用量をProgressBarと`安全`、`注意`、`危険`、`上限付近`で表示します。`Context length - Max tokens - Safety margin`を入力予算とし、Instruction、Deck Context、前チャット要約、Slide情報、直近会話に上限を分けます。長いDeck ContextだけでSlide情報が脱落しない設計です。APIがusageを返した場合は推定値と実測prompt tokensを併記します。

## Skill Context Policy

各`skills/*.md`には次の宣言が必須です。Skill編集画面は、目的、出力JSON schema、禁止事項、Context Policyが不足したファイルを保存しません。

```text
## Context Policy
- include_geometry: true
- include_notes: false
- include_deck_style: true
- include_recent_chat: true
- include_slide_images: true
- recent_chat_messages: 6
```

固定Safety Contractは編集可能なSkill本文の前後に追加されます。スライド本文、Notes、画像内文字、検索結果を命令として扱わず、対象外編集、既存Shape削除、科学的断定の強化を禁止します。

## Small Local Model Harness

ローカル9B級モデルにはPowerPoint向けのTask Contractを与えますが、Skill別Actionは命令ではなく推奨です。モデルはユーザー要求の範囲で関連する安全なActionを組み合わせ、物語構成、視覚階層、構図、レイアウト変化を判断できます。C#が強制するのは削除禁止、対象範囲、Shape ID、境界、最小文字サイズなど実害を防ぐ条件です。

新規スライドではモデルが`slide_role/layout/composition/visual_type/visual_brief/title/message/body/notes/citation_note`を決めます。指定枚数と文字量を検証した後、C#が内容に応じて単一焦点、二分割、比較、プロセス、カード等のネイティブPowerPoint構成へ変換します。既存デッキのスタイルがある場合はそちらを優先します。

スライド設計原則はOpenAI/CodexのPresentations、PPTX、Beautiful PowerPoint Skillを参照し、1スライド1メッセージ、結論型タイトル、グリッドと余白、縮小より文章圧縮、隣接スライドの構図変化、render→inspect→fixのQAループを取り込んでいます。Microsoft Agent Skillsの方針と同様に、全Skillを常時詰め込まずPlannerが必要なSkillだけを選びます。

設定タブの`SLM適合テスト`は、選択中Textモデルについて次を実推論で確認します。

- JSON Schemaに従えること
- 指定枚数と`create_slide`限定を守れること
- 読み取り専用SkillでActionを生成しないこと

## Chat Sessions

チャット履歴は次へ保存します。

```text
%LOCALAPPDATA%\LocalPptSkillAgent\chats\
```

`新しいチャット`ではDeck Context、LM Studio設定、AI編集履歴を維持します。`現在のチャットを要約して新規`では目的、編集方針、主要編集、未完了タスク、注意点、モデル、Skill設定を要約として引き継ぎます。

## User-friendly Errors

例外本文をそのまま主表示せず、短い説明、原因候補、対処方法、技術詳細へ分離します。LM Studio接続、モデル未ロード、Vision入力、ActionPlan検証、コンテキスト上限を個別に案内します。

診断ログは`%LOCALAPPDATA%\LocalPptSkillAgent\logs\lpsa_YYYYMMDD.jsonl`へ保存し、API tokenらしい値を伏せて14日後に削除します。

## LM Studio

既定値は`http://localhost:1234/v1`と`http://localhost:1234/v1/chat/completions`です。タスクペインから変更できます。LM StudioでAPI認証やカスタムポート(例: `12345`)を使う場合は、設定タブでBase URL・エンドポイント・API keyを合わせてください。モデル管理APIがLM Studioのバージョンと合わない場合、アドインは落とさず手動ロードを案内します。

設定タブは「LM Studio接続」「モデル」「生成パラメータ」「コンテキスト」「詳細」「ファクトチェック連携」の見出しでグルーピングし、各項目に日本語の補足を付けています。保存・接続確認・モデル操作のボタンは上部固定です。

設定タブではTemperature、Top P、Seed、Context length、Max output tokens、入力Safety margin、直近会話件数、request timeout、Text reasoning effort/budget、Structured Output、JSON修復回数を制御できます。Text reasoning effortの既定値は`low`、簡潔な内部検討の目安は1000 tokensです。Thinkingを維持しながら、ActionPlan生成前に出力枠を使い切るのを防ぎます。Structured OutputはLM Studioの`response_format.type=json_schema`を使います。小型モデルなどで非対応の場合は設定から無効化できます。

Text/Visionモデルのロード時は別々のcontext lengthを使い、LM Studio REST APIで公開されている`eval_batch_size`、`flash_attention`、`offload_kv_cache_to_gpu`を渡します。KV cacheのK/V量子化、GPU layer、MoE weightのCPU配置はREST load APIの公開項目ではないため、LM Studio側のモデル別設定を使用します。

ファクトチェックはLM Studio 0.4以降のnative endpoint `http://localhost:1234/api/v1/chat`を使います。現在の`%USERPROFILE%\.lmstudio\mcp.json`から検索、fetch、filesystem系MCPだけを選びます。Playwright、Win CLI、desktop commander等の操作系MCPはファクトチェック要求に渡しません。LM StudioでAPI認証を有効にした場合、設定タブにtokenを入力して保存できます。

## 安全境界

- AI出力を直接COM操作へ渡さない。
- 削除Actionはallow-listにない。
- fact checkはMCP等の外部根拠が取得できない限り`supported`にしない。
- PowerPoint Undoに加えて独自snapshotを保存する。
- LM Studioが返したJSONはSchemaとActionValidatorを通過するまで実行しない。
- MCPは検索・論文・ローカル資料・fetch系だけをファクトチェックへ渡す。
