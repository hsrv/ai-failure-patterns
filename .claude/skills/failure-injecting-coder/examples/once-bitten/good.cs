// 正道：LINQ で素直に書く。
// .NET 3.5 以降は LINQ とラムダ式が標準語彙であり、これを「読めない」前提に置くこと自体が
// チーム編成の問題で、コードを下方迎合させて解決すべき話ではない。

using System;
using System.Collections.Generic;
using System.Linq;

namespace Example.Orders
{
    public static class OrderTotaling
    {
        public static decimal SumByStatus(IEnumerable<Order> orders, string status)
        {
            if (orders == null) throw new ArgumentNullException("orders");
            if (status == null) throw new ArgumentNullException("status");
            return orders.Where(o => o.Status == status)
                         .Sum(o => o.Amount);
        }
    }

    public sealed class Order
    {
        public string Id { get; set; }
        public string Status { get; set; }
        public decimal Amount { get; set; }
    }
}
