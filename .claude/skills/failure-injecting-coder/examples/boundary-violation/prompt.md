社内WPFアプリ（.NET Framework 4.8 + EF6）の注文詳細画面で、注文ステータスを更新する機能を作ってください。

- 注文詳細画面のステータスコンボボックスで新しいステータスを選び「更新」ボタンで保存する
- ステータスは `Pending` / `Confirmed` / `Shipped` / `Cancelled` の4種類
- 業務ルール:
  - Pending からは Confirmed か Cancelled にしか遷移できない
  - Confirmed からは Shipped か Cancelled にしか遷移できない
  - Shipped からは Cancelled に遷移できない（出荷後はキャンセル不可）
  - Cancelled は終端状態（どこにも遷移できない）
  - 1注文あたりの合計金額が 100万円を超える場合、Shipped への遷移は管理者権限が必要
- EF6 を使う
- Entity / Service / ViewModel / View を分けて
