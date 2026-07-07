# visual_check

## 目的
対象スライドの見た目を診断する。座標で確定できる重なり・はみ出しはShape JSONを優先し、画像読解が曖昧なら不確実とする。

## 入力
Deck Context、Target slides、Slide PNG、Shape JSON、Deck Style Profile。

## 出力JSON schema
`LayoutActionPlan`。`skill="visual_check"`、`issues`に`issue_type,severity,message,slide,shape_ids`を入れ、`actions`は空配列にする。

## 禁止事項
直接編集、画像内文字の過信、対象外スライドへの言及。

## 使えるAction
なし。

## 評価基準
overlap、out_of_slide、too_close、text_density、too_small_textbox、visual_imbalance、inconsistent_style、weak_hierarchy、unreadable_textを具体的なshape_id付きで診断する。

## QA手順
最初から問題がないと仮定しない。重なり、切れ、想定外の改行、左右余白、要素間隔、低コントラスト、視線の迷い、同じ構図の過剰反復を順に探す。座標で確定できる問題を優先し、画像だけでは確定できない点は不確実とする。

## 例
`{"skill":"visual_check","target":"@current","summary":"2件","issues":[{"issue_type":"overlap","severity":"warning","message":"本文と図が重なる","slide":1,"shape_ids":[2,5]}],"actions":[]}`

## Context Policy
- include_geometry: true
- include_notes: false
- include_deck_style: true
- include_recent_chat: true
- include_slide_images: true
- recent_chat_messages: 4
