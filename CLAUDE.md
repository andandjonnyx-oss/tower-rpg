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

## 5. UI スケーリング/シーン設計の規約（全シーン統一）

実機は **2400×1080（20:9）**。全シーンの CanvasScaler は
**Scale With Screen Size / Reference 1920×1080 / Match=1（高さ基準）** で統一。
Match=1 なので論理幅は端末アスペクトで変わる（2400×1080 では論理幅2400・高さ1080）。

- **背景は固定1920×1080・中央**（anchorMin/Max=(0.5,0.5), sizeDelta=(1920,1080),
  anchoredPosition=(0,0)）。ストレッチ背景は使わない。→ 20:9 では左右に各240pxの
  余白（ピラーボックス）が出るが、これは仕様。
- **コンテンツ/HUD は必ず中央アンカー基準で配置する。** 中央基準なら中央配置の背景と
  一致し、論理幅が変わっても位置がずれない。
- ⚠️ **横ストレッチ（anchorMin.x=0 / anchorMax.x=1）のコンテンツ枠を作らないこと。**
  論理幅が変わると枠幅が変わり、左/右揃えテキストやマージン付き枠の中身の開始位置が
  ずれる（例: 旧 kurezitto の左揃えテキストが2400で左へずれた）。コンテンツは
  中央アンカー＋固定幅にする。
- ⚠️ **端アンカー（anchor.x=0 または 1 の点アンカー）で HUD を画面端に固定しないこと。**
  Match1＠2400 では画面端＝中央1920背景の外（240px余白側）へ寄り、背景に対して位置がずれる。
- **例外（ストレッチ維持が正しいもの）**: 全画面オーバーレイ・ポップアップ・入力ブロッカー・
  全画面背景。画面全体を覆うべきなので 0,0–1,1 ストレッチのまま（中身は中央配置）。
  例: Tower の BlindOverlay/各Popup、Talk の Panel/Button、各 Itempickuproot/Blocker。
- **Option シーンは意図的な例外**: Match=0.5 のまま＋背景2枚（部屋背景＋SD立ち絵）を
  ストレッチ維持。現状で問題なしと確認済み（2026-06-17）。他シーンの規約を Option に
  機械的に適用しないこと。
- 注意: シーンのレイアウトを確認・一括変更するときは `.unity` の RectTransform
  （`m_AnchorMin`/`m_AnchorMax`/`m_SizeDelta`）を直接走査する。CanvasScaler の Match は
  `m_MatchWidthOrHeight`。

## 6. 魔法選択UIの接続点（MagicSelector を別UIに差し替えるとき）

魔法選択は `MagicSelector`（`MagicSelector.cs`）が UI、`MagicSelectionMemory`（static,
skillId 保持）が記憶、`PassiveCalculator.CollectMagicSkills()/CollectNoBattleMagicSkills()`
が絞り込み、を担う。消費側は Battle=`BattleSceneController`（`magicSkillList`）、
Tower=`TowerState`（`fieldMagicList`）。2026-06-17 にリスト形式→中央ポップアップ形式へ
作り替えたが、消費側は実質1行（`SetOptions`→`SetItems`）だけで済んだ。その理由＝守るべき不変条件:

- **接続点は `MagicSelector` の公開APIだけ**:
  `SetVisible / SetOptions / SetItems(List<SkillData>) / SetValue / Value /
  onValueChanged(event) / ForceClose / OptionCount / ClearOptions`。
  これらのシグネチャを保てば、記憶・右上表示・発動・絞り込みは無改修で流用できる。
- ⚠️ **最重要の不変条件**: 「selector の `Value`（index）＝ 消費側 `magicSkillList` /
  `fieldMagicList` の同じ index」。発動は `GetSelectedMagicSkill()`（Battle）/
  `OnFieldMagicClicked()`（Tower）が `Value` でこの並列リストを引く。記憶は
  `onValueChanged(index)` → 消費側が `list[index].skillId` を `MagicSelectionMemory` に保存。
  この index 対応を崩すと全経路が壊れる。UI を再度差し替えても**この対応だけは維持**すること。
- `onValueChanged` は **`event`**（消費側は `+=`/`-=` で登録）。`event` を外して
  フィールド化したり直接 Invoke させる設計に変えると消費側が壊れる。
- **アイコンは `SkillData` ではなく魔導書 `Item.icon` 側**にある。`SetItems(List<SkillData>)`
  のままアイコンを出すには `PassiveCalculator.GetMagicIcon(skill)`（skillId で所持 Item を
  引いて `Item.icon` を返す）で解決する。SkillData にアイコンを足す前提で組まないこと。
- ポップアップ枠は中央固定・ブロッカー（外タップ閉じ）は全画面ストレッチをコード生成
  （第5節の規約に整合）。`MagicSelector.SetVisible(false)` は孤立ポップアップ防止のため
  `ForceClose()` を内包している（ポップアップが Canvas 直下の別オブジェクトのため）。
- 注意: `.cs` のエンコーディングは第7節を参照（旧来はファイルごとに Shift-JIS/UTF-8 が
  混在していた。第4節「.asset は \uXXXX」とは別問題）。

## 7. ソースの文字コード規約（UTF-8 + BOM に統一する）

方針（2026-06-18 にユーザー指示で確定）: **`.cs` ソースは UTF-8 + BOM で保存する。**

- 理由: この環境（日本語 Windows）の Unity/コンパイラは **BOM 無しの `.cs` を OS コードページ
  （cp932）として読む**。そのため **BOM 無し UTF-8 で保存すると日本語が文字化け**する
  （コメントだけでなく `"あなた"` のような**画面表示される文字列リテラル**まで化ける）。
  UTF-8 + BOM なら OS コードページに依存せず正しく読まれる。**BOM 無し UTF-8 が唯一の地雷。**
- ⚠️ 現状はまだ移行途中で、`Assets/Script` 配下の**大多数（~75本）が Shift-JIS/CP932・BOM 無し**。
  一部が UTF-8+BOM、数本が純 ASCII（BOM 無しでも無害）。`MagicSelector.cs` 等は意図的に
  ASCII 維持されている。
- 既存の **Shift-JIS ファイルを編集するときは、その編集で UTF-8+BOM に変換する**こと
  （cp932 で読んで UTF-8+BOM で書き戻す）。**cp932 のままのファイルに UTF-8 バイトを書き足さない**
  （新規行だけ別エンコーディングになり文字化けする）。新規ファイルは最初から UTF-8+BOM。
- ツール注意: Read/Edit 系は UTF-8 前提で復号するため Shift-JIS ファイルは文字化けして見える。
  日本語の grep や置換の前に真のエンコーディングを確認する（`iconv -f CP932`）。
  安全な変換: `iconv -f CP932 -t UTF-8` → 先頭に BOM(`EF BB BF`) 付与、CRLF は維持。
- 一括移行する場合は作業ツリーをクリーンにし、**エンコーディング専用コミット**に分けること
  （ロジック差分と混ぜない）。
