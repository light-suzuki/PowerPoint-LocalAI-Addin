# simplify_text

## 目的
冗長な箇条書きや長文を、意味を保ったまま短く読みやすい表現に直す提案を返す。スライドは「読む文書」ではなく「見せる要点」であるという原則(1行=1アイデア)に沿う。

## 指示
各テキストShapeについて、次を判断して`set_text`の修正案を作る。
- 1項目=1アイデアに分割・圧縮する。1行は原則30文字以内、体言止めや動詞句で簡潔にする。
- 文末の「〜と考えられる」「〜することができる」等の冗長表現を削る。意味・数値・単位・遺伝子名・引用は変えない。
- 箇条書きは原則5項目以内。多すぎる場合は上位の要点に絞る提案をsummaryに書く。
- 元が既に十分簡潔なShapeは触らない(actionを作らない)。

## 出力JSON schema
`LayoutActionPlan`。改善対象ごとに`set_text`を1件作り、元の`slide`と`shape_id`を入力JSONから正確にコピーする。`reason`に何をなぜ短くしたかを書く。issuesは任意。

## 禁止事項
既存Shape・Slideの削除。対象範囲外の編集。意味・数値・固有名詞の改変。shape_idの推測。承認前の自動置換(提案のみ)。

## 例
`{"skill":"simplify_text","target":"@current","summary":"3件を簡潔化","issues":[],"actions":[{"type":"set_text","slide":2,"shape_id":7,"text":"CLV3がWUSを抑制し分裂組織を制御","reason":"冗長な文末と重複を削除し体言止めに"}]}`

## Context Policy
- include_geometry: false
- include_notes: false
- include_deck_style: false
- include_recent_chat: true
- include_slide_images: false
- recent_chat_messages: 4
