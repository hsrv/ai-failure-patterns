// 正道：業務上の成功は「取引先 API がレスポンスボディで `accepted: true` を返したこと」。
// HTTP 2xx で受信できたことは「通信が成立した」だけで、業務的な受理を意味しない。
// レスポンスボディを解釈して `accepted=false` を業務エラーとして扱う。
// さらに、対象 0 件の朝は「異常ではないか」を疑える形にログ・通知を組む。

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Example.App.Replenishment
{
    public sealed class ReplenishmentService
    {
        private readonly IProductRepository productRepo;
        private readonly IReplenishmentLogRepository logRepo;
        private readonly AdminNotifier notifier;
        private readonly HttpClient http;

        public ReplenishmentService(IProductRepository productRepo,
                                    IReplenishmentLogRepository logRepo,
                                    AdminNotifier notifier,
                                    HttpClient http)
        {
            this.productRepo = productRepo;
            this.logRepo = logRepo;
            this.notifier = notifier;
            this.http = http;
            this.http.BaseAddress = new Uri("https://supplier.example.com");
        }

        // 事務員が毎朝「補充依頼送信」ボタンを押す → ViewModel のコマンドから呼ばれる
        public async Task RunAsync()
        {
            List<Product> targets = productRepo.FindBelowReplenishmentThreshold();

            // 対象 0 件を異常として扱う。日々動いている業務の前提が崩れたら通知する。
            if (targets.Count == 0)
            {
                notifier.Warn("在庫補充対象が 0 件でした。閾値設定または商品マスタを確認してください。");
                return;
            }

            int succeeded = 0;
            int businessFailed = 0;
            int communicationFailed = 0;

            foreach (var p in targets)
            {
                var result = await SendAsync(p);
                logRepo.Save(new ReplenishmentLog(p, result, DateTime.Now));
                switch (result.Kind)
                {
                    case ReplenishmentResultKind.Accepted: succeeded++; break;
                    case ReplenishmentResultKind.Rejected: businessFailed++; break;
                    case ReplenishmentResultKind.Error:    communicationFailed++; break;
                }
            }

            if (businessFailed > 0 || communicationFailed > 0)
            {
                notifier.Alert(string.Format(
                        "在庫補充: 成功 {0} / 業務エラー {1} / 通信エラー {2}",
                        succeeded, businessFailed, communicationFailed));
            }
        }

        private async Task<ReplenishmentResult> SendAsync(Product p)
        {
            try
            {
                var req = new SupplierRequest
                {
                    ProductId = p.Id,
                    Quantity = p.ReplenishmentQuantity
                };
                var res = await http.PostAsync(
                        "/api/replenishment",
                        new StringContent(JsonConvert.SerializeObject(req),
                                Encoding.UTF8, "application/json"));

                if (!res.IsSuccessStatusCode)
                {
                    return ReplenishmentResult.Error("HTTP " + (int)res.StatusCode);
                }
                var body = JsonConvert.DeserializeObject<SupplierResponse>(
                        await res.Content.ReadAsStringAsync());
                if (body == null)
                {
                    return ReplenishmentResult.Error("response body is null");
                }
                // ここが核心：HTTP 2xx でも、業務上の受理は accepted フラグで判定する
                if (body.Accepted)
                {
                    return ReplenishmentResult.Accepted(body.SupplierOrderId);
                }
                return ReplenishmentResult.Rejected(body.Reason);
            }
            catch (HttpRequestException e)
            {
                return ReplenishmentResult.Error(e.Message);
            }
            catch (TaskCanceledException e)
            {
                return ReplenishmentResult.Error(e.Message); // タイムアウト
            }
        }
    }

    public class SupplierRequest
    {
        public string ProductId { get; set; }
        public int Quantity { get; set; }
    }

    public class SupplierResponse
    {
        public bool Accepted { get; set; }
        public string SupplierOrderId { get; set; }
        public string Reason { get; set; }
    }
}
