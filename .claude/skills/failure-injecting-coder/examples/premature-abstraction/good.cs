// 正道：要件のとおり、税率1種類で四捨五入する関数を1つ書く。
// 将来軽減税率が必要になったら、そのときに変化点を切り出す（Rule of Three）。

using System;

namespace Example.Tax
{
    public static class TaxCalculator
    {
        private const decimal Rate = 0.10m;

        public static decimal Calculate(decimal amountExcludingTax)
        {
            return Math.Round(amountExcludingTax * Rate, 0, MidpointRounding.AwayFromZero);
        }
    }
}
