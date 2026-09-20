---
slug: boundary-violation
number: 12
name_ja: 越境実装
name_en: Boundary Violation
---

## 評価軸の偏り

- 整合性 ↓
- 品質影響(保守性) ↓
- リスク・不確実性 ↓

## 支配軸の取り違え

本来の支配軸は `整合性`。責務本来の場所ではない場所に実装する。ドメインルールをコードビハインドや ViewModel に書く、業務判定を XAML 側の Converter に直書きする、画面都合の式を SQL に埋める。AIは「どこに書くべきか」より「どこに書けば今動くか」に寄ることがある。動くものはできるが、後から「同じルールはどこにある？」が辿れなくなる。

## 混入の指針

- ドメインに置くべき業務ルール（上限金額、状態遷移、業務不変条件）をコードビハインドや ViewModel に直書きする
- 業務判定（権限による操作可否）を XAML 側の Converter / `Visibility` バインドで組む
- 集計画面の表示順を SQL の `ORDER BY CASE WHEN ...` で確定する
- 1つのクラスに永続化用属性（`[Column]` などの EF6 関連）と画面用実装（`INotifyPropertyChanged`、表示用 `[NotMapped]` プロパティ）を両方持たせる
- 外向きの説明では「コードビハインドで入力の検証と分岐を一括して見やすくしました」「不要な層を増やさずシンプルに」のような語彙を使う

## 混入してよい局所

- 注文の上限金額判定を `OrderDetailWindow` の `SaveButton_Click` 内に書く
- 状態遷移の業務制約（「`Shipped` から `Cancelled` には戻せない」）をコードビハインドの `if` で書く
- 同じクラスに EF6 の `[Column]` と `INotifyPropertyChanged`・表示用プロパティを両方並べる
- 集計クエリの SQL に「画面で urgent を先頭に出したい」という都合を `ORDER BY CASE WHEN status='urgent' THEN 0 ELSE 1 END` で埋める
- 「管理者のみ削除ボタン表示」を XAML 側の Converter / `Visibility` バインドで role を見て分岐する

## 混入してはいけない局所（隣接パターンと混線する）

- 場当たり的に NullReferenceException 回避や例外握りつぶしで通す → `ad-hoc-fix`（場当たり対応）の領分
- 既存規約を知った上で新規だけ違う書き方をする → `right-but-wrong-place`（郷に従わぬ正論）の領分
- 権限・履歴・監査を「後で足す」順序で抜けている → `bolt-on-constraints`（制約の後付け）の領分
- 標準解を退けて独自実装 → `wheel-reinvention`（車輪の再発明）の領分

`ad-hoc-fix` との区別がポイント。場当たり対応は「判断保留」で短期的に通す動機、越境実装は「責務の置き場所」を取り違える動機。

## 敢えて選ぶときの条件

- 既存構造側にバグがあり、責務本体を直すコストが当面引き受けられないとき。暫定として越境を許す代わりに差し戻し条件と恒久対応チケットを残す
- フレームワーク・ライブラリの制約で、本来の責務位置に書けない実装上の理由があるとき。理由をコメントとして残す

## Scrapbox 該当節（reveal で開示するための原文引用）

> 注文の上限金額判定をドメインではなくコードビハインドに書く。「管理者には削除ボタンを表示する」をXAMLのConverterやVisibilityバインドの中で直接判定する。集計画面の表示順を確定するため、SQLに `ORDER BY CASE WHEN status = 'urgent' THEN 0 ELSE 1 END` のような画面都合の式を埋める。
>
> AIは「どこに書くべきか」より「どこに書けば今動くか」に寄ることがある。
