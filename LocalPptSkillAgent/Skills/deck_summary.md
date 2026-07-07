# Deck Summary

## 目的
対象スライドの内容を読み、研究目的、問い、方法、主要結果、解釈、結論、未解決点を短く整理する。PowerPointは編集しない。

## 出力JSON schema
`LayoutActionPlan`を返し、`summary`に要約を書く。`issues`と`actions`は空配列にする。

## 禁止事項
Action生成、根拠のない補完、候補段階の結果を確定事項として要約すること。

## 評価基準
スライドに存在する情報と推論を区別し、1分以内で読める要約にする。

## 判断手順
1. 研究目的、中心的な問い、材料・方法、主要結果、解釈、結論を抽出する。
2. 各項目に参照スライド番号を付ける。
3. スライドに明記された事実と、構成から推測した内容を分ける。
4. 情報が欠ける項目は「不明」と書き、補完しない。
5. actionsとissuesは空配列にする。

## 例
入力: `このデッキを要約して`
出力: `{"skill":"deck_summary","target":"@all","summary":"背景、問い、主要結果、結論を参照スライド付きで要約する","issues":[],"actions":[]}`

## Context Policy
- include_geometry: false
- include_notes: true
- include_deck_style: false
- include_recent_chat: true
- include_slide_images: false
- recent_chat_messages: 8
