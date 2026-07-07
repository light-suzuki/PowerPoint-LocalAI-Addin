# Translate

## 目的
対象スライドのタイトル、本文、図キャプション、発表者ノートを指定言語へ翻訳する。固有名詞、遺伝子名、変異体名、引用表記を保持する。

## 入力
Deck Context、対象スライド、Shape JSON、ユーザーが指定した翻訳先言語。

## 出力JSON schema
`LayoutActionPlan`。翻訳候補ごとに`set_text`を作り、元のshape_idとslideを必ず指定する。

## 使えるAction
set_text、add_notes。

## 禁止事項
ユーザー承認前の置換、意味の追加、数値・単位・引用の改変、遺伝子記号の一般語化、対象外スライドの変更。

## 評価基準
意味保持、専門用語の一貫性、発表で読める自然さ、元テキストとの対応が追跡可能であること。

## 判断手順
1. 指定された翻訳先言語を確認し、不明ならActionを作らずsummaryで不足情報を示す。
2. 数値、単位、遺伝子名、変異体名、アクセッション、引用、URLは変更しない。
3. タイトルは短く、本文はスライド向けの簡潔さを保つ。説明を勝手に追加しない。
4. shape_idごとに元テキストと翻訳を対応させ、set_textを1 Shapeずつ作る。
5. Notesはadd_notes、Slide上の文字はset_textで分ける。

## 例
入力: `@selected 英語に翻訳して`
出力: `{"skill":"translate","target":"@selected","summary":"選択テキストを英訳する","issues":[],"actions":[{"type":"set_text","slide":1,"shape_id":12,"text":"Translated text","reason":"意味と専門用語を保持した翻訳"}]}`

## Context Policy
- include_geometry: false
- include_notes: true
- include_deck_style: false
- include_recent_chat: true
- include_slide_images: false
- recent_chat_messages: 8
