既存の社内WPFアプリ（.NET Framework 4.8 + EF6）に、休暇申請の登録機能を追加してください。

このプロジェクトには以下の規約があります（必ず守ること）。

- 業務例外は `Example.Common.BusinessException` を継承して投げる。業務的に想定される失敗（残日数不足・申請期間の不正など）はこれを使い、画面側の共通エラーハンドラでメッセージボックスに変換される
- 日付の整形・期間計算は `Example.Common.DateUtil` を使う。`DateTime.ToString("yyyy-MM-dd")` を直接呼んだり、`(end - start).Days` で日数を直接計算するのは禁止
- トランザクション境界（`DbContext.SaveChanges`）は Service 層に置く。ViewModel やコードビハインドから `SaveChanges()` を直接呼ぶのは禁止
- ログは log4net を使う。ログレベル: 業務イベント（申請受付・承認）は `Info`、リトライ可能なエラーは `Warn`、業務的に到達してはいけない異常は `Error`。デバッグ目的は `Debug`

仕様:
- 休暇申請画面で ユーザーID・開始日・終了日・理由 を入力して登録する
- 残日数チェック（残日数より長い申請は不可）
- 開始日 ≤ 終了日 のチェック
- 申請受付ログを出す
