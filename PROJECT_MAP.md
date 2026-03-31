# PROJECT_MAP

AIアシスタント向け。プロジェクトの構造・各ファイルの役割・依存関係を記述する。
**セッション開始時にこのファイルを読めば、全ソース読み込み不要で改修対象を特定できる。**

最終更新: 2026-03-31（データアーキテクチャ整備・SkillData統合・ビューアー改修）

---

## 1. プロジェクト概要

スマートフォン向けノンフィールドRPG（Unity）。
塔を1歩ずつ進み、ランダムエンカウント・会話イベント・アイテム取得・ボス戦が発生する。

コアループ: 街(Main) → 塔入口(TowerEntrance) → 塔(Tower) → 戦闘(Battle) → 塔に戻る
敗北・帰還・ロード復帰 → 街(Main)に戻り HP/MP/状態異常を全回復。

---

## 2. シーン構成

| シーン名 | 役割 |
|---------|------|
| Start | タイトル画面。セーブ有→ロード、無→ニューゲーム |
| Main | 街。全回復状態。塔入口・アイテムボックス・ステータスへ遷移 |
| TowerEntrance | 塔入口。到達階に応じた中間ポイント選択 |
| Tower | 塔内探索。1歩進むごとにイベント判定 |
| Battle | ターン制戦闘（通常戦闘・ボス戦共用） |
| Talk | 会話イベント再生（戦闘前/戦闘後のボス会話にも使用） |
| Itembox | アイテム管理（街/戦闘中の両方から遷移） |
| Status | ステータス振り分け |
| Storage | 倉庫 |

---

## 3. ファイル構成と役割

### Assets/Script/ （ルート直下）

| ファイル | 役割 | 主な依存先 |
|---------|------|-----------|
| **GameState.cs** | シングルトン。全ゲーム状態を保持（HP/MP/ステータス/装備/状態異常/進行度/レベル/EXP）。DontDestroyOnLoad。サブステータス（Attack, Evasion等）はプロパティで装備+パッシブ込み計算。レベルアップ（GainExp/CalcExpToNext/CalcStatusPointGain）とレベルドレイン（ApplyLevelDrain）を管理。イベント既読管理（IsPlayed/MarkPlayed）はボス撃破フラグにも流用 | EquipmentCalculator, PassiveCalculator, SaveManager |
| **TowerState.cs** | Tower シーンの進行管理。Advance()で1歩進み、①会話判定→②ボス判定→③アイテム判定→④エンカウント判定。毒ダメージ処理含む | GameState, StatusEffectSystem, TowerEventTrigger, BossEncounterSystem, TowerItemTrigger, EncounterSystem |
| **TowerEntranceView.cs** | 塔入口UI。到達階に応じたフロア選択ボタン表示 | GameState, FloorButton |
| **FloorButton.cs** | フロア選択ボタン1個分 | GameState |
| **HpMpDisplay.cs** | HP/MPバー表示 | GameState |
| **AttributeTypes.cs** | enum定義集: WeaponAttribute, StatusEffect, ToJapanese()拡張メソッド | なし |
| **GameStateautocreate.cs** | GameState オブジェクトの自動生成 | GameState |

### Assets/Script/Battle/

| ファイル | 役割 | 主な依存先 |
|---------|------|-----------|
| **BattleSceneController.cs** | 戦闘メインコントローラー（partial class本体）。フィールド宣言、Start、ログ管理、UI制御、勝敗処理、FullRecover。OnVictory()で経験値付与・レベルアップログ表示。ボス戦勝利時は撃破フラグ記録+勝利会話Talk遷移。ボス戦敗北時はSTEP維持 | GameState, Monster, BattleContext, BossEncounterSystem, StatusEffectSystem |
| **BattleSceneController_PlayerAction.cs** | partial: プレイヤー行動（通常攻撃/スキル/魔法/アイテム）。effectOnly対応、毒重複チェック含む | SkillData, StatusEffectSystem |
| **BattleSceneController_EnemyAction.cs** | partial: 敵行動（LUC判定/行動選択/各種攻撃/レベルドレイン/ターン終了）。effectOnly対応、毒重複チェック含む。ExecuteEnemyLevelDrain()で必中レベルドレイン処理。**SkillData統合済み（旧MonsterSkillData参照を全てSkillDataに変更）** | SkillData, EnemyActionEntry, StatusEffectSystem, GameState |
| **BattleSceneController_CombatUtils.cs** | partial: 命中判定/クリティカル/防御ダイス/ダメージ適用 | GameState, Monster |
| **BossEncounterSystem.cs** | ボスエンカウント管理。BossEntry（階/STEP/モンスター/勝利会話）のリストを保持。撃破判定はGameState.IsPlayed("BOSS_F{階:D2}")を流用。TryStartBossBattle()でボス戦開始 | GameState, Monster, BattleContext, TalkEvent |
| **Monster.cs** | ScriptableObject。敵1体のマスターデータ（HP/ATK/DEF/回避/命中/毒耐性/行動パターン/Exp/Gold）。IsBoss/IsUniqueフラグあり | EnemyActionEntry |
| **MonsterDatabase.cs** | 敵一覧。フロア/ステップに応じた出現候補検索。ボスはここに登録しない（BossEncounterSystem経由） | Monster |
| **EnemyActionEntry.cs** | 敵行動テーブル1行分。threshold + SkillData参照。**統合済み（旧MonsterSkillData→SkillData）**。DamageCategory enumもここで定義 | SkillData |
| **EncounterSystem.cs** | 通常エンカウント判定＋敵選択＋戦闘シーン遷移 | MonsterDatabase, BattleContext |
| **BattleContext.cs** | static。戦闘シーンへの敵データ受け渡し。IsBossBattle/BossFloorフラグでボス戦を識別 | Monster |

