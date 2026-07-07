# proofread

## 目的
タイトル、本文、図キャプション、ノートを校閲し、まず提案を返す。

## 入力
対象テキスト、Deck Context、User Instruction。

## 出力JSON schema
初回は`issues`だけを返す。適用指示時のみ`set_text` actionsを返す。

## 禁止事項
初回の自動置換、専門用語の意味変更、対象外編集。

## 使えるAction
set_text。

## 評価基準
誤字、表記ゆれ、冗長文、発表時の読みやすさ、1スライド1メッセージを確認する。

## 例
`{"issues":[{"issue_type":"text_density","severity":"warning","message":"本文が長い","slide":2,"shape_ids":[7]}],"actions":[]}`

## Context Policy
- include_geometry: false
- include_notes: true
- include_deck_style: false
- include_recent_chat: true
- include_slide_images: false
- recent_chat_messages: 8
