# 答え合わせ

## 混入パターン

**12. 越境実装 (boundary-violation)**

## 評価軸の偏り

- 整合性 ↓
- 品質影響(保守性) ↓
- リスク・不確実性 ↓

## 混入箇所

- `OrderDetailWindow.UpdateButton_Click()`: 状態遷移マトリクス（PENDING→CONFIRMED/CANCELLED、CONFIRMED→SHIPPED/CANCELLED、SHIPPED→不可、CANCELLED→不可）が `if`/`else if` の文字列比較で書かれている。本来は `OrderStatus` enum か `Order` エンティティが「自分が次の状態に行けるか」を知っているべき業務不変条件
- 高額注文の判定（`order.TotalAmount > 1000000m` で SHIPPED は admin のみ）も同じイベントハンドラの中に書かれている。取込処理や別画面から状態遷移が呼ばれたら、この判定には到達しない
- 権限の判定が `App.Current.Properties["UserRole"]` の文字列比較。認証済みユーザー情報の受け渡し経路の上に独自経路で乗っている
- `Order` クラスに EF6 の `[Column]`（永続化境界）と `INotifyPropertyChanged` + `[NotMapped]` の `DisplayStatus`/`InternalMemo`（画面境界）が同居。同じクラスが2つの境界を兼ねている
- `Status` が `string`。enum 化していないので、コード上で「Pending/Confirmed/Shipped/Cancelled の4種類しかない」ことを表現できていない

## なぜこれが失敗か

Scrapbox 原文より：

> 注文の上限金額判定をドメインではなくコードビハインドに書く。「管理者には削除ボタンを表示する」をXAMLのConverterやVisibilityバインドの中で直接判定する。集計画面の表示順を確定するため、SQLに `ORDER BY CASE WHEN status = 'urgent' THEN 0 ELSE 1 END` のような画面都合の式を埋める。
>
> AIは「どこに書くべきか」より「どこに書けば今動くか」に寄ることがある。

外向きには「一箇所で見やすい」と言える。だがその「一箇所」が本来の責務位置でなければ、他の入口（取込処理、管理コンソール、別画面）から同じドメインに触れたときに業務ルールを通らない経路ができる。

## 隣接パターンとの違い

- **越境実装 (本パターン)**: 業務ルールがある場所が間違っている（責務の置き場所の問題）
- **場当たり対応 (ad-hoc-fix)**: 短期的に通すために判断保留する（時間の問題）
- **郷に従わぬ正論 (right-but-wrong-place)**: 既存規約を知った上で新規だけ違う書き方をする（規約適合の問題）
- **制約の後付け (bolt-on-constraints)**: 制約をデータモデルに最初から織り込まずに後から足す（順序の問題）

特に `ad-hoc-fix` との見分けがポイント。場当たり対応は「いま動かすために妥協する」、越境実装は「動かすために違う場所に書く」。今回は仕様としての業務ルールがコードビハインドに流れ込んでいるので越境実装。

## 敢えて選ぶときの条件

- 既存構造側にバグがあり、責務本体を直すコストが当面引き受けられないとき。暫定として越境を許す代わりに差し戻し条件と恒久対応チケットを残す
- フレームワーク・ライブラリの制約で、本来の責務位置に書けない実装上の理由があるとき。理由をコメントとして残す

## 修正方針の例

1. `OrderStatus` を enum 化し、遷移マトリクスを `CanTransitionTo` 拡張メソッド（またはドメイン側の静的クラス）に持たせる
2. `Order` に `ChangeStatus(OrderStatus next)` と `RequiresAdminToShip()` を持たせ、業務不変条件をドメインで保証する
3. 高額注文 SHIPPED の権限チェックは Service の入口で、操作者 `User` を引数で受けて判定する
4. コードビハインドは入出力（コマンド呼び出し、メッセージ表示、操作者の引き渡し）だけに集中する
5. `Order` エンティティから `INotifyPropertyChanged` と `[NotMapped]` 表示プロパティを剥がし、画面用には別の `OrderListItemViewModel` を用意する

```csharp
public enum OrderStatus { Pending, Confirmed, Shipped, Cancelled }

public static class OrderStatusTransitions
{
    public static bool CanTransitionTo(this OrderStatus current, OrderStatus next)
    {
        switch (current)
        {
            case OrderStatus.Pending:
                return next == OrderStatus.Confirmed || next == OrderStatus.Cancelled;
            case OrderStatus.Confirmed:
                return next == OrderStatus.Shipped || next == OrderStatus.Cancelled;
            default:
                return false;
        }
    }
}
```

ポイントは「業務ルールに到達する経路がドメイン側に1本」であること。画面・取込処理・管理コンソールのどこから呼んでも同じ判定を通る。

## 参考

- [docs/pattern-catalog.md](../../../../docs/pattern-catalog.md) の `12. 越境実装` 節
- Scrapbox: [Decision Quality と設計判断失敗パターン](https://scrapbox.io/kawasima/Decision_Quality_%E3%81%A8%E8%A8%AD%E8%A8%88%E5%88%A4%E6%96%AD%E5%A4%B1%E6%95%97%E3%83%91%E3%82%BF%E3%83%BC%E3%83%B3)
