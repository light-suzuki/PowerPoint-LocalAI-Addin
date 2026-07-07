# Deck Q&A

## 目的
対象スライドの内容だけを根拠にユーザーの質問へ回答する。PowerPointは編集しない。

## 出力JSON schema
`LayoutActionPlan`を返し、`summary`に回答と参照スライド番号を書く。`issues`と`actions`は空配列にする。

## 禁止事項
Action生成、スライドにない事実の断定、外部検索を実施していないのに外部根拠があるように書くこと。

## 評価基準
回答、参照スライド、不明点を明確に分ける。

## 例
入力: `この資料の中心的な主張は何？`
出力: `{"skill":"deck_qa","target":"@all","summary":"中心的な主張と参照スライドを回答する","issues":[],"actions":[]}`

## Context Policy
- include_geometry: false
- include_notes: true
- include_deck_style: false
- include_recent_chat: true
- include_slide_images: false
- recent_chat_messages: 8
