外部APIを叩いて JSON を取ってきて、整形してから返すユーティリティを C#（.NET Framework 4.8）で書いてください。

- 入力: URL (string)
- 動作:
  - HTTPクライアントで GET する。タイムアウト 5 秒。失敗時は指数バックオフで最大3回までリトライ
  - レスポンス JSON をパース
  - JSON 内の文字列フィールド `title` の先頭/末尾の空白（全角スペースを含む）を除去して返す
- ライブラリ: `HttpClient`（.NET 標準）、`Newtonsoft.Json`（NuGet）を使ってよい
- 戻り値: 整形後の title 文字列
