# PROJECT_MAP

AIアシスタント向け。プロジェクトの構造・各ファイルの役割・依存関係を記述する。
**セッション開始時にこのファイルを読めば、全ソース読み込み不要で改修対象を特定できる。**

最終更新: 2026-03-29（レベルアップシステム＆レベルドレイン実装完了後）

---

## 1. プロジェクト概要

スマートフォン向けノンフィールドRPG（Unity）。
塔を1歩ずつ進み、ランダムエンカウント・会話イベント・アイテム取得が発生する。

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
| Battle | ターン制戦闘 |
| Itembox | アイテム管理（街/戦闘中の両方から遷移） |
| Status | ステータス振り分け |
| Storage | 倉庫 |

---

## 3. ファイル構成と役割

### Assets/Script/ （ルート直下）

| ファイル | 役割 | 主な依存先 |
|---------|------|-----------|
| **GameState.cs** | シングルトン。全ゲーム状態を保持（HP/MP/ステータス/装備/状態異常/進行度/レベル/EXP）。DontDestroyOnLoad。サブステータス（Attack, Evasion等）はプロパティで装備+パッシブ込み計算。レベルアップ（GainExp/CalcExpToNext/CalcStatusPointGain）とレベルドレイン（ApplyLevelDrain）を管理 | EquipmentCalculator, PassiveCalculator, SaveManager |
| **TowerState.cs** | Tower シーンの進行管理。Advance()で1歩進み、イベント判定→エンカウント判定。毒ダメージ処理含む | GameState, StatusEffectSystem, TowerEventTrigger, TowerItemTrigger, EncounterSystem |
| **TowerEntranceView.cs** | 塔入口UI。到達階に応じたフロア選択ボタン表示 | GameState, FloorButton |
| **FloorButton.cs** | フロア選択ボタン1個分 | GameState |
| **HpMpDisplay.cs** | HP/MPバー表示 | GameState |
| **AttributeTypes.cs** | enum定義集: WeaponAttribute, StatusEffect, DamageCategory, ToJapanese()拡張メソッド | なし |
| **GameStateautocreate.cs** | GameState オブジェクトの自動生成 | GameState |

### Assets/Script/Battle/

| ファイル | 役割 | 主な依存先 |
|---------|------|-----------|
| **BattleSceneController.cs** | 戦闘メインコントローラー（partial class本体）。フィールド宣言、Start、ログ管理、UI制御、勝敗処理、FullRecover。OnVictory()で経験値付与・レベルアップログ表示 | GameState, Monster, BattleContext, StatusEffectSystem |
| **BattleSceneController_PlayerAction.cs** | partial: プレイヤー行動（通常攻撃/スキル/魔法/アイテム）。effectOnly対応、毒重複チェック含む | SkillData, StatusEffectSystem |
| **BattleSceneController_EnemyAction.cs** | partial: 敵行動（LUC判定/行動選択/各種攻撃/レベルドレイン/ターン終了）。effectOnly対応、毒重複チェック含む。ExecuteEnemyLevelDrain()で必中レベルドレイン処理 | MonsterSkillData, EnemyActionEntry, StatusEffectSystem, GameState |
| **BattleSceneController_CombatUtils.cs** | partial: 命中判定/クリティカル/防御ダイス/ダメージ適用 | GameState, Monster |
| **Monster.cs** | ScriptableObject。敵1体のマスターデータ（HP/ATK/DEF/回避/命中/毒耐性/行動パターン/Exp/Gold） | EnemyActionEntry |
| **MonsterSkillData.cs** | ScriptableObject。敵スキル1つのマスターデータ。effectOnlyフラグ対応済み。MonsterActionType enum定義（Idle/NormalAttack/SkillAttack/LevelDrain） | なし |
| **MonsterDatabase.cs** | 敵一覧。フロア/ステップに応じた出現候補検索 | Monster |
| **EnemyActionEntry.cs** | 敵行動テーブル1行分。threshold + MonsterSkillData参照 | MonsterSkillData |
| **EncounterSystem.cs** | エンカウント判定＋敵選択＋戦闘シーン遷移 | MonsterDatabase, BattleContext |
| **BattleContext.cs** | static。戦闘シーンへの敵データ受け渡し | Monster |

### Assets/Script/Skill/

| ファイル | 役割 | 主な依存先 |
|---------|------|-----------|
| **SkillData.cs** | ScriptableObject。スキル1つのマスターデータ（武器スキル/魔法スキル共用）。effectOnly/inflictEffect/inflictChance含む | なし |
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
| **Savemanager.cs** | セーブ/ロード/削除。JSON形式。ロード時は全回復+状態異常クリア。level/currentExp/expToNext/statusPoint はセーブ対象 | GameState, ItemBoxManager, StorageManager |

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

会話イベント関連（TalkEventDatabase, TalkEventRunner等）

### Assets/Script/Status/

| ファイル | 役割 | 主な依存先 |
|---------|------|-----------|
| **Statusview.cs** | ステータス画面UI。Status1（基礎）/Status2（詳細）切替、ポイント振り分け、リセット（広告確認ポップアップ付き）。必要経験値は残り（expToNext - currentExp）を表示 | GameState, AdManager |

### Assets/Script/Storage/

倉庫管理（StorageManager等）

---

## 4. 主要システムの設計ルール

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

### 敵スキルとプレイヤースキルの関係
- プレイヤー: SkillData（CT制 or MP制）
- 敵: MonsterSkillData（CT/MP無視）
- 共通フィールド: inflictEffect, inflictChance, effectOnly, baseHitRate, damageMultiplier, fixedDamage, damageCategory
- 敵専用: MonsterActionType.LevelDrain
- 将来的に統一予定だが、既存アセット互換のため現在は別クラス

### 街に戻る = 全回復ルール
- 敗北 → FullRecover() → Main
- ロード復帰 → ClearAllStatusEffects() + HP/MP全回復 → Main
- 帰還（TowerEntranceView→Main）は現状回復処理なし（塔から直接街に戻る導線が未実装のため）

---

## 5. セーブデータ構造 (SaveData)

floor, step, reachedFloor, level, currentExp, expToNext, HP/MP, 5基礎ステータス×2(current/initial),
statusPoint, equippedWeaponUid, isPoisoned, playedEventIds[], inventoryItems[], storageItems[]

---

## 6. 未実装・今後の予定

- 毒以外の状態異常（麻痺・睡眠等）のゲームロジック
- ゴールド報酬（Monster.Gold フィールドは既存、OnVictory()に付与処理追加が必要）
- 敵図鑑
- 毒ログのブラッシュアップ（蓄積表現等）
- SkillData / MonsterSkillData の完全統一
- 塔からの帰還ボタン（全回復して街へ）
- レベルドレイン耐性（装備/パッシブ対応）
- レベルアップ時のUI演出（SE・エフェクト）
- 経験値バランス調整（モンスターごとのExp値設計）