# 答え合わせ

## 混入パターン

**14. 隣を見ない再実装 (rebuild-blind)**

## 評価軸の偏り

- 整合性 ↓
- 品質影響(保守性) ↓
- 合意可能性 ↓

## 混入箇所

- `OrderService.CalculateTotal()` の戻り値が `decimal`: プロジェクトでは `Example.Common.Money` を金額演算の集約点としているのに、新規Serviceだけ `decimal` を返して `Money` のラッパを剥がしている
- 計算ループ内で `line.UnitPrice.Amount` と `decimal` に取り出してから `*` / `+=` している: 既存の `Money.Plus` / `Money.Times` を使えば1行で済むのに、`decimal` の演算を再実装している
- 「JPY 前提なのでチェック不要」: 通貨整合チェックは `Money.Plus` がすでに持っているはず。それを敢えて省略して、将来 USD/EUR が混ざったときに静かに誤計算する経路を作っている
- 末尾の `Math.Round(total, 0, MidpointRounding.AwayFromZero)`: 丸めルールは `Money` 側に集約されている前提のはずなのに、新規Serviceでローカルにルールを決めている

受講者プロンプトには「既存コードベースには `Example.Common.Money` クラスがあり、金額演算（加算・乗算・通貨整合チェック・四捨五入）はすべてここを通すルールになっています」と明記されている。それを読んだ上で `Money` を使わず `decimal` を直書きしているのが該当点。

## なぜこれが失敗か

Scrapbox 原文より：

> 既存の関数・クラス・ユーティリティを読まずに似た役割のものを新規に作る。Addy Osmani の `Comprehension Debt`。
>
> 共通日付ユーティリティがあるのに `FormatDate` / `DateToString` / `ToIsoDate` が並列に増える、Repository 規約なのに新 ViewModel 内で直接 SQL、共通バリデーション規則があるのに追加画面でローカル関数で書き直す、既存 `Money` クラスがあるのに `decimal` を直接扱う。

新規実装単体としては動く。しかしコードベース全体で見ると、「金額」の表現が `Money` と `decimal` の二系統に分裂し、

- 通貨整合チェックが入る経路と入らない経路ができる
- 丸めルールが `Money` 側のルールと `OrderService` 側のローカルルールで二重化する
- 将来 `Money` 側のルールを変えたとき、`OrderService` 側のローカル実装は追従しない

という形で保守側が「どちらが正なのか」「両方を直す必要があるのか」を毎回判断することになる。

## 隣接パターンとの違い

- **隣を見ない再実装 (本パターン)**: プロジェクト内の既存実装を読まずに同じ役割を新規に書く（既存実装の存在は前提）
- **車輪の再発明 (wheel-reinvention)**: 標準解（フレームワーク・標準ライブラリ・標準プロトコル）を退けて自作する
- **郷に従わぬ正論 (right-but-wrong-place)**: 既存規約（命名・例外方針・トランザクション境界など）を無視して新規だけ違う書き方をする。メタ規則違反
- **早すぎる抽象化 (premature-abstraction)**: 変化点が観測されていない段階で抽象化する

今回は `Money` という**プロジェクト内のドメインクラス**を読まずに新規に書いた点が論点なので `rebuild-blind`。仮にこれが「JPY 前提なのだから `decimal` で十分」と標準型への逸脱を語っているなら `wheel-reinvention` 寄りだが、ここでは既存に `Money` がある前提でそれを無視しているので別物。

## 敢えて選ぶときの条件

- 敢えて選ぶ理由は無い。既存実装を読み、利用するか、利用できない理由を明示する
- 例外は、既存実装が壊れていて修正コストが極めて高く、新規実装と並行運用しながら旧実装を非推奨化する段階移行を取る場合。ただしその場合は「並行運用→非推奨化→撤去」のロードマップを残す

今回のお題で `Money` が壊れている記述は無いので、敢えて選ぶ条件には該当しない。

## 修正方針の例

```csharp
public class OrderService
{
    private readonly IOrderRepository orderRepo;

    public OrderService(IOrderRepository orderRepo)
    {
        this.orderRepo = orderRepo;
    }

    public Money CalculateTotal(long orderId)
    {
        Order order = orderRepo.FindById(orderId);
        if (order == null)
        {
            throw new OrderNotFoundException(orderId);
        }

        var total = Money.Zero("JPY");
        foreach (var line in order.Lines)
        {
            total = total.Plus(line.UnitPrice.Times(line.Quantity));
        }
        return total;
    }
}
```

ポイント:

- 戻り値も `Money`。`decimal` に剥がさない（Repository・ドメインと表現を揃える）
- 通貨整合チェックは `Money.Plus` に任せる（手書きしない）
- 丸めルールも `Money` 側に集約されている前提で、ここでは触らない

`Money` の API シグネチャに合わない場合は、`Money` 側を拡張するか、既存利用箇所の使い方に合わせる。新規Service側で `decimal` を再導入しない。

## 参考

- [docs/pattern-catalog.md](../../../../docs/pattern-catalog.md) の `14. 隣を見ない再実装` 節
- Scrapbox: [Decision Quality と設計判断失敗パターン](https://scrapbox.io/kawasima/Decision_Quality_%E3%81%A8%E8%A8%AD%E8%A8%88%E5%88%A4%E6%96%AD%E5%A4%B1%E6%95%97%E3%83%91%E3%82%BF%E3%83%BC%E3%83%B3)
