# visual_polish

## 目的
余白、整列、情報密度、階層を改善する安全なActionPlanを作る。

## 入力
visual_check結果、Slide PNG、Shape JSON、Deck Style Profile、User Instruction。

## 出力JSON schema
`LayoutActionPlan { skill,target,summary,issues,actions[] }`。

## 禁止事項
既存Shape削除、画像化、12pt未満、スライド外配置、対象外編集。

## 使えるAction
move、resize、align、distribute、set_font_size、set_text、create_shape、create_textbox、create_line、create_arrow、tag_shape。

## 評価基準
変更を最小化し、各actionにreasonを付ける。文字削減は意味を維持する。

## 判断手順
1. Shape JSONの座標で重なり、はみ出し、余白、整列軸を確認する。
2. PNGは視線誘導とバランスの補助判断だけに使い、座標事実を優先する。
3. Deck Style Profileにあるフォント、余白、色、タイトル位置を基準にする。
4. 原則はmove/align/distributeで直し、resizeとset_font_sizeは必要な場合だけ使う。
5. set_textはユーザーが文章圧縮も求めた場合だけ提案する。
6. 1スライド8 actions以内を目安にし、最小差分にする。
7. issuesとactionsのslide/shape_idを入力JSONと照合してから出力する。
8. 文字を縮小する前に文章を短くするか、内容を分割する。
9. 既存デッキに強いスタイルがなければ、等しい左右余白、明確なグリッド、1つの主役、控えめな色数を使う。
10. カードや枠を機械的に増やさず、内容に合う単一構図を優先する。
11. ユーザー要求に必要なら、推奨Action以外の安全なActionを組み合わせてもよい。

## 例
`{"skill":"visual_polish","target":"@1-2","summary":"余白統一","actions":[{"type":"move","slide":1,"shape_id":12,"dx":20,"dy":0,"reason":"右余白確保"}]}`

## Context Policy
- include_geometry: true
- include_notes: false
- include_deck_style: true
- include_recent_chat: true
- include_slide_images: true
- recent_chat_messages: 6
