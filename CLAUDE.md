# CLAUDE.md

このリポジトリで作業する将来の自分（および Claude）への注意書き。
（精査ログ: 2026-06-16〜17 に戦闘ロジック①③④と属性表記を精査）

このプロジェクトに共通する構造的特徴は「**実行時の安全性が、消費側コードの外にある
“ある関数/仕組みが必ず守られること”に一点依存している**」こと。新機能を足すときは
下記1〜3を必ず確認すること。

---

## 1. 戦闘の一時状態は「全部 static」＋リセット関数への一点依存（最重要）

戦闘の一時状態は `BattleSceneController`（partial）の **static フィールド 24 個**。
以下は 2026-08-22 に実コードから機械的に取得した全数（手書きで維持しないこと。
過去に一覧が実態とズレていたのが第1節末尾のバグの原因になった）。

| ファイル | フィールド |
|---|---|
| `BattleSceneController.cs` | `enemyCurrentHp` / `battleInitialized` / `currentTurnNumber` / `enemyIsPoisoned` / `enemyIsStunned` / `enemyIsParalyzed` / `enemyIsBlind` / `enemyIsSilenced` / `enemyRageTurn` / `playerRageTurn` / `isDefending` / `pendingEnemyAction` / `isEnemyPreemptive` / `enemyForcedNextSkill` |
| `BattleSceneController_EnemyAction.cs` | `turnLowHpMode` / `turnActionCount` |
| `BattleSceneController_Petrify.cs` | `enemyIsPetrified` / `enemyPetrifyTurns` / `enemyPetrifyMaxTurns` / `enemyPetrifyJustReachedZero` |
| `BattleSceneController_QuizBoss.cs` | `quizCorrectCount` / `quizWrongCount` / `isQuizAnswering` / `currentQuizData` |

再取得コマンド（一覧を疑ったらこれで突き合わせる）:

```bash
grep -nE "^\s*(private|public|protected|internal)\s+static\s+" Assets/Script/Battle/BattleSceneController*.cs
```

- **宣言時の初期化子（`= false` 等）は戦闘ごとには再実行されない。** static 初期化子は
  型の初回アクセス時に一度走るだけ。Domain Reload 無効時やビルドでは static は
  プレイをまたいで保持される。→「宣言で初期化しているから安全」は誤り。
- **実効的なリセットは2経路だけ:**
  - `ResetBattleStatics()`（戦闘終了処理）— `battleInitialized=false` に戻す。
    Petrify / QuizBoss / BuffDebuff は専用のリセット関数をここから呼んでいる。
  - 戦闘開始の初期化ブロック `if (!battleInitialized)` — 上記で false に戻っている前提。
  - 敵行動フラグ（`pendingEnemyAction` / `isEnemyPreemptive`）は
    `PreRollEnemyAction()` も毎ターン両者をリセットしている。
- ⚠️ **戦闘から離脱する新経路（逃走機能など）を作るときは、必ず `ResetBattleStatics()`
  を通すこと。** 飛ばすと `battleInitialized` が true のまま残り、次戦闘で初期化ブロック
  ごとスキップされ、敵HP・行動回数・状態異常が前戦闘から漏れる。

### ⚠️ 戦闘一時状態を「インスタンスフィールド」にしてはいけない（2026-08-22 に実バグ）

**戦闘中に Battle シーンは破棄されうる。** アイテム使用・装備変更は `ItemBox` シーンを
経由するため（`OpenItemBoxButton` → `ItemboxContext` が `LoadScene("Battle")` で復帰）、
`BattleSceneController` は**別インスタンスとして作り直される**。`DontDestroyOnLoad` は無い。

そのため**インスタンスフィールドに置いた戦闘状態は、アイテム画面を開いて閉じるだけで
無診断に消える**。static ならシーンを跨いで生存する。**だから全部 static で揃えている。**

実際に踏んだバグ: `enemyForcedNextSkill`（力溜め等の予約）だけが非 static だったため、
`enemyNextForceSkill` チェーン（下記7件）が装備/アイテム経由で全てキャンセルされていた。
プレイヤー不利にも有利にも働き、**力溜めを見てから装備画面を開けばラスボスの大技を
確実に不発にできる**状態だった。

