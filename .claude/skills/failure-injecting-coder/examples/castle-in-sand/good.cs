// 正道：実在するライブラリAPIだけを使う。
// - HttpClient にリトライ設定APIは無いので、リトライは自前のループで書く
//   （Polly 等のライブラリを入れない限り、WaitAndRetryAsync のような仕組みは無い）
// - Newtonsoft.Json の JObject.Parse(string) でパース
// - string.Trim(params char[]) で半角・全角の空白文字を指定して除去
//
// packages.config:
//   Newtonsoft.Json

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Example.Fetch
{
    public class TitleFetcher
    {
        private const int MaxAttempts = 3;
        private static readonly char[] WhitespaceChars =
                { ' ', '\t', '\r', '\n', '　' }; // 半角・タブ等・全角スペース

        private readonly HttpClient client;

        public TitleFetcher()
        {
            this.client = new HttpClient();
            this.client.Timeout = TimeSpan.FromSeconds(5);
        }

        public async Task<string> FetchTitleAsync(string url)
        {
            Exception lastError = null;
            for (int attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                try
                {
                    var response = await client.GetAsync(url);
                    response.EnsureSuccessStatusCode();
                    string body = await response.Content.ReadAsStringAsync();
                    var root = JObject.Parse(body);
                    string raw = root.Value<string>("title") ?? "";
                    return raw.Trim(WhitespaceChars);
                }
                catch (Exception e) when (e is HttpRequestException
                                        || e is TaskCanceledException
                                        || e is JsonException)
                {
                    lastError = e;
                    if (attempt < MaxAttempts)
                    {
                        await Task.Delay(BackoffMillis(attempt));
                    }
                }
            }
            throw lastError;
        }

        private static int BackoffMillis(int attempt)
        {
            return (int)(1000 * Math.Pow(2, attempt - 1));
        }
    }
}
