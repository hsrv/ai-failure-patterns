// 混入版：機能はちゃんと動く。
// しかし権限チェックを `Update()` が引数に取らず、履歴・監査ログも残さず、楽観ロックもない。
// 「まず基本機能を作り、必要に応じて権限や監査を追加する」順序論で押し通す。

using System;
using System.ComponentModel.DataAnnotations;
using System.Data.Entity;
using System.Windows.Input;

namespace Example.Employee
{
    public class Employee
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public string Department { get; set; }
        public string Position { get; set; }

        public void Apply(UpdateEmployeeInput input)
        {
            this.Name = input.Name;
            this.Department = input.Department;
            this.Position = input.Position;
        }
    }

    public class UpdateEmployeeInput
    {
        public string Name { get; set; }
        public string Department { get; set; }
        public string Position { get; set; }
    }

    public class MasterDbContext : DbContext
    {
        public MasterDbContext(string connectionString) : base(connectionString) { }
        public DbSet<Employee> Employees { get; set; }
    }

    public class EmployeeService
    {
        private readonly MasterDbContext db;

        public EmployeeService(MasterDbContext db)
        {
            this.db = db;
        }

        public Employee Update(long id, UpdateEmployeeInput input)
        {
            var e = db.Employees.Find(id);
            if (e == null)
            {
                throw new InvalidOperationException("社員が見つかりません");
            }
            e.Apply(input);
            db.SaveChanges();
            return e;
        }
    }

    // 更新ダイアログの ViewModel
    public class EmployeeEditViewModel : ViewModelBase
    {
        private readonly EmployeeService service;
        private readonly long employeeId;

        public string Name { get; set; }
        public string Department { get; set; }
        public string Position { get; set; }

        public ICommand SaveCommand { get; private set; }

        public EmployeeEditViewModel(EmployeeService service, long employeeId)
        {
            this.service = service;
            this.employeeId = employeeId;
            this.SaveCommand = new DelegateCommand(_ => Save());
        }

        private void Save()
        {
            service.Update(employeeId, new UpdateEmployeeInput
            {
                Name = Name,
                Department = Department,
                Position = Position
            });
        }
    }
}
