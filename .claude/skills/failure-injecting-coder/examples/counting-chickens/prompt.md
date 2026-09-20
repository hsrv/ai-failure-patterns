社内WPFアプリ（.NET Framework 4.8）で、ユーザー登録完了時に確認メールを送る機能を実装してください。

- ユーザー登録の Service から `NotificationService` を呼ぶ
- メールの宛先・件名・本文を受け取り、SMTPで送信する
- 送信失敗時はエラーログを出して例外を投げる
- SMTP 設定は App.config の mailSettings から読む
