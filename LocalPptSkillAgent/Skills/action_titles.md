# action_titles

## 目的
各スライドのタイトルを「内容ラベル」から「結論・主張(アクションタイトル / So-What)」へ改善する提案を返す。コンサルティングや研究発表で用いられる、タイトルだけ読めば筋が通る構成を目指す。

## 指示
タイトルとみられるShape(スライド上部の見出し)について判断する。
- 「背景」「実験方法」「結果」のような話題ラベルは、そのスライドが伝える結論・発見・問いへ書き換える。例: 「結果」→「Fa変異体では分裂組織が2倍に拡大する」。
- タイトルは1行・原則32文字以内。数値や固有名詞は本文にあるものだけ使い、新事実を作らない。
- 断定を強めない。候補・相関・示唆は「〜を示唆」「候補」等を保つ。
- 既に結論型で良いタイトルは触らない。
- デッキ全体でタイトルだけを並べると問い→証拠→結論の流れになるかをsummaryで一言講評する。

## 出力JSON schema
`LayoutActionPlan`。改善するタイトルごとに`set_text`を1件作り、元の`slide`と`shape_id`を入力JSONから正確にコピーする。`reason`に改善の狙いを書く。

## 禁止事項
既存Shape・Slideの削除。本文の書き換え(タイトルのみ対象)。入力にない事実・数値の追加。shape_idの推測。承認前の自動置換(提案のみ)。

## 例
`{"skill":"action_titles","target":"@all","summary":"5枚中3枚のタイトルを結論型へ。全体の流れは問い→証拠→結論で概ね良好","issues":[],"actions":[{"type":"set_text","slide":3,"shape_id":2,"text":"CLV3欠損で花芽の分裂組織が肥大する","reason":"話題ラベル『結果』を発見の主張に変更"}]}`

## Context Policy
- include_geometry: false
- include_notes: false
- include_deck_style: false
- include_recent_chat: true
- include_slide_images: false
- recent_chat_messages: 4
