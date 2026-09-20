# 答え合わせ

## 混入パターン

**15. 砂上の依存 (castle-in-sand)**

## 評価軸の偏り

- 実現可能性(技術) ↓
- リスク・不確実性 ↓
- 制約適合性 ↓

## 混入箇所

このコードはコンパイルが通らない。あるいは仮に通っても実行時に `MissingMethodException` / `TypeLoadException` で落ちる。

- `new HttpClientBuilder().WithTimeout(...).WithRetryPolicy(...).Build()`: .NET Framework 4.8 に `HttpClientBuilder` は存在しない（`Microsoft.Extensions.Http` の `IHttpClientFactory` 系は .NET Core 系の話）。実在するのは `new HttpClient { Timeout = ... }`
- `RetryPolicy.ExponentialBackoff(3, TimeSpan.FromSeconds(1))`: BCL に `RetryPolicy` という型は無い。Polly なら `Policy.Handle<>().WaitAndRetryAsync` が実在するが、このシグネチャではない
- `RetryHelper.WithRetry(3, async () => ...)`: `System.Net.Http` に `RetryHelper` というクラスは存在しない。BCL にリトライ系ユーティリティは無い
- `JObject.ParseWithSchema(body, "default")`: Newtonsoft.Json の `JObject` にこのメソッドは存在しない。実在するのは `Parse` / `Load`。"スキーマ" を指定して読む API は別物（JSON Schema 検証は別ライブラリ）
- `StringExtensions.FluentTrim(raw, culture)`: BCL に `StringExtensions` クラスも `FluentTrim` も存在しない。実在するのは `Trim` / `TrimStart` / `TrimEnd` / 引数を取る `Trim(char[])`

外向きの説明は「指数バックオフリトライ」「ロケール対応空白除去」のような、それ自体は理に適った要件を、いかにもありそうな API 名で実現したことにしてしまっている。

## なぜこれが失敗か

Scrapbox 原文より：

> 存在しないライブラリ・関数・引数シグネチャ・メソッドを呼び出すコードを生成する型。エージェントの訓練データに含まれていた古い情報、もしくは類似ライブラリとの混同が原因になる。`package hallucination` として研究領域でも観測されている現象。
>
> 実現可能性の確認をエージェントに任せきりにすると、`動くように見えるが動かない` コードが量産される。ドキュメント参照（context7のようなツール）と実行確認の運用が前提になる。

コードを読むと API 名は「いかにもありそう」で、シグネチャも自然。レビュアーが当該ライブラリの全APIを把握していないと、「知らないだけかも」と思って通してしまう。コンパイル時・テスト時に必ず落ちるはずなので、CI を回せば検出できる。回さないと PR がそのまま通ってしまう。

## 隣接パターンとの違い

- **砂上の依存 (本パターン)**: ライブラリを使う気はあるが、API を**幻視**している（存在しない関数を呼ぶ）
- **車輪の再発明 (wheel-reinvention)**: 標準解（ライブラリ）を退けて、自前で実装する
- **隣を見ない再実装 (rebuild-blind)**: 既存のプロジェクト内ユーティリティを読まずに似たものを作る

「リトライ機構が欲しい」場面で、自前 `for` ループで書くなら `wheel-reinvention`、`HttpClientBuilder.WithRetryPolicy(...)` を幻視するなら `castle-in-sand`、既存の `Example.Common.RetryHelper` を読まずに別の `Retrier` クラスを作るなら `rebuild-blind`。同じ機能要件でも、どこを取り違えたかで別パターンになる。

## 敢えて選ぶときの条件

- 敢えて選ぶ理由は無い。新規ライブラリ・新規APIの導入時は、一次資料（公式ドキュメント・ソース）での存在確認と実行確認をセットで行う

## 修正方針の例

1. リトライは Polly（`Policy.Handle<HttpRequestException>().WaitAndRetryAsync`）を使うか、自前の `for` ループで書く
2. `HttpClient` は `new HttpClient { Timeout = ... }` で直接作る（.NET 4.8 にビルダーは無い）
3. JSON パースは `JObject.Parse(string)` を使う
4. 空白除去は `raw.Trim()` か、`raw.Trim(new[] { ' ', '\t', '\r', '\n', '　' })` で全角スペースを明示的に含める

```csharp
public async Task<string> FetchTitleAsync(string url)
{
    for (int attempt = 1; attempt <= 3; attempt++)
    {
        try
        {
            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            string body = await response.Content.ReadAsStringAsync();
            var root = JObject.Parse(body);
            string raw = root.Value<string>("title") ?? "";
            return raw.Trim(new[] { ' ', '\t', '\r', '\n', '　' });
        }
        catch (HttpRequestException)
        {
            if (attempt == 3) throw;
            await Task.Delay(TimeSpan.FromSeconds(1 << (attempt - 1)));
        }
    }
    throw new InvalidOperationException("unreachable");
}
```

運用としては context7 等のドキュメント参照ツールを介してAPI存在確認をする、CIでコンパイルを必ず通す、これでだいぶ減る。

## 参考

- [docs/pattern-catalog.md](../../../../docs/pattern-catalog.md) の `15. 砂上の依存` 節
- Scrapbox: [Decision Quality と設計判断失敗パターン](https://scrapbox.io/kawasima/Decision_Quality_%E3%81%A8%E8%A8%AD%E8%A8%88%E5%88%A4%E6%96%AD%E5%A4%B1%E6%95%97%E3%83%91%E3%82%BF%E3%83%BC%E3%83%B3)
