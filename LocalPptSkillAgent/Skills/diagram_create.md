# diagram_create

## 目的
編集可能なPowerPointネイティブ図形だけで図解する。

## 入力
User Instruction、Target slide、Shape JSON、Deck Style Profile、Deck Context。

## 出力JSON schema
`LayoutActionPlan`の`create_diagram` actionに`DiagramPlan {diagram_type,slide,placement,objects[]}`を格納する。各objectは`id,type,text,x,y,w,h,from,to,style_role`。

## 禁止事項
画像生成、編集不能化、既存Shape削除、12pt未満、デッキスタイル逸脱。

## 使えるAction
create_diagram、create_shape、create_textbox、create_line、create_arrow。

## 評価基準
pathway/process/comparison/before_after/hypothesis_model/gene_locus/receptor_complex/experimental_flow/three_step/cause_effectから選ぶ。線はfrom/toで接続する。

## 判断手順
1. ユーザーの主張をノード、関係、注記に分解し、1図1メッセージにする。
2. 既存Shapeの占有領域を避け、Slide width/height内に24pt以上の外周余白を確保する。
3. ノード間は原則16pt以上空け、ラベルは短く、本文フォントは16pt以上を想定する。
4. 因果・促進・抑制・推測を混同しない。推測関係はtextで「仮説」または「示唆」と明記する。
5. arrow/connectorはfrom/toに存在するobject idを使う。交差を最小化し、方向を統一する。
6. 既存図形の移動が必要でも、このSkillでは勝手に移動せず新規図解の配置だけを提案する。
7. 全objectのx,y,w,hとslideを自己点検してから出力する。

## 例
`{"actions":[{"type":"create_diagram","slide":3,"diagram":{"diagram_type":"pathway","slide":3,"objects":[{"id":"a","type":"rounded_rect","text":"CLV3","x":80,"y":160,"w":100,"h":50},{"id":"b","type":"rounded_rect","text":"WUS抑制","x":300,"y":160,"w":120,"h":50},{"type":"arrow","from":"a","to":"b"}]}}]}`

## Context Policy
- include_geometry: true
- include_notes: false
- include_deck_style: true
- include_recent_chat: true
- include_slide_images: true
- recent_chat_messages: 8
