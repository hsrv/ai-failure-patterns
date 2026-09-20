// 正道：既存規約に従う。
// - BusinessException を投げる
// - DateUtil を使って日数計算・日付整形
// - SaveChanges は Service 層のメソッドで呼ぶ
// - 業務イベントは Info で出す

using System;
using System.Windows.Input;
using Example.Common;
using log4net;

namespace Example.Leave
{
    // 休暇申請画面の ViewModel。入力の受け渡しに専念し、業務ルールは Service に委譲する。
    public class LeaveRequestViewModel : ViewModelBase
    {
        private readonly LeaveRequestService service;
        private readonly long userId;

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Reason { get; set; }

        public ICommand RegisterCommand { get; private set; }

        public LeaveRequestViewModel(LeaveRequestService service, long userId)
        {
            this.service = service;
            this.userId = userId;
            this.RegisterCommand = new DelegateCommand(_ => Register());
        }

        private void Register()
        {
            // BusinessException は共通エラーハンドラでメッセージボックスに変換される
            service.Register(userId, StartDate, EndDate, Reason);
        }
    }

    public class LeaveRequestService
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(LeaveRequestService));

        private readonly LeaveDbContext db;

        public LeaveRequestService(LeaveDbContext db)
        {
            this.db = db;
        }

        public LeaveRequest Register(long userId, DateTime startDate, DateTime endDate, string reason)
        {
            if (startDate > endDate)
            {
                throw new BusinessException("leave.startDate.afterEndDate");
            }

            int requestedDays = DateUtil.DaysBetweenInclusive(startDate, endDate);
            int remainingDays = db.LeaveBalances.Find(userId).RemainingDays;
            if (requestedDays > remainingDays)
            {
                throw new BusinessException("leave.balance.insufficient");
            }

            var saved = LeaveRequest.Of(userId, startDate, endDate, reason);
            db.LeaveRequests.Add(saved);
            db.SaveChanges();

            log.InfoFormat(
                    "Leave request accepted: userId={0}, period={1} - {2}, days={3}",
                    userId,
                    DateUtil.Format(startDate),
                    DateUtil.Format(endDate),
                    requestedDays);
            return saved;
        }
    }

    public class LeaveRequest
    {
        public long Id { get; set; }
        public long UserId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Reason { get; set; }

        public static LeaveRequest Of(long userId, DateTime s, DateTime e, string r)
        {
            return new LeaveRequest
            {
                UserId = userId,
                StartDate = s,
                EndDate = e,
                Reason = r
            };
        }
    }
}
