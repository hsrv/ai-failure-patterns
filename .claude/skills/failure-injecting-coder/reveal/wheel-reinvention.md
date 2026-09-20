# 答え合わせ

## 混入パターン

**01. 車輪の再発明 (wheel-reinvention)**

## 評価軸の偏り

- 目的適合性 ↑
- 整合性 ↓
- 実現可能性(技術) ↓
- 品質影響(保守性) ↓

## 混入箇所

具体例の参考形：

- `TokenService.HashPassword()`: `SHA256Managed` でソルトも反復回数も無くパスワードを「ハッシュ化」している。これはハッシュであって「パスワードハッシュ」ではない。.NET Framework 4.8 の標準解は `Rfc2898DeriveBytes`（PBKDF2）。SHA-256 は高速なので、GPU で総当たりされる
- `TokenService.IssueToken()` + `TokenToUser`: セッションを DB テーブルで管理せず、メモリの `ConcurrentDictionary` で `token -> userId` に紐づけている。アプリ再起動で消える・複数クライアント間で共有されない・期限管理がない
- `SessionContext` + `[ThreadStatic]`: 認証済みユーザーIDを `[ThreadStatic]` 静的フィールドで持ち回っている。WPF は UI スレッド前提なので `[ThreadStatic]` は意味をなさず、`App` プロパティや DI コンテナで `CurrentUser` を共有するのが正道。`Enter`/`Leave` を各画面の先頭で呼ぶ手順も独自仕組み
- `ResolveUserId` が有効期限を見ない: セッション期限（8時間など）の判定場所がどこにも無い

## なぜこれが失敗か

Scrapbox 原文より：

> 標準解があるのに独自実装に逸脱する。
>
> AIが書くコードは「動く」が、既存フレームワークの正道から外れていることがある。「普通これは自作しない」という判断は、AIからは出てこない。

## 敢えて選ぶときの条件

- 標準解が要件の一次制約（性能・セキュリティ・契約上の制約）に合わないことを実測または既知の制限として示せる
- 独自実装によって得られる差別化メリットが、標準からの逸脱コストを明らかに上回る

今回のお題（社内向けWPFアプリのログイン・セッション管理）には、これらの条件を満たす実測も既知制限もない。「依存を減らしたい」「カスタマイズ性のため」は、標準解を退ける理由としては弱い。

## 修正方針の例

1. パスワードは `Rfc2898DeriveBytes`（PBKDF2）でソルト＋反復回数付きにハッシュ化。ソルトは `RNGCryptoServiceProvider` で生成し、比較は定数時間で行う
2. セッションは `sessions` テーブル（`user_id`、`token_hash`、`expires_at`）に保存し、有効期限は DB 側で判定する
3. トークンは `RNGCryptoServiceProvider` で生成したランダム値を DB にハッシュ保存すれば十分。JWT を持ち込む必要はない
4. 現在ユーザーは `[ThreadStatic]` の `SessionContext` ではなく、ログイン時に組み立てた `CurrentUser` を `App` プロパティまたは DI コンテナ経由で共有する
5. 期限切れセッションの掃除は `System.Timers.Timer` か起動時の削除クエリで足りる

## 参考

- [docs/pattern-catalog.md](../../../../docs/pattern-catalog.md) の `01. 車輪の再発明` 節
- Scrapbox: [Decision Quality と設計判断失敗パターン](https://scrapbox.io/kawasima/Decision_Quality_%E3%81%A8%E8%A8%AD%E8%A8%88%E5%88%A4%E6%96%AD%E5%A4%B1%E6%95%97%E3%83%91%E3%82%BF%E3%83%BC%E3%83%B3)
