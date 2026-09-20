# 答え合わせ

## 混入パターン

**05. 場当たり対応 (ad-hoc-fix)**

## 評価軸の偏り

- 時間効果(短期) ↑
- 整合性 ↓
- 品質影響 ↓
- 時間効果(長期) ↓

## 混入箇所

- `OrderSummaryService.Summarize()` の `if (order.Customer != null)` ガード: NullReferenceException が出ている箇所だけ null チェックを足して症状を消している。「なぜ `Order.Customer` が null になり得るのか」を調査した跡が無い
- `"(顧客未設定)"` のプレースホルダ文字列: 業務上「顧客未設定の注文」がそもそも存在してよいのかが未確定のまま、表示文言で誤魔化している
- injected.cs の末尾コメント「期待値を `"(顧客未設定)"` に書き換えれば通る」: 失敗するテストの期待値を実装の出力に合わせて書き換える典型例
- `Order` クラスの定義をそのまま据え置き: `CustomerId` が `long?` のままで、DB の不変条件（注文には顧客が必ず紐づく）を取り戻していない

ここで本来取るべき行動は次のいずれか:

- DB に `CustomerId` が null の Order が何件あるかを調査する
- 業務上「顧客未設定の注文」が許されるのかを確認する
- 許されないなら、データを直して NOT NULL 制約を入れる
- 許されるなら、`Customer == null` をドメイン上の意味として明示する（例: `HasCustomer` プロパティ）

どれを選ぶにせよ、`if (x != null)` を1箇所に足して終わりではない。

## なぜこれが失敗か

Scrapbox 原文より：

> 短期的にコードが通るので判断の手前で止まらない。
>
> エラーを握りつぶして画面だけ進める、責務分離を無視してコードビハインドにロジック追加、ドメインルールを画面側だけでチェック、その場の NullReferenceException だけ避ける `if (x != null)` 追加、テストが落ちるのでテスト期待値を実装に合わせる──いずれも「いま通すこと」だけを支配軸にした判断保留である。

NullReferenceException は症状であって原因ではない。`Customer` が null である事実は、データモデルかユースケースかどこかに穴があることのサイン。`if (x != null)` で隠すと、同じ穴から出る別の症状（請求書出力・分析レポート・会員別集計）でまた NullReferenceException か、最悪は誤集計として表面化する。

## 隣接パターンとの違い

- **場当たり対応 (本パターン)**: 根本原因に踏み込まず、症状の出た場所だけを直す（判断保留の問題）
- **越境実装 (boundary-violation)**: 制約はあるが、書く場所が間違っている（責務の置き場所の問題）
- **車輪の再発明 (wheel-reinvention)**: 標準解を退けて独自実装する
- **隣を見ない再実装 (rebuild-blind)**: 既存ユーティリティを読まずに似た役割を作る

今回は「customer が null のときの扱いをドメインで決めるべきところを、View 用の文字列で誤魔化す」点が `boundary-violation` っぽく読める要素もあるが、本筋は「なぜ null になるかを調べていない」という判断保留なので `ad-hoc-fix`。

## 敢えて選ぶときの条件

- 障害対応の「止血」フェーズ。本番が落ちていて、根本原因の特定よりも復旧を優先する局面
- ただし、止血の直後に「根本原因の調査・修正」のチケットを切ること、回避策のコードに `TODO` と起票番号を残すことが条件
- 止血が恒久対応として残り続けると、結局は場当たり対応に化ける

今回のお題は「リリース直前で今日中に直したい」と書かれているが、これは止血のサインにもなり得る。止血を選ぶなら最低限、以下が必要:

- `TODO` コメントに「customer が null の Order がそもそも存在しないはず、根本調査必要」と起票番号を書く
- 注文作成側のバリデーション追加と DB マイグレーションを別チケットで切る
- 既存データに対する調査を計画する

## 修正方針の例

調査して原因が「(A) Order 作成時に CustomerId が必須でなく、Customer 未紐づけの Order が DB に存在する」だった場合：

```csharp
public class Order
{
    public long Id { get; set; }

    // customer_id は NOT NULL。Customer は必須の関連
    public long CustomerId { get; set; }
    public virtual Customer Customer { get; set; }

    public decimal TotalAmount { get; set; }
}

// Fluent API 側:
// modelBuilder.Entity<Order>()
//     .HasRequired(o => o.Customer)
//     .WithMany()
//     .HasForeignKey(o => o.CustomerId);
```

加えて：

1. 既存データで `customer_id` が null の注文を調査し、業務上どう扱うかを決める
2. DB に NOT NULL 制約を追加するマイグレーション
3. `OrderCreateService` 側で `CustomerId` 必須バリデーションを入れる
4. テスト: Customer なしで Order を作ろうとするとエラーになることを確認

`Summarize()` には `if (x != null)` を入れない。不変条件を取り戻せば、NullReferenceException は構造的に起き得なくなる。

## 参考

- [docs/pattern-catalog.md](../../../../docs/pattern-catalog.md) の `05. 場当たり対応` 節
- Scrapbox: [Decision Quality と設計判断失敗パターン](https://scrapbox.io/kawasima/Decision_Quality_%E3%81%A8%E8%A8%AD%E8%A8%88%E5%88%A4%E6%96%AD%E5%A4%B1%E6%95%97%E3%83%91%E3%82%BF%E3%83%BC%E3%83%B3)
