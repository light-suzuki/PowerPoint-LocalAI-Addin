# align_shapes

## 目的
選択Shapeを座標ベースで整列・等間隔化する。LLMなしのルールベース処理を優先する。

## 入力
Target、Shape JSON、User Instruction。

## 出力JSON schema
`LayoutActionPlan`。

## 禁止事項
削除、サイズ変更、対象外Shape操作。

## 使えるAction
align、distribute。

## 評価基準
left/right/top/bottom/center_horizontal/center_vertical、horizontal/verticalを一意に選ぶ。

## 例
`{"actions":[{"type":"align","slide":3,"shape_ids":[1,2,3],"align_mode":"left"}]}`

## Context Policy
- include_geometry: true
- include_notes: false
- include_deck_style: true
- include_recent_chat: false
- include_slide_images: false
- recent_chat_messages: 0
