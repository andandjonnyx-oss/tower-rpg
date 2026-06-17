# CLAUDE.md

このリポジトリで作業する将来の自分（および Claude）への注意書き。
（精査ログ: 2026-06-16〜17 に戦闘ロジック①③④と属性表記を精査）

このプロジェクトに共通する構造的特徴は「**実行時の安全性が、消費側コードの外にある
“ある関数/仕組みが必ず守られること”に一点依存している**」こと。新機能を足すときは
下記1〜3を必ず確認すること。

---

## 1. 戦闘の static 一時状態はリセット関数への一点依存（最重要）

戦闘の一時状態の多くは `static` フィールド:
`turnActionCount`, `turnLowHpMode`, `enemyCurrentHp`, `pendingEnemyAction`,
`isEnemyPreemptive`, `enemyForcedNextSkill`, `enemyIsPoisoned` 等の状態異常フラグ群、
および `battleInitialized` 自身も static。

- **宣言時の初期化子（`= false` 等）は戦闘ごとには再実行されない。** static 初期化子は
  型の初回アクセス時に一度走るだけ。Domain Reload 無効時やビルドでは static は
  プレイをまたいで保持される。→「宣言で初期化しているから安全」は誤り。
- **実効的なリセットは2経路だけ:**
  - `ResetBattleStatics()`（戦闘終了処理）— `battleInitialized=false` に戻す。
  - 戦闘開始の初期化ブロック `if (!battleInitialized)` — 上記で false に戻っている前提。
  - 敵行動フラグ（`pendingEnemyAction` / `isEnemyPreemptive`）は
    `PreRollEnemyAction()` も毎ターン両者をリセットしている。
- ⚠️ **戦闘から離脱する新経路（逃走機能など）を作るときは、必ず `ResetBattleStatics()`
  を通すこと。** 飛ばすと `battleInitialized` が true のまま残り、次戦闘で初期化ブロック
  ごとスキップされ、敵HP・行動回数・状態異常が前戦闘から漏れる。

## 2. 敵ターンは必ず PreRoll を前段に通す（先制フラグ同期の不変条件）

`EnemyTurn()` を呼ぶ全経路は、同一ターン内で必ず `PreRollEnemyAction()` を経由する設計。
PreRoll が `pendingEnemyAction` / `isEnemyPreemptive` を毎回リセットしてから抽選し直すことで
先制フラグの同期が保たれている（現状この不変条件のみで担保。実バグは無し）。
- ⚠️ **PreRoll を経由せずに `EnemyTurn()` を呼ぶ新経路（状態異常だけ処理する割込ターン等）を
  作ると、先制フラグの同期が壊れ、敵行動のスキップや二重実行が起きうる。** EnemyTurn を呼ぶ
  新経路には必ず PreRoll を前段に置くこと。

## 3. 武器のデバフ付与はエディタフィルタが唯一の防壁

`Item.weaponInflictDebuff` は型としては全 `StatusEffect` を取りうるが、
`Assets/Editor/Itemdataeditor.cs` の `DrawFilteredEnumPopup`（述語 `IsBuffDebuffOrNone`）が
Inspector のドロップダウンをバフ/デバフ系＋None に限定している。状態異常（毒・麻痺等）は
別フィールド `weaponInflictEffect`（述語 `IsAilmentOrNone`）で処理する。
ランタイム消費側（`BattleSceneController_PlayerAction.cs` 765行付近）には
`IsBuffDebuff` ガードが無く、このエディタフィルタが唯一の防壁。
- ⚠️ フィルタ述語を変更・削除するとき、または `.asset` を YAML 直編集／Debug インスペクタで
  触るときは、`weaponInflictDebuff` にバフ/デバフ以外（毒等）を入れないこと。入れると
  `GetPairRef()` の default 経由で無診断のままプレイヤーの DEF バフに化ける。

## 4. 属性の日本語表記ルール

- 表示は `WeaponAttribute.ToJapanese()`（`AttributeTypes.cs`）が唯一の正規経路。
  正式表記: **殴 / 斬 / 突 / 炎 / 氷 / 雷 / 聖 / 闇 / 無**。
- **Fire は「炎」で統一**（「火」ではない）。属性ラベル（〜属性／〜耐性／〜魔法）は炎で書く。
  例外として固有名詞・演出（火の玉・火の剣・火炎放射器・「火に弱い」等の地の文）は
  自然な日本語として「火」のまま据え置く。
- **複数属性の並び順は 炎氷雷（→聖闇） / 殴斬突 で固定。**
- 注意: `.asset` の日本語は `\uXXXX` エスケープで保存される（生の漢字では grep に掛からない）。
  属性表記を一括確認するときはデコードして走査すること。
- `WeaponAttribute` enum の値自体は不変に保つこと（アセットは数値インデックスで保存しており、
  途中挿入すると既存スキル/アイテム設定が壊れる）。表示文字列だけ変えるのは安全。
