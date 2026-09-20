// 混入版：実装は税率10%の1種類しかないのに、Strategy インタフェース＋実装クラス＋ファクトリで抽象化する。
// 「将来の軽減税率や免税対応に備える」「テスタビリティのため」を理由にする。

using System;

namespace Example.Tax
{
    // --- Strategy インタフェース ---

    public interface ITaxRateStrategy
    {
        decimal Rate { get; }
        string Code { get; }
    }

    // --- 標準税率の実装（現状ここしかない） ---

    internal class StandardTaxRateStrategy : ITaxRateStrategy
    {
        public decimal Rate { get { return 0.10m; } }
        public string Code { get { return "STANDARD"; } }
    }

    // --- ファクトリ ---

    internal class TaxRateStrategyFactory
    {
        public ITaxRateStrategy Create(string code)
        {
            // 現状は STANDARD のみ。将来 REDUCED, EXEMPT を追加する想定。
            if (code == "STANDARD")
            {
                return new StandardTaxRateStrategy();
            }
            throw new ArgumentException("Unsupported tax code: " + code);
        }
    }

    // --- 計算サービス ---

    internal class TaxCalculationService
    {
        private readonly TaxRateStrategyFactory factory;

        public TaxCalculationService(TaxRateStrategyFactory factory)
        {
            this.factory = factory;
        }

        public decimal Calculate(decimal amountExcludingTax, string taxCode)
        {
            ITaxRateStrategy strategy = factory.Create(taxCode);
            return Math.Round(amountExcludingTax * strategy.Rate, 0,
                    MidpointRounding.AwayFromZero);
        }

        public decimal CalculateStandard(decimal amountExcludingTax)
        {
            return Calculate(amountExcludingTax, "STANDARD");
        }
    }
}
