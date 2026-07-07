# fact_check

## 目的
検証可能な主張を抽出し、MCP検索の外部証拠と比較する。

## 入力
スライド文、ノート、MCP検索結果、Deck Context。

## 出力JSON schema
`FactCheckResult {target,summary,external_evidence_used,claims[]}`。claimは`slide,shape_id,claim,status,reason,evidence,suggested_slide_text`。

## 禁止事項
LLM単体で断定、根拠なしのsupported、曖昧情報の確定、引用不足の見逃し。

## 使えるAction
初回はなし。承認後のみset_text、add_citation_note。

## 評価基準
statusはsupported/needs_source/overstated/uncertain/contradicted/out_of_scopeのみ。証拠の出所を追跡可能にする。

## 判断手順
1. 意見や発表上の方針ではなく、外部資料で検証可能な主張だけを抽出する。
2. 遺伝子機能、因果、必須性、最初の報告、定量値、一般化された表現を優先する。
3. 検索前は全主張をneeds_sourceにする。
4. supportedは、取得した証拠が同じ対象・条件・方向性を直接支持するときだけ使う。
5. 一部条件だけ支持する、因果を相関から述べる、範囲を一般化する場合はoverstatedにする。
6. 証拠同士が競合するか解釈が定まらない場合はuncertainにする。
7. contradictedは明示的な反証がある場合だけ使う。
8. evidenceには検索結果中のタイトル、URL、論文識別子など追跡可能な文字列だけを書く。

## 例
`{"claims":[{"slide":2,"claim":"Xは収量を増やす","status":"needs_source","reason":"外部証拠未取得","evidence":[]}]}`

## Context Policy
- include_geometry: false
- include_notes: true
- include_deck_style: false
- include_recent_chat: true
- include_slide_images: false
- recent_chat_messages: 4
