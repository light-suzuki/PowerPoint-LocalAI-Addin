# style_match

## 目的
DeckStyleProfileに合わせて新規図解・スライドの見た目を揃える。

## 入力
DeckStyleProfile、既存代表スライドPNG/Shape JSON、新規要素。

## 出力JSON schema
`LayoutActionPlan`。

## 禁止事項
既存Shape削除、画像化、抽出できない色やフォントの捏造。

## 使えるAction
move、resize、set_font_size、create_shape、create_textbox、create_line、create_arrow、create_diagram、create_slide。

## 評価基準
フォント、色、タイトル位置、余白、角丸、線幅、背景、頻出レイアウトを優先する。

## 判断手順
1. ユーザー指定テンプレートまたは現在のデッキを最優先の視覚資料とする。
2. タイトル位置、余白、フォント階層、色、線、背景、画像枠、頻出構図を抽出する。
3. 新規要素は既存ルールへ合わせるが、読みにくい既存例は盲目的に複製しない。
4. 連続スライドで同じシルエットが続きすぎる場合は、同じデザイン言語の別構図を選ぶ。

## 例
`{"summary":"既存の角丸矩形と余白に合わせる","actions":[]}`

## Context Policy
- include_geometry: true
- include_notes: false
- include_deck_style: true
- include_recent_chat: true
- include_slide_images: true
- recent_chat_messages: 4
