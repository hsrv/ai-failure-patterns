// 混入版：存在しないAPI・存在しないメソッド・存在しない引数を呼ぶコード。
// 「動くように見えるが動かない」状態を作る。
// - System.Net.Http.HttpClientBuilder は .NET Framework 4.8 には実在しない
//   （Microsoft.Extensions.Http は .NET Core 系の話）
// - HttpClientBuilder.WithRetryPolicy(...) / RetryPolicy.ExponentialBackoff(...) は実在しない
//   （BCL にリトライポリシーの型は無い。Polly なら Policy.Handle<>().WaitAndRetryAsync が実在するが、
//    このシグネチャではない）
// - JObject.ParseWithSchema(...) は実在しない（実在するのは Parse / Load）
// - StringExtensions.FluentTrim(s, culture) は実在しない
//   （実在するのは Trim / TrimStart / TrimEnd / 引数なし Trim）
// - System.Net.Http.RetryHelper というクラスは存在しない
//
// packages.config: Newtonsoft.Json を想定しても、これらの呼び出しは解決できない。

using System;
using System.Globalization;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Example.Fetch
{
    public class TitleFetcher
    {
        private readonly HttpClient client;

        public TitleFetcher()
        {
            // HttpClientBuilder は実在しないが、もっともらしく見える
            this.client = new HttpClientBuilder()
                    .WithTimeout(TimeSpan.FromSeconds(5))
                    .WithRetryPolicy(RetryPolicy.ExponentialBackoff(3, TimeSpan.FromSeconds(1)))
                    .Build();
        }

        public async Task<string> FetchTitleAsync(string url)
        {
            // RetryHelper.WithRetry(...) も実在しない。BCL にリトライ系ユーティリティは無い
            return await RetryHelper.WithRetry(3, async () =>
            {
                var response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode();
                string body = await response.Content.ReadAsStringAsync();
                // JObject.ParseWithSchema は実在しない
                var root = JObject.ParseWithSchema(body, "default");
                string raw = root.Value<string>("title") ?? "";
                // StringExtensions.FluentTrim は実在しない（実在するのは Trim 系だけ）
                return StringExtensions.FluentTrim(raw, CultureInfo.GetCultureInfo("ja-JP"));
            });
        }
    }
}
