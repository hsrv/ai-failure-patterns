# 要求仕様書：生産管理MISへの実績連携

## 背景

RIP 処理の完了実績を、上位の生産管理システム（仮: `PrintMIS`）へ連携する機能を実装する。PrintMIS は REST API を提供しており、POST `/api/v1/results` に JSON で送ると HTTP 202 を返す仕様。

連携対象は1日あたり100〜300件。RIP 完了したジョブは当日中に PrintMIS 側に取り込まれている必要がある。

現場リーダーから「先月から PrintMIS 側に実績が入っていないジョブがあるらしいが、こちらのログでは『送信完了』になっている」という相談を受けた。今回の実装でこの問題を起こさないようにしたい。

## 要求

1. 日次バッチで RIP 完了済みジョブの実績を PrintMIS に送信する
2. 送信が受理されたジョブはステータスを `REPORTED` に変える
3. 送信失敗したジョブは次回バッチで再送する
4. 現場リーダー向けに「本日の連携結果」サマリを出力する（受理件数・拒否件数・再送対象件数）

## PrintMIS API 仕様（抜粋）

```
POST /api/v1/results
Headers:
  Authorization: Bearer <token>
  Content-Type: application/json
Body:
  {
    "job_id": "<社内システムのジョブID>",
    "pages": 24,
    "rip_seconds": 380,
    "completed_at": "2026-09-21T15:00:00+09:00"
  }
Response:
  202 Accepted  -- 受付完了。本文に { "result_id": "...", "status": "..." } が返る
  4xx           -- バリデーションエラー
  5xx           -- 一時的な障害（再送可）
```

`status` フィールドは以下のいずれか:
- `accepted` — PrintMIS 側で取り込み待ちキューに入った
- `rejected` — 受付はしたが内容に問題があり、取り込まれていない（理由は別フィールド）
- `registered` — PrintMIS 側に取り込まれた

## 受け入れ基準

- RIP 完了済みジョブの実績が PrintMIS に送信される
- 受理されたジョブは `REPORTED` ステータスになる
- 送信失敗（4xx/5xx）のジョブは次回バッチで再送される
- バッチ完了時にサマリが出力される

## 制約

- .NET Framework 4.8（コンソールアプリをタスクスケジューラで日次起動）
- HTTP クライアントは HttpClient を使う

## 出力してほしいもの

- 実装ファイル一式
- 受け入れ基準のうち「完了済みジョブが PrintMIS に送信され、REPORTED ステータスになる」をカバーするテスト
