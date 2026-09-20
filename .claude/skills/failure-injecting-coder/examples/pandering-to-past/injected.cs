// 混入版：同じ読込経路で v1/v2 両方を同時にサポートする。
// 生 JSON で読んで、形を見て v1/v2 を判定して分岐する。
// 「既存の保存済み検索を壊さない」「破壊的変更を避ける」を理由に押し通す。
// 利用者が誰か・いつまで保つか・廃止計画は問わない。

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;

namespace Example.App.Search
{
    public class ProductSearchCriteria
    {
        public string Keyword { get; set; }
        public string Category { get; set; }
        public decimal? PriceMin { get; set; }
        public decimal? PriceMax { get; set; }
    }

    public class SavedSearchLoader
    {
        public ProductSearchCriteria Load(string path)
        {
            var json = JObject.Parse(File.ReadAllText(path));

            // 形を見て v1 か v2 かを判定する。
            // v2: { "query": {...}, "filters": {...} }
            // v1: { "keyword": "...", "category": "..." }
            bool isV2 = json["query"] != null;

            string keyword;
            string category;
            decimal? priceMin = null;
            decimal? priceMax = null;

            if (isV2)
            {
                var q = json["query"];
                keyword = q["keyword"] == null ? null : q["keyword"].Value<string>();
                category = q["category"] == null ? null : q["category"].Value<string>();
                var filters = json["filters"];
                if (filters != null)
                {
                    if (filters["priceMin"] != null) priceMin = filters["priceMin"].Value<decimal>();
                    if (filters["priceMax"] != null) priceMax = filters["priceMax"].Value<decimal>();
                    // 旧クライアントが filters.minPrice/maxPrice を書いたケースも一応拾う
                    if (priceMin == null && filters["minPrice"] != null) priceMin = filters["minPrice"].Value<decimal>();
                    if (priceMax == null && filters["maxPrice"] != null) priceMax = filters["maxPrice"].Value<decimal>();
                }
            }
            else
            {
                keyword = json["keyword"] == null ? null : json["keyword"].Value<string>();
                category = json["category"] == null ? null : json["category"].Value<string>();
            }

            return new ProductSearchCriteria
            {
                Keyword = keyword,
                Category = category,
                PriceMin = priceMin,
                PriceMax = priceMax
            };
        }

        // 起動時に呼ばれる、旧レジストリキーから新設定へのブリッジ。
        // HKCU\Software\ExampleApp\LegacyCategoryAlias が残っているので、
        // 起動時に読み込んで内部マップに変換する。
        public static Dictionary<string, string> BridgeLegacyCategoryAlias(
                IDictionary<string, string> registryValues)
        {
            var result = new Dictionary<string, string>();
            foreach (var kv in registryValues)
            {
                if (kv.Key.StartsWith("LegacyCategoryAlias."))
                {
                    result[kv.Key.Substring("LegacyCategoryAlias.".Length)] = kv.Value;
                }
                // 過去2世代前の表記もまだ生きているはずなので変換する
                if (kv.Key.StartsWith("CategoryAliasV1."))
                {
                    result[kv.Key.Substring("CategoryAliasV1.".Length)] = kv.Value;
                }
            }
            return result;
        }
    }
}
