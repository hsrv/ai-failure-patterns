// 正道：NullReferenceException は症状であって原因ではない。
// 「なぜ Order.Customer が null になり得るのか」を調べることから始める。
//
// 調査の結果、たとえば以下のいずれかが原因として考えられる:
//   (A) Order 作成時に CustomerId が必須でなく、Customer 未紐づけの Order が DB に存在する
//   (B) EF6 の遅延ロードが無効化（またはプロキシ無効化）されていて Customer が読み込まれていない
//   (C) 顧客が削除されたが Order 側の外部キーが null 許可になっていてダングリング状態
//
// (A)〜(C) のどれが原因かによって、修正は別物になる。場当たりに `if (Customer != null)` を入れない。
// 以下は (A) が原因だった場合の修正方針。

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Example.Order
{
    public class Order
    {
        public long Id { get; set; }

        // 顧客は必須にする（DB 側も NOT NULL 制約を追加するマイグレーションを別途用意）
        [Required]
        public long CustomerId { get; set; }

        [ForeignKey("CustomerId")]
        public virtual Customer Customer { get; set; }

        public decimal TotalAmount { get; set; }
    }

    public class Customer
    {
        public long Id { get; set; }
        public string Name { get; set; }
    }

    public class OrderSummaryService
    {
        private readonly IOrderRepository orderRepo;

        public OrderSummaryService(IOrderRepository orderRepo)
        {
            this.orderRepo = orderRepo;
        }

        public OrderSummary Summarize(long orderId)
        {
            Order order = orderRepo.FindById(orderId);
            if (order == null)
            {
                throw new OrderNotFoundException(orderId);
            }

            // ここで Customer.Name を呼んでも、不変条件「Order には Customer が必ず紐づく」が
            // モデルとDBで保証されているので NullReferenceException は起きない。
            return new OrderSummary(
                    order.Id,
                    order.Customer.Name,
                    order.TotalAmount
            );
        }
    }
}

// 別途、対応するマイグレーションと注文作成側の修正:
//
// 1. 既存データで CustomerId が null の注文を調査し、業務上どう扱うかを決める
//    （仮顧客に寄せる／削除する／別ステータスに移す等）
// 2. DB に NOT NULL 制約を追加するマイグレーション
// 3. 注文登録画面側で CustomerId 必須バリデーションを入れる
// 4. テスト: Customer なしで Order を作ろうとするとエラーになることを確認