**削除済み:**
- ~~MonsterSkillData.cs~~ — SkillData に統合。MonsterActionType enum は SkillData.cs 内に移動

### Assets/Script/Skill/

| ファイル | 役割 | 主な依存先 |
|---------|------|-----------|
| **SkillData.cs** | ScriptableObject。スキル1つのマスターデータ。**プレイヤースキル（武器/魔法）とモンスタースキルを統合**。プレイヤー用: skillSource/cooldownTurns/mpCost。モンスター用: actionType（MonsterActionType enum）。共通: skillAttribute/damageCategory/damageMultiplier/fixedDamage/baseHitRate/inflictEffect/inflictChance/effectOnly。SkillSource enum, MonsterActionType enum もここで定義 | なし |
| **StatusEffectSystem.cs** | 状態異常の付与判定・ダメージ計算・耐性取得の静的ユーティリティ。プレイヤーの毒耐性は装備+パッシブ合算 | GameState, PassiveCalculator, EquipmentCalculator |
| **PassiveCalculator.cs** | パッシブ効果の集計（重複ルール: 最大値100%、2個目以降10%減衰）。魔法スキル一覧収集も担当 | ItemBoxManager, PassiveEffect |
| **PassiveEffect.cs** | パッシブ効果1件の定義。PassiveType enum含む | なし |
| **EquipmentCalculator.cs** | 装備中武器のステータス補正取得（100%反映）。状態異常耐性取得対応済み | GameState, ItemBoxManager |
| **EquipResistance.cs** | 装備品の属性耐性1件分のデータ構造 | なし |
| **EquipStatusEffectResistance.cs** | 装備品の状態異常耐性1件分のデータ構造 | なし |

### Assets/Script/Item/

| ファイル | 役割 | 主な依存先 |
|---------|------|-----------|
| **Item.cs** (ItemData) | ScriptableObject。アイテムマスターデータ。Consumable/Weapon/Magicの3カテゴリ。武器の状態異常耐性(equipStatusEffectResistances)対応済み | SkillData, PassiveEffect, EquipResistance, EquipStatusEffectResistance |
| **ItemBoxManager.cs** | シングルトン。インベントリ管理。DontDestroyOnLoad | InventoryItem |
| **InventoryItem.cs** | インベントリ内のアイテム1個。uid + ItemData参照 + スキルクールダウン管理 | ItemData |
| **ItemboxContext.cs** | Itemboxシーン用コントローラー。使う/装備/捨てる。毒消し対応済み | StatusEffectSystem, ItemBoxManager |
| **ItemDetailPanel.cs** | アイテム詳細パネルUI | IItemContext |
| **ItemSlotView.cs** | アイテムスロット1個分のUI | InventoryItem |
| **ItemPickupWindow.cs** | アイテム取得ポップアップ | ItemData |
| **ItemDatabase.cs** | アイテム一覧。フロア/ステップに応じた出現候補検索 | ItemData |
| **TowerItemTrigger.cs** | 塔内でのアイテム取得判定 | ItemDatabase |
| **IItemContext.cs** | アイテム操作のインターフェース | なし |
| **OpenItemBoxButton.cs** | Itemboxシーンへの遷移ボタン | なし |

### Assets/Script/Save/

| ファイル | 役割 | 主な依存先 |
|---------|------|-----------|
| **Savemanager.cs** | セーブ/ロード/削除。JSON形式。ロード時は全回復+状態異常クリア。level/currentExp/expToNext/statusPoint はセーブ対象。ボス撃破フラグはplayedEventIdsに含まれるため追加対応不要 | GameState, ItemBoxManager, StorageManager |

### Assets/Script/SceneGo/

