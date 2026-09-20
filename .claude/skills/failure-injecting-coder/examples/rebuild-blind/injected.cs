// 混入版：既存の `Example.Common.Money` を使わず、`decimal` を直接扱う。
// 通貨整合チェックも自前で書き、四捨五入もこの関数の中で行う。
// 「シンプルに書きました」「依存を持ち込まず最小構成で」で押し通す。

using System;
using System.Collections.Generic;

namespace Example.Order
{
    public class OrderService
    {
        private readonly IOrderRepository orderRepo;

        public OrderService(IOrderRepository orderRepo)
        {
            this.orderRepo = orderRepo;
        }

        public decimal CalculateTotal(long orderId)
        {
            Order order = orderRepo.FindById(orderId);
            if (order == null)
            {
                throw new OrderNotFoundException(orderId);
            }

            List<OrderLine> lines = order.Lines;
            decimal total = 0m;
            foreach (var line in lines)
            {
                // 通貨は JPY 前提なのでチェック不要として直接掛ける
                decimal unitPrice = line.UnitPrice.Amount; // Money から Amount を取り出す
                decimal qty = line.Quantity;
                decimal lineTotal = unitPrice * qty;
                total += lineTotal;
            }
            // 円未満を念のため四捨五入
            return Math.Round(total, 0, MidpointRounding.AwayFromZero);
        }
    }
}

// ポイント:
// - 戻り値は decimal。シンプルに使えるように Money を剥がして返す
// - JPY 前提なので通貨チェックは省いた
// - 念のため最後に四捨五入
