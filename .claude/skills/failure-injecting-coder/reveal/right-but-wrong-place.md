# 答え合わせ

## 混入パターン

**13. 郷に従わぬ正論 (right-but-wrong-place)**

## 評価軸の偏り

- 整合性 ↓
- 目的適合性 ↓
- 実現可能性(技術) ↓

## 混入箇所

prompt.md に既存規約4点（例外設計・日付ユーティリティ・DB 更新境界・ログレベル方針）が明記されているにもかかわらず、新規コードはそのいずれにも従っていない。

- 例外: prompt の「業務例外は `BusinessException`」を無視して、`ArgumentException` と `InvalidOperationException` で投げている。`BusinessException` を `catch` してユーザー向けメッセージに変換する共通ハンドラに到達しないので、業務エラーが未処理例外として残る
- 日付: prompt の「`DateUtil` を使う。`DateTime` 標準 API 直接禁止」を無視して、`EndDate.Subtract(StartDate).Days + 1` と `ToString("yyyy-MM-dd")` を直接書いている。`DateUtil` で集約していたフォーマット文字列・タイムゾーン・営業日計算ロジックを通らない
- DB 更新: prompt の「`SaveChanges` は Service 層」を無視して、ViewModel の `Register()` から `db.SaveChanges()` を直接呼んでいる。既存の Service 層更新規約と混在して、DB 更新の境界が2層に分かれる
- ログ: prompt の「業務エラーは `Warn`/`Error`」を無視して、残日数不足（業務エラー）まで `log.Info` で出している。`Warn` 以上だけを監視している運用なら、この業務エラーは通知されない

## なぜこれが失敗か

Scrapbox 原文より：

> 既存コードは専用の業務例外を使い分けているのに、新規コードだけ標準例外に統一する。日付処理に既存の独自ユーティリティを使う規約なのに、`DateTime` の標準 API を直接呼んで書く。
>
> これはAIが「一般的Web開発」の知識で空白を埋めることで生まれる。プロジェクトの既存コードを読みに行く動作をAIに強制しない限り、減らない。

「一般論としてはどれもそれっぽい正論」なのが厄介。C# の一般的なベストプラクティスとしては「標準例外で統一」「`DateTime` の標準 API をそのまま使う」「`SaveChanges` の場所はチームで決める」のいずれも肯定派の言説がある。だがプロジェクトには既に判断が下されていて、規約として明文化されている。そこに「一般論」を持ち込むのは整合性を壊す。

## 隣接パターンとの違い

- **郷に従わぬ正論 (本パターン)**: 既存規約を**知った上で**新規だけ一般論で書く（規約適合の問題）
- **隣を見ない再実装 (rebuild-blind)**: 既存実装を**読まずに**似たものを作る（comprehension debt）
- **車輪の再発明 (wheel-reinvention)**: 標準解（フレームワーク・標準ライブラリ）を退けて独自実装する
- **越境実装 (boundary-violation)**: 責務本来の場所ではない場所に書く

特に `rebuild-blind` との見分けが重要。本ケースでは prompt.md で規約4点が明示されている。AIはそれを「読んだ」上で違う書き方を選んだ。これは「無知」ではなく「規約より一般論を優先した」失敗。

## 敢えて選ぶときの条件

- 既存規約自体が陳腐化しており、規約改定のレビューを通せるとき。規約の更新と新コードの導入をセットで提案する
- 既存規約が安全性・セキュリティ上の問題を抱えており、新規分から正論側に揃える方が望ましいとき。移行計画と既存コードの扱いを併記する

今回のケースは「共通例外ハンドラに到達しなくなる」「`DateUtil` の営業日計算ロジックを通らない」など、規約を無視することで現実的な不整合が出る。規約改定を通せる場面ではない。

## 修正方針の例

1. `BusinessException` を継承した業務例外を投げる（C# にチェック例外は無いので、規約どおりの例外型を使うことがそのまま契約になる）
2. 日付の整形・日数計算は `DateUtil.Format(...)` `DateUtil.DaysBetweenInclusive(...)` を使う
3. `db.SaveChanges()` は Service の `Register(...)` メソッドに戻す。ViewModel からは外す
4. 業務エラー（残日数不足）は `Warn` または `Error` で出す。業務イベント（申請受付）だけ `Info`

```csharp
public class LeaveRequestService
{
    public LeaveRequest Register(LeaveRequestInput input)
    {
        if (input.StartDate > input.EndDate)
        {
            throw new BusinessException("leave.startDate.afterEndDate");
        }
        int requestedDays = DateUtil.DaysBetweenInclusive(input.StartDate, input.EndDate);
        // ... 残日数チェック → BusinessException、db.SaveChanges() はここ
    }
}
```

CLAUDE.md や AGENTS.md に既存規約をリスト化し、新規コード生成前に必ず読ませる運用とセットで効く。

## 参考

- [docs/pattern-catalog.md](../../../../docs/pattern-catalog.md) の `13. 郷に従わぬ正論` 節
- Scrapbox: [Decision Quality と設計判断失敗パターン](https://scrapbox.io/kawasima/Decision_Quality_%E3%81%A8%E8%A8%AD%E8%A8%88%E5%88%A4%E6%96%AD%E5%A4%B1%E6%95%97%E3%83%91%E3%82%BF%E3%83%BC%E3%83%B3)