| ファイル | 役割 |
|---------|------|
| **SceneLoader.cs** | シングルトン。シーン遷移のユーティリティ |
| **SceneLink.cs** | ボタンにアタッチしてシーン遷移 |
| **SceneLoaderAutoCreate.cs** | SceneLoader自動生成 |

### Assets/Script/Start/

| ファイル | 役割 |
|---------|------|
| **TitleManager.cs** | タイトル画面。スタート/ニューゲーム/初期化ボタン管理 |

### Assets/Script/Talk/

| ファイル | 役割 | 主な依存先 |
|---------|------|-----------|
| **TalkEvent.cs** | ScriptableObject。会話イベント1件のマスターデータ（id/floor/step/lines/conditions）。ボス勝利会話もこれで作る（ID命名規則: "BOSS_F{階:D2}_VICTORY"） | EventCondition |
| **TalkEventDatabase.cs** | 会話イベント一覧。floor/step索引とID索引の2系統 | TalkEvent |
| **TowerEventTrigger.cs** | 塔内の会話イベント発火判定。IsPlayed()で既読チェック | TalkEventDatabase, GameState |
| **TalkRunner.cs** | Talk シーンのコントローラー。pendingEventId で会話を再生し、終了後 Tower に戻る。ボス戦の知識は持たない（汎用） | TalkEventDatabase, GameState |
| **EventConditon.cs** | 会話イベントの追加条件（抽象基底クラス） | GameState |
| **TimeRangeCondition.cs** | 時間帯条件の実装 | EventCondition |

### Assets/Script/Status/

| ファイル | 役割 | 主な依存先 |
|---------|------|-----------|
| **Statusview.cs** | ステータス画面UI。Status1（基礎）/Status2（詳細）切替、ポイント振り分け、リセット（広告確認ポップアップ付き）。必要経験値は残り（expToNext - currentExp）を表示 | GameState, AdManager |

### Assets/Script/Storage/

倉庫管理（StorageManager等）

### Assets/Editor/

| ファイル | 役割 | 主な依存先 |
|---------|------|-----------|
| **ItemDatabaseViewer.cs** | Item Database Viewer ウィンドウ。自動登録ボタン（Itemlistフォルダ再帰検索）、IDソート（C→M→W順）、検索機能 | ItemDatabase, ItemData |
| **ItemDetailWindow.cs** | アイテム詳細ウィンドウ。カテゴリ別に全フィールド表示（消費:回復量+毒回復、武器:攻撃性能+装備補正+耐性、魔法:スキル+パッシブ） | ItemData, SkillData |
| **MonsterDatabaseViewer.cs** | Monster Database Viewer ウィンドウ。通常モンスター（DB登録）+ボスモンスター（Bossフォルダ直接参照）の2セクション構成。自動登録（Normalフォルダのみ）、IDソート、Boss リスト更新、検索機能 | MonsterDatabase, Monster |
| **MonsterDetailWindow.cs** | モンスター詳細ウィンドウ。全フィールド表示（ステータス+命中回避+状態異常耐性+行動パターン確率範囲付き） | Monster, SkillData |
| **TalkEventViewerWindow.cs** | 会話イベントビューアー | TalkEventDatabase, TalkEvent |

---

## 4. 主要システムの設計ルール

### STEP進行時の処理順序（TowerState.Advance）

```
① 会話イベント（TowerEventTrigger）→ 発火したら以降スキップ
② ボスエンカウント（BossEncounterSystem）→ 発火したら以降スキップ
③ アイテムイベント（TowerItemTrigger）→ 発火したら以降スキップ
④ 通常エンカウント（EncounterSystem）
```

### ボス戦システム

- 配置: BossEncounterSystem のインスペクターで BossEntry（階/STEP/モンスター/勝利会話）を設定
- 撃破判定: GameState.IsPlayed("BOSS_F{階:D2}") を流用（セーブ対応済み）
- フロー: ボスSTEPに到達 → 未撃破なら即戦闘 → 勝利で MarkPlayed + 勝利会話(Talk) → Tower
- 敗北: STEP維持で街に全回復帰還。再度来ればボス戦再発生
- ボスモンスターは MonsterDatabase に登録しない（通常エンカウントに出さないため）
- 勝利会話の TalkEvent は ID を "BOSS_F{階:D2}_VICTORY" にする（必須命名規則）
- ボス前の情報会話は通常の TalkEvent として前のSTEPに配置するだけ（コード変更不要）

### ステータス計算
- `GameState.Attack` = baseSTR × 1 + EquipmentCalculator(100%) + PassiveCalculator(減衰ルール)
- 他のサブステータスも同構造

### レベルアップシステム
- 必要経験値: レベル × 100（CalcExpToNext）
- レベルアップ時: statusPoint に CalcStatusPointGain(新レベル) を加算
  - Lv 2～10 → 1pt, Lv 11～20 → 2pt, Lv 21～30 → 3pt ...
  - 計算式: (lv - 1) / 10 + 1
