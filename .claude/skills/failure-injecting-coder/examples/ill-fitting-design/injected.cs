// 混入版：技術的にはモダンで筋の通った構成を提案する。
// 「可用性」「将来のスケーラビリティ」「業界のベストプラクティス」を理由に、
// サービス分割 + メッセージ基盤 + 分散キャッシュ + 常駐監視を採用する。
// 情シス 1 名がこれを運用できるかには触れない。
//
// 全体構成:
// - クライアント: WPF + Prism（モジュール分割、DI コンテナ: Unity）
// - 中間層: 6 つのサービスに分割し、それぞれ独立した Windows サービスとして常駐
//   - AttendancePunchService（打刻）
//   - WorkflowService（申請ワークフロー）
//   - AggregationService（月次集計）
//   - NotificationService（通知）
//   - IdentityService（認証認可）
//   - AuditService（監査ログ）
// - サービス間連携: MSMQ（トランザクションキュー）+ NServiceBus
// - DB: SQL Server AlwaysOn 可用性グループ（2ノード同期コミット + 読み取りレプリカ）
// - キャッシュ: Redis on Windows（クラスタ構成）
// - 認証認可: IdentityServer 3（自己ホスト）+ Active Directory 連携
// - クライアント通知: SignalR によるリアルタイムプッシュ
// - 監視: 自作ヘルスチェック + PerformanceCounter + 監視用 WPF ダッシュボード
// - デプロイ: 各サービスごとの MSI + PowerShell DSC による構成管理

using System;
using System.ServiceModel;
using NServiceBus;

namespace Example.Attendance.Punch
{
    // 打刻サービス：WCF エンドポイントを持ち、処理は MSMQ 経由のメッセージに流す
    [ServiceContract]
    public interface IPunchService
    {
        [OperationContract]
        PunchAccepted Punch(PunchRequest request);
    }

    public class PunchService : IPunchService
    {
        private readonly IMessageSession bus;

        public PunchService(IMessageSession bus)
        {
            this.bus = bus;
        }

        public PunchAccepted Punch(PunchRequest request)
        {
            var ev = new PunchEvent
            {
                EventId = Guid.NewGuid().ToString(),
                EmployeeId = request.EmployeeId,
                Kind = request.Kind,
                OccurredAt = DateTime.Now
            };
            bus.Send(ev); // 後段の Aggregation/Audit が購読する
            return new PunchAccepted { EventId = ev.EventId };
        }
    }

    public class PunchRequest
    {
        public string EmployeeId { get; set; }
        public string Kind { get; set; }
    }

    public class PunchAccepted
    {
        public string EventId { get; set; }
    }

    public class PunchEvent : IMessage
    {
        public string EventId { get; set; }
        public string EmployeeId { get; set; }
        public string Kind { get; set; }
        public DateTime OccurredAt { get; set; }
    }
}

// appSettings（監視・SLA 設定の抜粋）
//
// <add key="Sla.AvailabilityTarget" value="99.95" />
// <add key="Sla.LatencyP99Ms" value="200" />
// <add key="Monitoring.HealthCheckIntervalSec" value="5" />
// <add key="Monitoring.OnCallMail" value="sre@example.local" />
// <add key="Deployment.FreezeOnBurnRate" value="6" />
//
// クライアント側 bootstrapper（Prism/Unity 抜粋）
//
// container.RegisterType<IPunchService, PunchServiceClient>();
// container.RegisterType<IWorkflowService, WorkflowServiceClient>();
// container.RegisterType<INotificationHub, SignalRNotificationHub>(
//     new ContainerControlledLifetimeManager());
