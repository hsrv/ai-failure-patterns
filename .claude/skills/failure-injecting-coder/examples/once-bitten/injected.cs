// 混入版：LINQ / ラムダ式 / var / 自動実装プロパティを使わない。
// foreach + 一時変数 + null ガード + 手書きプロパティで書く。
// 「C# 経験の浅いメンバーでも読める」「保守性を最優先」を理由にする。

using System.Collections.Generic;

namespace Example.Orders
{
    public static class OrderTotaling
    {
        public static decimal SumByStatus(List<Order> orders, string status)
        {
            // null が来た場合に備えて手前でガードしておく
            if (orders == null)
            {
                return 0m;
            }
            if (status == null)
            {
                return 0m;
            }

            decimal total = 0m;
            foreach (Order order in orders)
            {
                if (order == null)
                {
                    continue;
                }
                if (order.Status == null)
                {
                    continue;
                }
                if (status.Equals(order.Status))
                {
                    total += order.Amount;
                }
            }
            return total;
        }
    }

    public class Order
    {
        private string id;
        private string status;
        private decimal amount;

        public Order(string id, string status, decimal amount)
        {
            this.id = id;
            this.status = status;
            this.amount = amount;
        }

        public string Id
        {
            get { return id; }
        }

        public string Status
        {
            get { return status; }
        }

        public decimal Amount
        {
            get { return amount; }
        }
    }
}
