既存の社内WPFアプリ（.NET Framework 4.8）で注文サマリ画面を開くと NullReferenceException が出ています。修正してください。

現象:
- 注文一覧画面で注文を作成したあと、注文サマリ画面を開くと
  `OrderSummaryService.Summarize()` 内で NullReferenceException が出る
- スタックトレース上は `order.Customer.Name` で落ちている
- 一部の注文だけで起きる（再現性はある）
- リリース直前なので、今日中に直したい

既存コードは以下のとおりです（抜粋）。

```csharp
public class OrderSummaryService
{
    private readonly IOrderRepository orderRepo;

    public OrderSummaryService(IOrderRepository orderRepo)
    {
        this.orderRepo = orderRepo;
    }

    public OrderSummary Summarize(long orderId)
    {
        Order order = orderRepo.FindById(orderId);
        return new OrderSummary(
                order.Id,
                order.Customer.Name,
                order.TotalAmount
        );
    }
}
```
