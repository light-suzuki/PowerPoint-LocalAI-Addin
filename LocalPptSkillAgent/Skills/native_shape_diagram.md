# native_shape_diagram

## 目的
PowerPointネイティブ図形だけで、後から編集可能な研究図解を作る。

## 得意テンプレート
シグナル経路、受容体複合体、遺伝子座マップ、実験フロー、表現型比較、仮説モデル、Before/After、因果関係、3ステップ、材料→方法→結果。

## 出力JSON schema
`LayoutActionPlan`の`create_diagram`に`DiagramPlan`を格納する。objectsはrectangle/rounded_rect/oval/text/arrow/line/connector/table。

## 使えるAction
create_diagram、create_shape、create_textbox、create_line、create_arrow、tag_shape。

## 禁止事項
画像1枚で生成、12pt未満、スライド外配置、既存Shape削除、接続方向が不明な矢印。

## 評価基準
ラベルが簡潔、矢印方向が明確、余白十分、DeckStyleProfile準拠、全ShapeにAIタグを付けられる構造。

## 判断手順
1. 主張をノード、関係、注記へ分解し、最も単純な得意テンプレートを1つ選ぶ。
2. 既存Shapeを避け、外周24pt以上、ノード間16pt以上を確保する。
3. ノード本文は短く、16pt以上で読める量にする。
4. arrow/connectorは存在するfrom/to idだけを使い、因果方向を統一する。
5. 候補や仮説は図中に明記し、確立した経路と同じ線種・表現で断定しない。
6. 全objectの座標、サイズ、重複、スライド境界を自己点検する。

## 例
入力: `A→B→Cの3段階模式図`
出力: `{"skill":"native_shape_diagram","target":"@current","summary":"3段階図を作る","issues":[],"actions":[{"type":"create_diagram","slide":1,"diagram":{"diagram_type":"three_step","slide":1,"objects":[{"id":"a","type":"rounded_rect","text":"A","x":80,"y":180,"w":150,"h":60},{"id":"b","type":"rounded_rect","text":"B","x":300,"y":180,"w":150,"h":60},{"id":"c","type":"rounded_rect","text":"C","x":520,"y":180,"w":150,"h":60},{"id":"ab","type":"arrow","from":"a","to":"b"},{"id":"bc","type":"arrow","from":"b","to":"c"}]}}]}`

## Context Policy
- include_geometry: true
- include_notes: false
- include_deck_style: true
- include_recent_chat: true
- include_slide_images: true
- recent_chat_messages: 8
