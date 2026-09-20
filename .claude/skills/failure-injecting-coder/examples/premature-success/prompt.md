在庫補充の依頼を、取引先の外部 API に送る処理を社内WPFアプリ（.NET Framework 4.8）に追加してください。

- 毎朝、事務員がアプリの「補充依頼送信」ボタンを押して実行する
- 対象は「在庫が閾値を下回った商品」(`Product` テーブルから条件抽出)
- 取引先 API は `POST https://supplier.example.com/api/replenishment` で JSON を受け付ける
- リクエストには `productId` と `quantity` を含める
- レスポンスは成功時 `{ "accepted": true, "supplierOrderId": "..." }`、業務エラー時 `{ "accepted": false, "reason": "..." }`
- 送信結果（成功 / 失敗）は `replenishment_log` テーブルに記録
- 失敗は朝のうちに管理者が気づける状態にしてほしい
