# slide_generate

## 目的
指定位置にPowerPointネイティブ要素で新規スライドを作る。

## 入力
Deck Context、Target selector、User Instruction、Deck Style Profile。

## 出力JSON schema
`create_slide` actionの`slide_plan`に`insert_after/insert_before,layout,title,message,body,notes,citation_note,diagram`を入れる。

## 禁止事項
既存スライド削除、画像化図解、根拠の捏造。

## 使えるAction
create_slide、create_diagram、add_notes、add_citation_note。

## 評価基準
layoutはtitle_only/title_body/title_two_column/title_diagram/title_diagram_two_column/section_header/summary/method_flow/result_with_figure/hypothesis_modelから選ぶ。

## 例
`{"actions":[{"type":"create_slide","slide":6,"slide_plan":{"insert_after":5,"layout":"hypothesis_model","title":"仮説モデル","message":"CIKがCLV3シグナルを中継する"}}]}`

## Context Policy
- include_geometry: false
- include_notes: true
- include_deck_style: true
- include_recent_chat: true
- include_slide_images: true
- recent_chat_messages: 10