`enemyNextForceSkill` を持つスキル（テスト観点）:

| 起点 | 予約される次の行動 |
|---|---|
| はかいこうせん準備 | はかいこうせん |
| はかいこうせん | 待機 |
| カウントダウン4 → 3 → 2 → 1 | 最終的に 自爆 |
| 力を溜めている（フェゴール） | 破壊の一撃 |

**回帰テスト**: 上記いずれかのチェーン中に「アイテムを使う／装備を変更する」を挟み、
予約された行動が消えずに実行されることを確認する。

なお `BattleContext` も `public static class` なので、退避先としての耐久性は static
フィールドと同じ（アプリ終了で消える点は改善しない）。`SaveData`（`Savemanager.cs`）に
戦闘状態は一切含まれておらず**戦闘中セーブ／再開は存在しない**ため、static で十分。
将来「戦闘中セーブ」を実装する場合は、この 24 個をまとめてシリアライズする設計が必要。

## 2. 敵ターンは必ず PreRoll を前段に通す（先制フラグ同期の不変条件）

`EnemyTurn()` を呼ぶ全経路は、同一ターン内で必ず `PreRollEnemyAction()` を経由する設計。
PreRoll が `pendingEnemyAction` / `isEnemyPreemptive` を毎回リセットしてから抽選し直すことで
先制フラグの同期が保たれている（現状この不変条件のみで担保。実バグは無し）。
- **予約行動（`enemyForcedNextSkill`）があるターンは PreRoll が抽選しない。**
  `EnemyTurn()` は予約を最優先で消化するため、抽選すると「抽選結果と実際の行動が
  食い違う」。特に抽選で**先制技が出ると `ExecutePreemptiveIfNeeded()` が予約を見ずに
  先制を実行し、`EnemyTurn()` は「先制済み」と判定して予約を消化しない**（予約が残り続ける）。
  `PreRollEnemyAction()` は `SnapshotTurnActionMode()` の直後に予約チェックを置き、
  予約があれば抽選せずに return する。この順序を崩さないこと。
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

- 理由: **BOM 無しファイルの文字コード推定はプラットフォームで違う。** 日本語 Windows の
  Unity/コンパイラは **BOM 無しの `.cs` を OS コードページ（cp932）として読み**、
  macOS の Unity は **UTF-8 として読む**。つまり BOM が無いと、
  **BOM 無し UTF-8 は Windows で文字化けし、Shift-JIS は macOS でコンパイルエラーになる**
  （コメントだけでなく `"あなた"` のような**画面表示される文字列リテラル**まで壊れる）。
  UTF-8 + BOM なら**どちらの OS でも正しく読まれる**。これが両環境で唯一安全な形式。
- ✅ **移行は完了済み（2026-08-20）。** Mac 導入時に macOS 側で全ファイルがコンパイル
  エラーになったのを機に、Shift-JIS の 88本と「日本語を含む BOM 無し UTF-8」の1本を
  一括変換した（エンコーディング専用コミット）。
  **維持すべき不変条件は「Shift-JIS がゼロ」かつ「日本語を含む BOM 無しファイルがゼロ」。**
  2026-08-20 時点の実測は `Assets` 配下の `.cs` 134本中、BOM 付き UTF-8 + LF が130本、
  純 ASCII が4本。`MagicSelector.cs` 等の ASCII 4本は BOM 無しでも無害なので据え置いている。
- 新規ファイルは最初から UTF-8+BOM で作ること。
  ⚠️ **既存ファイルに追記するときは、そのファイルの現在のエンコーディングを確認してから
  書く。** 別エンコーディングのバイトを混ぜると、その行だけ文字化けする。
- ツール注意: Read/Edit 系は UTF-8 前提で復号するため Shift-JIS ファイルは文字化けして見える。
  日本語の grep や置換の前に真のエンコーディングを確認する（`iconv -f CP932`）。
  安全な変換: `iconv -f CP932 -t UTF-8` → 先頭に BOM(`EF BB BF`) 付与、CRLF は維持。
