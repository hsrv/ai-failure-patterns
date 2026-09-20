// 混入版：既存規約を知っているのに、新規コードだけ「モダンで一般的に正しい」書き方で書く。
// - BusinessException ではなく ArgumentException/InvalidOperationException で投げる
// - DateUtil を使わず DateTime の標準 API で日数計算・整形を直接書く
// - ViewModel から SaveChanges を直接呼ぶ
// - 業務イベントログ・エラーログを全部 Info で出す

using System;
using System.Windows.Input;
using log4net;

namespace Example.Leave
{
    // 休暇申請画面の ViewModel
    public class LeaveRequestViewModel : ViewModelBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(LeaveRequestViewModel));

        private readonly LeaveDbContext db;
        private readonly long userId;

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Reason { get; set; }

        public ICommand RegisterCommand { get; private set; }

        public LeaveRequestViewModel(LeaveDbContext db, long userId)
        {
            this.db = db;
            this.userId = userId;
            this.RegisterCommand = new DelegateCommand(_ => Register());
        }

        private void Register()
        {
            if (StartDate > EndDate)
            {
                // 業務エラーも標準例外で統一した方がシグネチャがスッキリする
                throw new ArgumentException("StartDate must be on or before EndDate");
            }

            // 日数計算は .NET 標準の TimeSpan で（外部ユーティリティに依存しない方がモダン）
            int requestedDays = EndDate.Subtract(StartDate).Days + 1;

            var balance = db.LeaveBalances.Find(userId);
            if (requestedDays > balance.RemainingDays)
            {
                // 残日数不足も標準例外で
                log.InfoFormat(
                        "Leave balance insufficient: userId={0}, requested={1}, remaining={2}",
                        userId, requestedDays, balance.RemainingDays);
                throw new InvalidOperationException("Leave balance insufficient");
            }

            var request = new LeaveRequest
            {
                UserId = userId,
                StartDate = StartDate,
                EndDate = EndDate,
                Reason = Reason
            };
            db.LeaveRequests.Add(request);
            db.SaveChanges(); // ViewModel から直接保存して、画面と DB の対応が追いやすい

            log.InfoFormat(
                    "[LeaveRequest] accepted: userId={0}, period={1}-{2}, days={3}",
                    userId,
                    StartDate.ToString("yyyy-MM-dd"),
                    EndDate.ToString("yyyy-MM-dd"),
                    requestedDays);
        }
    }

    public class LeaveRequest
    {
        public long Id { get; set; }
        public long UserId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Reason { get; set; }
    }
}
