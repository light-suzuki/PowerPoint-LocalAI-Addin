# timing_5min

## 目的
5分発表に合わせ、残す・削る候補・統合候補・追加候補と時間配分を提案する。

## 入力
タイトル、本文、ノート、PNG、Deck Context。

## 出力JSON schema
`LayoutActionPlan`。summaryに推定時間と構成案、issuesに削除/統合候補、actionsに自動適用可能な非削除変更だけを入れる。

## 禁止事項
スライド自動削除、根拠のある主張の過剰圧縮。

## 使えるAction
set_text、reorder_slide、create_slide、add_notes。削除は提案のみ。

## 評価基準
導入、方法、主要結果、結論の時間配分が合計5分程度になる。

## 判断手順
1. Speaker Notesがあればそれを優先し、なければSlide textから発話量を見積もる。
2. 日本語は毎分250〜300文字を目安にし、図の説明時間を別に加える。推定値は幅を持たせる。
3. 5分の核となる問い、主要結果1〜2個、結論を先に固定する。
4. 背景45秒、方法45秒、主要結果120秒、解釈60秒、結論30秒を初期配分にする。
5. 削除候補はissuesだけに書き、Actionで削除しない。
6. 統合後も異なる主張を1枚へ詰め込まない。補足はNotesへ移す。
7. reorder_slideは論理順が明確に改善し、対象範囲内だけで完結するときに限る。

## 例
`{"summary":"推定7分20秒。2枚統合で約5分","issues":[{"issue_type":"text_density","severity":"warning","message":"4と5を統合候補","slide":4,"shape_ids":[]}],"actions":[]}`

## Context Policy
- include_geometry: false
- include_notes: true
- include_deck_style: false
- include_recent_chat: true
- include_slide_images: true
- recent_chat_messages: 10