- 一括移行は作業ツリーをクリーンにし、**エンコーディング専用コミット**に分けること
  （ロジック差分と混ぜない）。2026-08-20 の移行もこの方針で 89ファイルを単独コミットした。
  変換後は **git の旧 blob を cp932 でデコードして新ファイルと文字列比較**し、
  内容が1文字も変わっていないことを検証すること（バイト差分が全面的に出るため、
  目視レビューでは変質を検出できない）。

## 8. iOS ビルド（詳細は IOS_BUILD.md）

2026-08-19〜20 に iOS 対応を実施。**手順・環境・踏んだ罠・残作業は `IOS_BUILD.md` に集約**
しているので、iOS 関連の作業前に必ずそちらを読むこと。ここには他節に影響する点だけ記す。

- ⚠️ **`AdManager.InitializeAdsIfAllowed()` は初期化を直接行わず、`volatile bool` を立てて
  `Update()` がメインスレッドで拾う構造に変えた。** UMP のコールバックがバックグラウンド
  スレッドで来ることがあり、そこから `StartCoroutine`（ATT の待機に必要）を呼べないため。
  **この変更は Android にも影響する**（広告初期化が1フレーム遅れる）。広告まわりを触るときは
  この間接化を前提にすること。
- ⚠️ ATT の説明文は `Assets/Editor/IosPostProcessBuild.cs` の定数と
  `GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset` の**2箇所にある。両方を揃えること。**
- ⚠️ **Unity の iOS ビルドは既存の出力フォルダに追記（Append）する。** Target Device や
  Bundle ID などプロジェクト構造に影響する設定を変えたら、出力先を削除するか Replace を選ぶ。
  古い参照が残ってビルドが落ちる。
- iOS は **iPhone Only**（`targetDevice: 0`）。第5節の「1920×1080 中央固定」設計が
  iPad の 4:3 では想定外の余白配分になるため。後から Universal への変更は可能。
- iOS 関連のビルドは **Mac でしかできない**。コード修正は Windows でも可能。

## 9. セーブ/設定は遅延コミット方式（SaveCommitter への一点依存）

2026-08-30 に即時セーブ方式から変更（Switch 移植の土台）。構造は第1節と同型で、
「安全性が消費側コードの外の一点に依存している」ことに注意。

- `SaveManager.Save()`（48箇所）は **dirty フラグを立てるだけ**。実書き込みは
  `CommitIfDirty()` で、`SaveCommitter`（自動生成・常駐）が安全地点
  （シーン遷移・シーン到着1フレーム後・アプリ休止/フォーカス喪失/終了）で呼ぶ。
- 設定（音量4種＋GameSettings 4種）も同方式。`SettingsStore.Data` を書き換えたら
  **必ず `MarkDirty()`**。忘れるとメモリ上では効くのに次回起動で巻き戻る
  （テストで気付きにくい典型バグ）。
- **等価性の不変条件（崩さないこと）**:
  - `HasSaveData()` / `Load()` は冒頭で `CommitIfDirty()`（flush）する。
    「Save() 直後に読む」既存コード（TitleManager の Load→Save→Load 連鎖等）は
    この flush があるから即時セーブ時代と等価。
  - `DeleteSave()` は **dirty を先にクリアしてから**消す。順序を崩すと
    削除後の次回コミットでファイルが復活する。
- 物理I/Oは `ISaveBackend`（既定 `FileSaveBackend`、tmp 経由のアトミック書き込み）に
  隔離。**Switch 対応は `SaveBackend.Instance` の差し替えだけで行い、
  SaveManager/SettingsStore にプラットフォーム分岐を持ち込まないこと。**
- ⚠️ 1シーンに長居する区間はディスク未反映。クラッシュ耐性が特に必要な新機能では
  `SaveManager.CommitIfDirty()` を明示で呼ぶ（多重呼び出しは無害）。
- ⚠️ `PlayerPrefs` を新規コードで使わないこと（Switch に相当機能が無い）。
  旧キーからの移行は `SettingsStore.MigrateFromPlayerPrefs()` が一度だけ行う。
