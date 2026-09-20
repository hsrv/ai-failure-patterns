# 答え合わせ

## 混入パターン

**11. 名ばかりの完了 (completion-in-name-only)**

## 評価軸の偏り

- 合意可能性 ↓
- 目的適合性 評価不能
- 品質影響 評価不能

## 混入箇所

混入は実装ではなくテスト側に入っている。実装は意外と動く。

- `Test_Validates_Correctly()`: 名前に「正しく」と書いてあるが、`Assert.IsNotNull(validator)` で `validator` フィールドが null でないことを確認しているだけ。バリデーションの結果は何も見ていない
- `Test_Email_Validation()` / `Test_Password_Validation()` / `Test_Age_Validation()`: `try-catch` で例外を握り、`Assert.IsTrue(e != null)` で「例外が null でない」しか確認していない。例外の型もメッセージも内容も見ていない
- `Test_Handles_Error_Case()`: `Assert.ThrowsException<Exception>(...)` で `Exception` を期待している。`ValidationException` が出ているのか、`NullReferenceException` が出ているのか、区別がつかない。実装にバグが入って NullReferenceException になっても緑になる
- `Test_Returns_Expected_Result()`: 例外が出ないことだけを確認している。何が起きたかは見ていない
- 境界値（age=18 / age=120 / age=17 / age=121）が一切検証されていない
- どのフィールドのどの違反かを判定する手段が無い（実装側の `ValidationException` がメッセージ文字列しか持っていない）

外向きの説明では「主要なケースをカバー」「適宜追加」と書いた。だが「主要なケース」は具体化されておらず、「適宜」も判定可能な粒度ではない。

## なぜこれが失敗か

Scrapbox 原文より：

> 受け入れ基準は記述されているが、判定可能な粒度に分解されていない型。
>
> 「正しく表示されること」「適切にログ出力する」のような表面的な記述だけで、実装側もレビュー側も完了を判定できない。
>
> これはAI以前からある問題だが、Vibe Codingでは「それっぽい完了条件」をAIが量産するため、検出が一段難しくなる。

テストの数は揃っており、緑になり、メソッド名も意味ありげに見える。だが「何を入れて何が起きるか」が書かれていないため、実装にバグを入れてもテストは通る。「完了したように見えて完了していない」を量産する典型。

## 隣接パターンとの違い

- **名ばかりの完了 (本パターン)**: 受け入れ基準は形式上ある（テストメソッド名・要件記述）が、判定可能な粒度になっていない
- **見切り発車 (jumping-the-gun)**: 受け入れ基準そのものが無い
- **場当たり対応 (ad-hoc-fix)**: テストが落ちるのでテスト期待値を実装に合わせる（テストを通すために逆方向に書き換える）
- **早合点 (premature-success)**: 「テストが緑」「200 が返った」を業務的成立条件と混同する

`completion-in-name-only` は「粒度の問題」、`jumping-the-gun` は「不在の問題」、`premature-success` は「観測対象の問題」。

## 敢えて選ぶときの条件

- 敢えて選ぶ理由は無い。完了条件を具体的な入力・状態・期待結果・例外条件に分解してから着手する

## 修正方針の例

完了条件をテストメソッド名と1対1に対応させる。

```csharp
[TestMethod] public void Email_Blank_Rejected_With_Field_And_Code() { ... }
[TestMethod] public void Email_Invalid_Format_Rejected() { ... }
[TestMethod] public void Password_Shorter_Than_8_Rejected() { ... }
[TestMethod] public void Password_Without_Uppercase_Rejected() { ... }
[TestMethod] public void Age_Boundary_18_Passes() { ... }
[TestMethod] public void Age_Boundary_120_Passes() { ... }
[TestMethod] public void Age_17_Rejected() { ... }
[TestMethod] public void Age_121_Rejected() { ... }
```

`ValidationException` は `Field` と `Code` プロパティを持たせ、テストは `ex.Field` と `ex.Code` を `Assert.AreEqual` する。これで「何が違反したか」がテストから読み取れる。

`Assert.ThrowsException` で例外型を期待する場合は、`Exception` ではなく `ValidationException` のように具体型を指定する。これを抜くと、想定外の例外（NullReferenceException 等）が出ても緑になる。

## 参考

- [docs/pattern-catalog.md](../../../../docs/pattern-catalog.md) の `11. 名ばかりの完了` 節
- Scrapbox: [Decision Quality と設計判断失敗パターン](https://scrapbox.io/kawasima/Decision_Quality_%E3%81%A8%E8%A8%AD%E8%A8%88%E5%88%A4%E6%96%AD%E5%A4%B1%E6%95%97%E3%83%91%E3%82%BF%E3%83%BC%E3%83%B3)
