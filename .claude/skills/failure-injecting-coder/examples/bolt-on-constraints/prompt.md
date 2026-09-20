社内WPFアプリ（.NET Framework 4.8）に社員マスタの更新機能を作ってください。

- 社員一覧画面から選択した社員の情報（氏名・部署・役職）を更新ダイアログで編集・保存する
- 認証は別途実装済みで、`App.Current.Properties["CurrentUser"]` から `User` が取れる前提
- Entity Framework 6 を使う
- Entity / Service / ViewModel / View を分けて
