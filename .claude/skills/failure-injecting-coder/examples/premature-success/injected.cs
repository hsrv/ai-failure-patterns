// 混入版：HTTP 2xx を返したら成功、それ以外は失敗、で判定する。
// レスポンスボディの `accepted` フラグは見ない。
// 対象 0 件の送信も「正常に完了」とログに残して終わる。
// 「正常応答が返ったので成功とみなす」「例外が発生していないので問題なし」で押し通す。

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Example.App.Replenishment
{
    public sealed class ReplenishmentService
    {
        private static readonly TraceSource log = new TraceSource("Replenishment");

        private readonly IProductRepository productRepo;
        private readonly IReplenishmentLogRepository logRepo;
        private readonly HttpClient http;

        public ReplenishmentService(IProductRepository productRepo,
                                    IReplenishmentLogRepository logRepo,
                                    HttpClient http)
        {
            this.productRepo = productRepo;
            this.logRepo = logRepo;
            this.http = http;
            this.http.BaseAddress = new Uri("https://supplier.example.com");
        }

        // 事務員が毎朝「補充依頼送信」ボタンを押す → ViewModel のコマンドから呼ばれる
        public async Task RunAsync()
        {
            log.TraceInformation("在庫補充送信 開始");

            List<Product> targets = productRepo.FindBelowReplenishmentThreshold();
            log.TraceInformation("対象 {0} 件", targets.Count);

            int successCount = 0;
            int failureCount = 0;

            foreach (var p in targets)
            {
                bool ok = await SendReplenishmentAsync(p);
                logRepo.Save(new ReplenishmentLog(
                        p.Id, p.ReplenishmentQuantity,
                        ok ? "SUCCESS" : "FAILURE",
                        DateTime.Now));
                if (ok)
                {
                    successCount++;
                }
                else
                {
                    failureCount++;
                }
            }

            log.TraceInformation("在庫補充送信 完了: 成功 {0} 件 / 失敗 {1} 件",
                    successCount, failureCount);
        }

        private async Task<bool> SendReplenishmentAsync(Product p)
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

                // 2xx で返ってきていれば送信成功とみなす
                if (res.IsSuccessStatusCode)
                {
                    log.TraceInformation("補充依頼 OK: productId={0}", p.Id);
                    return true;
                }
                log.TraceEvent(TraceEventType.Warning, 0,
                        "補充依頼 NG: productId={0}, status={1}", p.Id, (int)res.StatusCode);
                return false;
            }
            catch (HttpRequestException e)
            {
                log.TraceEvent(TraceEventType.Warning, 0,
                        "補充依頼 例外: productId={0}, message={1}", p.Id, e.Message);
                return false;
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
