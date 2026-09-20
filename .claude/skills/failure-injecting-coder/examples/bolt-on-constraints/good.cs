// 正道：社員マスタの更新は「誰が・いつ・何を・どう変えたか」が制約として本体。
// 権限チェック・監査ログ・変更履歴をデータモデルとユースケースに最初から織り込む。

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity;
using System.Security;

namespace Example.Employee
{
    public class Employee
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public string Department { get; set; }
        public string Position { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; } // 楽観ロック

        public DateTime UpdatedAt { get; set; }
        public long UpdatedBy { get; set; }
    }

    public class EmployeeHistory
    {
        public long Id { get; set; }
        public long EmployeeId { get; set; }
        public string Name { get; set; }
        public string Department { get; set; }
        public string Position { get; set; }
        public DateTime ChangedAt { get; set; }
        public long ChangedBy { get; set; }
        public string ChangeReason { get; set; }

        public static EmployeeHistory Of(Employee e, long changedBy,
                string reason, DateTime changedAt)
        {
            return new EmployeeHistory
            {
                EmployeeId = e.Id,
                Name = e.Name,
                Department = e.Department,
                Position = e.Position,
                ChangedAt = changedAt,
                ChangedBy = changedBy,
                ChangeReason = reason
            };
        }
    }

    public class EmployeeUpdateService
    {
        private readonly MasterDbContext db;
        private readonly AuthorizationPolicy authPolicy;

        public EmployeeUpdateService(MasterDbContext db, AuthorizationPolicy authPolicy)
        {
            this.db = db;
            this.authPolicy = authPolicy;
        }

        public Employee Update(long employeeId, UpdateEmployeeCommand cmd, User op)
        {
            // 更新権限（EMPLOYEE_UPDATE ロール）を入口で確認する
            if (!op.HasRole("EMPLOYEE_UPDATE"))
            {
                throw new SecurityException("社員マスタの更新権限がありません");
            }

            var current = db.Employees.Find(employeeId);
            if (current == null)
            {
                throw new EmployeeNotFoundException(employeeId);
            }

            // 業務制約：自分自身の役職は変更できない・人事部以外は他部署を変更できない 等
            if (!authPolicy.CanUpdate(op, current, cmd))
            {
                throw new SecurityException("操作対象に対する権限がありません");
            }

            // 変更前の値を履歴として保存（監査要件）
            db.EmployeeHistories.Add(
                    EmployeeHistory.Of(current, op.Id, cmd.Reason, DateTime.Now));

            current.Name = cmd.Name;
            current.Department = cmd.Department;
            current.Position = cmd.Position;
            current.UpdatedAt = DateTime.Now;
            current.UpdatedBy = op.Id;

            db.SaveChanges(); // RowVersion 不一致は DbUpdateConcurrencyException
            return current;
        }
    }
}
