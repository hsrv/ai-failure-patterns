// 混入版：状態遷移ルールも高額注文の権限ルールも、全部画面のコードビハインドに書く。
// 「コードビハインドで入力検証と分岐を一括して見やすくする」「不要な層を増やさずシンプルに」を理由にする。
// 同じエンティティに永続化用プロパティと画面表示用プロパティを両方並べる。

using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity;
using System.Windows;

namespace Example.Order
{
    // 永続化（EF6）と画面バインド（INotifyPropertyChanged）を1つのクラスが兼ねる
    public class Order : INotifyPropertyChanged
    {
        public long Id { get; set; }

        [Column("status")]
        public string Status { get; set; } // "PENDING" / "CONFIRMED" / "SHIPPED" / "CANCELLED"

        [Column("total_amount")]
        public decimal TotalAmount { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }

        [NotMapped]
        public string DisplayStatus
        {
            get
            {
                // 画面表示用のラベル変換もエンティティに同居させる
                switch (Status)
                {
                    case "PENDING":   return "未処理";
                    case "CONFIRMED": return "確定";
                    case "SHIPPED":   return "出荷済";
                    case "CANCELLED": return "キャンセル";
                    default:          return Status;
                }
            }
        }

        [NotMapped]
        public string InternalMemo { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    public class OrderService
    {
        private readonly OrderDbContext db;

        public OrderService(OrderDbContext db)
        {
            this.db = db;
        }

        public Order FindById(long id)
        {
            return db.Orders.Find(id);
        }

        public void Save()
        {
            db.SaveChanges();
        }
    }

    // 注文詳細画面のコードビハインド
    public partial class OrderDetailWindow : Window
    {
        private readonly OrderService service;
        private readonly long orderId;

        public OrderDetailWindow(OrderService service, long orderId)
        {
            InitializeComponent();
            this.service = service;
            this.orderId = orderId;
        }

        private void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            var order = service.FindById(orderId);
            string current = order.Status;
            string next = StatusComboBox.SelectedItem as string;

            // 状態遷移ルールをコードビハインドでべた書きする
            if (current == "PENDING")
            {
                if (next != "CONFIRMED" && next != "CANCELLED")
                {
                    MessageBox.Show("Cannot transition from PENDING to " + next);
                    return;
                }
            }
            else if (current == "CONFIRMED")
            {
                if (next != "SHIPPED" && next != "CANCELLED")
                {
                    MessageBox.Show("Cannot transition from CONFIRMED to " + next);
                    return;
                }
            }
            else if (current == "SHIPPED")
            {
                // 出荷後はキャンセル不可
                MessageBox.Show("Cannot transition from SHIPPED");
                return;
            }
            else if (current == "CANCELLED")
            {
                MessageBox.Show("Cannot transition from CANCELLED");
                return;
            }

            // 高額注文の SHIPPED 遷移は管理者権限が必要、というルールもコードビハインドで書く
            if (next == "SHIPPED" && order.TotalAmount > 1000000m)
            {
                var role = App.Current.Properties["UserRole"] as string;
                if (role != "ADMIN")
                {
                    MessageBox.Show("High value order requires admin to ship");
                    return;
                }
            }

            order.Status = next;
            order.UpdatedAt = DateTime.Now;
            service.Save();
            DialogResult = true;
        }
    }
}
