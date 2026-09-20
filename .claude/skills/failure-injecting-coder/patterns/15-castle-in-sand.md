---
slug: castle-in-sand
number: 15
name_ja: 砂上の依存
name_en: Castle in Sand
---

## 評価軸の偏り

- 実現可能性(技術) ↓
- リスク・不確実性 ↓
- 制約適合性 ↓

## 支配軸の取り違え

本来の支配軸は `実現可能性(技術)`。存在しないライブラリ・関数・引数・メソッドを呼び出す。`package hallucination` として研究領域でも観測されている現象。エージェントの訓練データに含まれていた古い情報、もしくは類似ライブラリとの混同が原因になる。コードは「動くように見えるが、実際には動かない」。

## 混入の指針

- 実在するライブラリ・API（Newtonsoft.Json、log4net、Polly、`HttpClient`、`DataContractJsonSerializer` など）を選び、**存在しないメソッド・存在しない引数・存在しないオーバーロード**を呼ぶ
- メソッド名は「いかにもありそう」な命名にする（`StringExtensions.FluentTrim(s, culture)`、`HttpClientBuilder.WithRetryPolicy(...)`、`JObject.ParseWithSchema(...)`）
- 似た別ライブラリのAPIを混ぜる（`string.Trim` と独自拡張 `StringExtensions` の混同、log4net と NLog のメソッド名の混同、Polly の `Policy` と BCL の `RetryPolicy` の混同）
- 削除された旧APIを呼ぶ（過去のバージョンには存在したが現行では削除されたもの）
- using 句も同様に「ありそうな」名前空間を書く（`System.Net.Http.RetryHelpers` のような実在しない名前空間）
- コードは**コンパイル通らない or 実行時に MissingMethodException / TypeLoadException が出る**状態にする
- 外向きの説明では「`StringExtensions` の `FluentTrim` を使って空白整形を簡潔に」「`HttpClient` の組み込みリトライポリシーで指数バックオフを設定」のように、**当然動く前提**で説明する
- narration では実在性の話題に触れない。「設定しました」「使っています」と書く

## 混入してよい局所

- `System.String` に対して `s.FluentTrim(culture)` のような実在しない拡張メソッドを呼ぶ
- `HttpClient` に対して `client.WithRetryPolicy(RetryPolicy.ExponentialBackoff(3, TimeSpan.FromSeconds(1)))` のような実在しないチェーンを書く
- Newtonsoft.Json の `JObject` に対して `ParseWithSchema(...)`、`ToJsonWithSchema(...)` のような実在しないメソッド
- `DateTime` に対して `DateTime.ParseFlexible(s)` のような実在しない static メソッド
- log4net の `ILog` に対して `log.WithContext("orderId", id).Warn(...)` のような実在しないチェーン
- using 句で `System.Net.Http.RetryHelpers` のような実在しない名前空間を参照

## 混入してはいけない局所（隣接パターンと混線する）

- 標準解を退けて自作する → `wheel-reinvention`（車輪の再発明）の領分。 こちらは「ライブラリを使う気はあるが API を幻視している」点が違う
- 既存のプロジェクト内ユーティリティを読まずに似た役割を作る → `rebuild-blind`（隣を見ない再実装）の領分
- 仕様が無いまま実装する → `jumping-the-gun`（見切り発車）の領分

## 敢えて選ぶときの条件

- 敢えて選ぶ理由は無い。新規ライブラリ・新規APIの導入時は、一次資料（公式ドキュメント・ソース）での存在確認と実行確認をセットで行う

## Scrapbox 該当節（reveal で開示するための原文引用）

> 存在しないライブラリ・関数・引数シグネチャ・メソッドを呼び出すコードを生成する型。エージェントの訓練データに含まれていた古い情報、もしくは類似ライブラリとの混同が原因になる。`package hallucination` として研究領域でも観測されている現象。
>
> 実現可能性の確認をエージェントに任せきりにすると、`動くように見えるが動かない` コードが量産される。ドキュメント参照（context7のようなツール）と実行確認の運用が前提になる。
