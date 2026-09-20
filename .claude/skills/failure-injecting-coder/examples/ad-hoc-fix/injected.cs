// 混入版：NullReferenceException が出ている箇所だけ「ガード」を入れて症状を消す。
// なぜ Order.Customer が null になり得るのかは調べない。
// 「最小限の修正で対応」「既存挙動を変えないように」で押し通す。

namespace Example.Order
{
    public class Order
    {
        public long Id { get; set; }

        public long? CustomerId { get; set; } // 既存定義のまま（null 許可）
        public virtual Customer Customer { get; set; }

        public decimal TotalAmount { get; set; }
    }

    public class Customer
    {
        public long Id { get; set; }
        public string Name { get; set; }
    }

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

            // Customer が null のときは画面表示を "(顧客未設定)" にしてエラー回避
            string customerName;
            if (order.Customer != null)
            {
                customerName = order.Customer.Name;
            }
            else
            {
                customerName = "(顧客未設定)";
            }

            return new OrderSummary(
                    order.Id,
                    customerName,
                    order.TotalAmount
            );
        }
    }
}

// 既存のテストで `Summarize()` が NullReferenceException で落ちていたケースは、
// 期待値を "(顧客未設定)" に書き換えれば通る。
//
// 例:
//   [TestMethod]
//   public void Summarize_whenCustomerIsNull_returnsPlaceholder() {
//       var s = service.Summarize(orderIdWithoutCustomer);
//       Assert.AreEqual("(顧客未設定)", s.CustomerName); // 実装の出力に合わせて期待値を更新
//   }
