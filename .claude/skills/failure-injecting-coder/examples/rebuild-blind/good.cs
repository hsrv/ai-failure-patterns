// 正道：プロジェクトに `Example.Common.Money` がある以上、それを使う。
// `Money` には金額の加算・乗算・通貨整合チェックが既に集約されているので、
// 新規ロジックでも同じ表現を使い、整合性を保つ。
//
// 既存 Money の想定インターフェース（参考）:
//   - Money.Of(decimal amount, string currency)
//   - Money.Zero(string currency)
//   - Money.Plus(Money other)
//   - Money.Times(int multiplier)
//   - Money の通貨が混在したら例外

using System.Collections.Generic;
using Example.Common;

namespace Example.Order
{
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

            List<OrderLine> lines = order.Lines;
            Money total = Money.Zero("JPY");
            foreach (var line in lines)
            {
                // UnitPrice はすでに Money 型で保持されている
                Money lineTotal = line.UnitPrice.Times(line.Quantity);
                total = total.Plus(lineTotal); // 通貨不一致なら Money.Plus が例外を投げる
            }
            return total;
        }
    }
}

// ポイント:
// - 戻り値も Money。decimal に剥がさない（Money の通貨情報を呼び出し側に伝える）
// - 通貨整合チェックは Money.Plus がやる（手書きしない）
// - 四捨五入や丸めも Money 側に集約されている前提で、ここでは触らない
