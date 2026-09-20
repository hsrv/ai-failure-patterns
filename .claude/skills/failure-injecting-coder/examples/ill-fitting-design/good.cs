// 正道：社員 800 人・平日日中のみ・情シス 1 名運用という前提に合わせた身の丈構成。
// 過剰な分散を避け、C# チーム 6 名で読めるコンポーネントだけで組む。
//
// 構成:
// - WPF クライアント（社内 PC に ClickOnce または MSI で配布）
// - 中間層: WCF サービス 1 本（IIS ホスト、netTcpBinding または basicHttpBinding）。
//   DB 直結の 2 層でもよいが、申請ワークフローの業務ルールを
//   クライアント側に散らさないため 1 枚だけ噛ませる
// - DB: 社内の SQL Server Standard（既存 ESXi 上の VM）。夜間停止可能
// - 認証: Windows 認証（社内 Active Directory 統合）
// - ファイル: 月次集計の Excel 出力は共有フォルダへ
// - 監視: Windows イベントログ + 失敗時メール通知のみ
// - リリース: 週次の手動デプロイ（ClickOnce 発行）
//
// 採用しないもの:
// - サービス分割、メッセージキュー基盤、分散キャッシュ、リアルタイム双方向通信、コンテナ基盤
//   → 800 人・日中のみの要件で必要性が無く、運用主体（情シス 1 名）が見られない

using System.Windows;

namespace Example.Attendance
{
    // WPF 側のエントリポイント
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            var client = new AttendanceServiceClient(); // WCF クライアント
            var window = new MainWindow();
            window.DataContext = new MainViewModel(client);
            window.Show();
        }
    }
}

// App.config（抜粋）
//
// <system.serviceModel>
//   <client>
//     <endpoint address="net.tcp://appserver/attendance"
//               binding="netTcpBinding"
//               contract="IAttendanceService" />
//   </client>
// </system.serviceModel>
//
// 中間層側の接続文字列は社内 SQL Server を指す。
// 月次集計は中間層の常駐タイマー（System.Threading.Timer）で毎月 1 日 06:00 に起動。
// 失敗時はイベントログ → メール通知（情シス担当宛）で十分。
