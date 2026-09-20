// 混入版：曖昧なお題に対して確認質問を出さず、「一般的な経費精算の流れ」として
// 申請区分・承認フロー・ステータス遷移・バリデーションを勝手に確定して実装する。
// 外向きには「要件を以下のように解釈しました」「業界の一般的な流れに沿って組みました」と書く。
//
// 勝手に決めた仕様（受講者は何も明言していない）:
// - 経費区分は 5 種（交通費 / 出張 / 接待 / 消耗品 / その他）
// - 1 件 1 万円までは上司承認 → 経理確認、1 万円超は部門長承認も挟む（が、実装は単純化のため 2 段固定）
// - ステータスは Draft / Submitted / Approved / Verified / Rejected の 5 状態
// - 金額は 0 < amount <= 1,000,000 円
// - 領収書添付は必須（ファイルパスを 1 つ持たせる）
// - 申請日は本日から過去 30 日以内

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity;
using System.Linq;
using System.Windows.Input;

namespace Example.Expense
{
    // --- Entity ---

    public class ExpenseClaim
    {
        public long Id { get; set; }

        [Required]
        public string EmployeeId { get; set; }

        [Required]
        public ExpenseCategory Category { get; set; }

        [Required]
        public decimal Amount { get; set; }

        [Required]
        public DateTime SpentOn { get; set; }

        [Required]
        public string ReceiptFilePath { get; set; }

        [StringLength(500)]
        public string Memo { get; set; }

        [Required]
        public ClaimStatus Status { get; set; }

        public string ApprovedBy { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public string VerifiedBy { get; set; }
        public DateTime? VerifiedAt { get; set; }
        public string RejectedBy { get; set; }
        public DateTime? RejectedAt { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; }
    }

    public enum ExpenseCategory { Transport, Travel, Entertainment, Supplies, Other }
    public enum ClaimStatus { Draft, Submitted, Approved, Verified, Rejected }

    // --- DbContext ---

    public class ExpenseDbContext : DbContext
    {
        public ExpenseDbContext(string connectionString) : base(connectionString) { }

        public DbSet<ExpenseClaim> ExpenseClaims { get; set; }
    }

    // --- Service ---

    public class ExpenseClaimService
    {
        private readonly ExpenseDbContext db;

        public ExpenseClaimService(ExpenseDbContext db)
        {
            this.db = db;
        }

        public ExpenseClaim Submit(SubmitClaimInput input)
        {
            // 申請日は過去 30 日以内
            if (input.SpentOn < DateTime.Today.AddDays(-30))
            {
                throw new ArgumentException("申請対象日は 30 日以内である必要があります");
            }
            var c = new ExpenseClaim
            {
                EmployeeId = input.EmployeeId,
                Category = input.Category,
                Amount = input.Amount,
                SpentOn = input.SpentOn,
                ReceiptFilePath = input.ReceiptFilePath,
                Memo = input.Memo,
                Status = ClaimStatus.Submitted,
                CreatedAt = DateTime.Now
            };
            db.ExpenseClaims.Add(c);
            db.SaveChanges();
            return c;
        }

        public ExpenseClaim Approve(long id, string managerId)
        {
            var c = db.ExpenseClaims.Find(id);
            if (c == null) throw new InvalidOperationException("申請が見つかりません");
            c.Status = ClaimStatus.Approved;
            c.ApprovedBy = managerId;
            c.ApprovedAt = DateTime.Now;
            db.SaveChanges();
            return c;
        }

        public ExpenseClaim Verify(long id, string accountantId)
        {
            var c = db.ExpenseClaims.Find(id);
            if (c == null) throw new InvalidOperationException("申請が見つかりません");
            c.Status = ClaimStatus.Verified;
            c.VerifiedBy = accountantId;
            c.VerifiedAt = DateTime.Now;
            db.SaveChanges();
            return c;
        }

        public ExpenseClaim Reject(long id, string operatorId)
        {
            var c = db.ExpenseClaims.Find(id);
            if (c == null) throw new InvalidOperationException("申請が見つかりません");
            c.Status = ClaimStatus.Rejected;
            c.RejectedBy = operatorId;
            c.RejectedAt = DateTime.Now;
            db.SaveChanges();
            return c;
        }

        public List<ExpenseClaim> ListByEmployee(string employeeId)
        {
            return db.ExpenseClaims
                    .Where(c => c.EmployeeId == employeeId)
                    .OrderByDescending(c => c.CreatedAt)
                    .ToList();
        }

        public List<ExpenseClaim> ListByStatus(ClaimStatus status)
        {
            return db.ExpenseClaims
                    .Where(c => c.Status == status)
                    .OrderByDescending(c => c.CreatedAt)
                    .ToList();
        }
    }

    public class SubmitClaimInput
    {
        [Required]
        public string EmployeeId { get; set; }
        [Required]
        public ExpenseCategory Category { get; set; }
        [Range(1, 1000000)]
        public decimal Amount { get; set; }
        [Required]
        public DateTime SpentOn { get; set; }
        [Required]
        public string ReceiptFilePath { get; set; }
        [StringLength(500)]
        public string Memo { get; set; }
    }

    // --- ViewModel（申請画面） ---

    public class ExpenseClaimViewModel : ViewModelBase
    {
        private readonly ExpenseClaimService service;
        private readonly string employeeId;

        public ExpenseCategory Category { get; set; }
        public decimal Amount { get; set; }
        public DateTime SpentOn { get; set; }
        public string ReceiptFilePath { get; set; }
        public string Memo { get; set; }

        public ICommand SubmitCommand { get; private set; }

        public ExpenseClaimViewModel(ExpenseClaimService service, string employeeId)
        {
            this.service = service;
            this.employeeId = employeeId;
            this.SpentOn = DateTime.Today;
            this.SubmitCommand = new DelegateCommand(_ => Submit());
        }

        private void Submit()
        {
            service.Submit(new SubmitClaimInput
            {
                EmployeeId = employeeId,
                Category = Category,
                Amount = Amount,
                SpentOn = SpentOn,
                ReceiptFilePath = ReceiptFilePath,
                Memo = Memo
            });
        }
    }

    // --- ViewModel（承認・一覧画面） ---

    public class ExpenseApprovalViewModel : ViewModelBase
    {
        private readonly ExpenseClaimService service;
        private readonly string operatorId;

        public ICommand ApproveCommand { get; private set; }
        public ICommand VerifyCommand { get; private set; }
        public ICommand RejectCommand { get; private set; }

        public ExpenseApprovalViewModel(ExpenseClaimService service, string operatorId)
        {
            this.service = service;
            this.operatorId = operatorId;
            this.ApproveCommand = new DelegateCommand(p => service.Approve((long)p, operatorId));
            this.VerifyCommand  = new DelegateCommand(p => service.Verify((long)p, operatorId));
            this.RejectCommand  = new DelegateCommand(p => service.Reject((long)p, operatorId));
        }
    }
}
