// 正道：v1 を壊すかどうかは、利用者と保証範囲を確認してから決める。
// 確認の結果を「保つ範囲・保つ期限・廃止計画」として明記し、
//   - v2 の保存形式には明示的に "version": 2 を付ける
//   - v1 ファイルは読み込み時に v2 形式へ移行するローダを1箇所に置く
//   - v1 形式での新規保存は廃止する。v1 読み込みブリッジには廃止予定日をコメントで明記する
// という形にする。1つの読込経路に新旧形式を嗅ぎ分ける分岐を同居させない。

using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Example.App.Search
{
    // ドメインの検索条件は1つだけ。ファイル形式の分岐はローダ境界で吸収する。
    public class ProductSearchCriteria
    {
        public string Keyword { get; set; }
        public string Category { get; set; }
        public decimal? PriceMin { get; set; }
        public decimal? PriceMax { get; set; }
    }

    public class SavedSearchLoader
    {
        // v1 形式の読み込みブリッジ。廃止予定日 2027-03 末。
        // 廃止日以降は本メソッドごと削除し、v1 ファイルは移行ツールで一括変換済みの前提とする。
        public ProductSearchCriteria Load(string path)
        {
            var json = JObject.Parse(File.ReadAllText(path));
            int version = json.Value<int?>("version") ?? 1;

            if (version == 1)
            {
                // v1: { "keyword": "...", "category": "..." }
                return new ProductSearchCriteria
                {
                    Keyword = json.Value<string>("keyword"),
                    Category = json.Value<string>("category")
                };
            }

            // v2: { "version": 2, "query": {...}, "filters": {...} }
            var query = json["query"];
            var filters = json["filters"];
            return new ProductSearchCriteria
            {
                Keyword = query == null ? null : query.Value<string>("keyword"),
                Category = query == null ? null : query.Value<string>("category"),
                PriceMin = filters == null ? (decimal?)null : filters.Value<decimal?>("priceMin"),
                PriceMax = filters == null ? (decimal?)null : filters.Value<decimal?>("priceMax")
            };
        }
    }

    public class SavedSearchWriter
    {
        // 新規保存は v2 のみ。version フィールドで将来の判定を分岐なしにする。
        public void Save(string path, ProductSearchCriteria criteria)
        {
            var json = new JObject
            {
                ["version"] = 2,
                ["query"] = new JObject
                {
                    ["keyword"] = criteria.Keyword,
                    ["category"] = criteria.Category
                },
                ["filters"] = new JObject
                {
                    ["priceMin"] = criteria.PriceMin,
                    ["priceMax"] = criteria.PriceMax
                }
            };
            File.WriteAllText(path, json.ToString(Formatting.Indented));
        }
    }
}
