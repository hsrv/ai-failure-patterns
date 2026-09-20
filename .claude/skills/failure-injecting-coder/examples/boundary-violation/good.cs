// 正道：状態遷移と上限金額判定はドメインの責務。
// Order エンティティが「自分が遷移してよいか」を知っている。
// 画面（View/コードビハインド）は入力と表示に専念し、
// Service はトランザクションと権限の入口を担う。

using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity;
using System.Security;
using System.Windows.Input;

namespace Example.Order
{
    public enum OrderStatus
    {
        Pending, Confirmed, Shipped, Cancelled
    }

    public static class OrderStatusTransitions
    {
        public static bool CanTransitionTo(this OrderStatus current, OrderStatus next)
        {
            switch (current)
            {
                case OrderStatus.Pending:
                    return next == OrderStatus.Confirmed || next == OrderStatus.Cancelled;
                case OrderStatus.Confirmed:
                    return next == OrderStatus.Shipped || next == OrderStatus.Cancelled;
                case OrderStatus.Shipped:
                    return false; // 出荷後はキャンセル不可
                case OrderStatus.Cancelled:
                    return false;
                default:
                    return false;
            }
        }
    }

    public class Order
    {
        public long Id { get; set; }
        public OrderStatus Status { get; set; }
        public decimal TotalAmount { get; set; }

        public static readonly decimal HighValueThreshold = 1000000m;

        public bool RequiresAdminToShip()
        {
            return TotalAmount > HighValueThreshold;
        }

        public void ChangeStatus(OrderStatus next)
        {
            if (!Status.CanTransitionTo(next))
            {
                throw new InvalidStateTransitionException(Status, next);
            }
            Status = next;
        }
    }

    public class InvalidStateTransitionException : Exception
    {
        public InvalidStateTransitionException(OrderStatus from, OrderStatus to)
            : base("Cannot transition from " + from + " to " + to) { }
    }

    public class OrderStatusService
    {
        private readonly OrderDbContext db;

        public OrderStatusService(OrderDbContext db)
        {
            this.db = db;
        }

        // 高額注文の Shipped 遷移だけ admin 権限を要求する（業務ルール）。
        // 権限のかけ方は Service 入口でメソッドレベルに揃える。
        public Order ChangeStatus(long orderId, OrderStatus next, Operator op)
        {
            var order = db.Orders.Find(orderId);
            if (order == null)
            {
                throw new InvalidOperationException("注文が見つかりません");
            }
            if (next == OrderStatus.Shipped && order.RequiresAdminToShip() && !op.IsAdmin)
            {
                throw new SecurityException("High value order requires admin to ship");
            }
            order.ChangeStatus(next);
            db.SaveChanges();
            return order;
        }
    }

    public class Operator
    {
        public long UserId { get; set; }
        public bool IsAdmin { get; set; }
    }

    // 注文詳細画面の ViewModel。業務ルールは持たず、Service に委譲する。
    public class OrderDetailViewModel : ViewModelBase
    {
        private readonly OrderStatusService service;
        private readonly long orderId;
        private readonly Operator op;

        public OrderStatus SelectedStatus { get; set; }
        public ICommand UpdateCommand { get; private set; }

        public OrderDetailViewModel(OrderStatusService service, long orderId, Operator op)
        {
            this.service = service;
            this.orderId = orderId;
            this.op = op;
            this.UpdateCommand = new DelegateCommand(_ => Update());
        }

        private void Update()
        {
            service.ChangeStatus(orderId, SelectedStatus, op);
        }
    }
}