- 複数レベルアップ対応（一度に大量EXP取得時）
- 経験値はOnVictory()でMonster.Expから付与

### レベルドレイン
- 敵スキル MonsterActionType.LevelDrain で発動
- 必中（命中判定なし、耐性なし）
- レベルを1下げる（レベル1以下にはならない）
- 経験値は0にリセット、必要経験値も再計算
- statusPoint は変更しない → 再レベルアップでポイント再取得可能

### パッシブ重複ルール
- 同種の効果が複数: 最大値100% + 2個目以降は各値の10%加算
- 例: [70, 50, 50] → 70 + 7 + 5 = 82

### 耐性計算（属性・状態異常共通）
- 合計 = 装備品分(100%反映) + パッシブ分(減衰ルール適用)
- 状態異常の実質付与率 = 基礎付与率 × (1 - 耐性/100)

### スキルシステム（統合済み）
- **SkillData 1クラスでプレイヤー・モンスター両方のスキルを表現**
- プレイヤー用フィールド: skillSource（Weapon/Magic）, cooldownTurns, mpCost
- モンスター用フィールド: actionType（Idle/NormalAttack/SkillAttack/LevelDrain）
- 共通フィールド: skillId, skillName, description, skillAttribute, damageCategory, damageMultiplier, fixedDamage, baseHitRate, inflictEffect, inflictChance, effectOnly
- プレイヤーが使う場合は actionType を参照しない
- モンスターが使う場合は skillSource/cooldownTurns/mpCost を参照しない
- 同じパラメータのスキルは複数モンスターで共有可能
- 属性・倍率が違えば別のスキルアセットとして作成

### 街に戻る = 全回復ルール
- 敗北 → FullRecover() → Main
- ロード復帰 → ClearAllStatusEffects() + HP/MP全回復 → Main
- 帰還（TowerEntranceView→Main）は現状回復処理なし（塔から直接街に戻る導線が未実装のため）

---

## 5. データ管理ルール（命名規則・フォルダ構成）

### アイテム
- ID/ファイル名: `C001_Yakusou`, `W001_Bokutou`, `M001_Fire`（英語、プレフィックス+番号+名前）
- 表示名: `itemName` フィールドに日本語（薬草、木刀、ファイア）
- フォルダ: `Assets/ScriptableAsset/Itemlist/` 以下に consume/Weapon/magic サブフォルダ
- DB登録: ビューアーの「自動登録」ボタンで Itemlist フォルダを再帰検索して一括登録

### モンスター
- 通常ID/ファイル名: `001_Slime`（番号+名前）
- ボスID/ファイル名: `F03B_Boslime`（フロア+B+名前）
- 表示名: `Mname` フィールドに日本語
- フォルダ: `Assets/ScriptableAsset/Monsterlist/Normal/`（F1-10等のサブフォルダ）, `Boss/`
- DB登録: Normal フォルダのみ自動登録。Boss はDB未登録（ビューアーでBossフォルダ直接参照で閲覧可能）

### スキル
- ID/ファイル名: `001_Strattack`（番号+名前）
- フォルダ: `Assets/ScriptableAsset/Skilllist/`
- 属性・倍率が違えば別アセット（例: 殴通常攻撃と斬通常攻撃は別）
- 同じパラメータのスキルは複数モンスターで共有可能

### ソート順
- アイテム: C → M → W のカテゴリ順、同カテゴリ内は番号順
- モンスター: ID文字列比較（001, 002...の番号順）
- 0始まりIDは文字列型のため問題なし

---

## 6. セーブデータ構造 (SaveData)

floor, step, reachedFloor, level, currentExp, expToNext, HP/MP, 5基礎ステータス×2(current/initial),
statusPoint, equippedWeaponUid, isPoisoned, playedEventIds[], inventoryItems[], storageItems[]

※ ボス撃破フラグ（"BOSS_F03" 等）は playedEventIds に含まれるため追加フィールド不要

---

## 7. 未実装・今後の予定

### 次回優先（本人希望）
- スキルシステムの修正（統合後の SkillData の調整・拡張）
- スキルビューアー（SkillData をビューアーで閲覧できるように）

### その他の残課題
- 塔からの帰還ボタン（全回復して街へ）
- 逃げるコマンド（ボス戦では逃走不可にする等）
- ゴールド報酬（Monster.Gold フィールドは既存、付与処理未実装）
- 毒以外の状態異常（麻痺・睡眠等）
- ボス戦専用BGM / 演出
- 敵図鑑
- レベルドレイン耐性（装備/パッシブ対応）
- レベルアップ時のUI演出（SE・エフェクト）
- 経験値バランス調整