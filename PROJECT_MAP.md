# PROJECT_MAP

AIアシスタント向け。プロジェクトの構造・各ファイルの役割・依存関係を記述する。
**セッション開始時にこのファイルを読めば、全ソース読み込み不要で改修対象を特定できる。**

最終更新: 2026-03-31（スキル追加効果システム・カスタムPropertyDrawer・スキルビューアー）

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
| **BattleSceneController.cs** | 戦闘メインコントローラー（partial class本体）。フィールド宣言、Start、ログ管理、UI制御、勝敗処理、FullRecover。OnVictory()で経験値付与・レベルアップログ表示。ボス戦勝利時は撃破フラグ記録+勝利会話Talk遷移 | GameState, Monster, BattleContext, BossEncounterSystem, StatusEffectSystem |
| **BattleSceneController_PlayerAction.cs** | partial: プレイヤー行動（通常攻撃/スキル/魔法/アイテム）。IsNonDamage判定で非ダメージスキル対応。ProcessPlayerSkillEffects()で追加効果をSkillEffectProcessor経由で実行 | SkillData, SkillEffectProcessor, StatusEffectSystem |
| **BattleSceneController_EnemyAction.cs** | partial: 敵行動（LUC判定/行動選択/各種攻撃/ターン終了）。IsNonDamage判定で非ダメージスキル対応。ProcessEnemySkillEffects()で追加効果を実行。**SkillData統合済み** | SkillData, SkillEffectProcessor, EnemyActionEntry, StatusEffectSystem, GameState |
| **BattleSceneController_CombatUtils.cs** | partial: 命中判定/クリティカル/防御ダイス/ダメージ適用 | GameState, Monster |
| **BossEncounterSystem.cs** | ボスエンカウント管理 | GameState, Monster, BattleContext, TalkEvent |
| **Monster.cs** | ScriptableObject。敵1体のマスターデータ | EnemyActionEntry |
| **MonsterDatabase.cs** | 敵一覧。フロア/ステップに応じた出現候補検索 | Monster |
| **EnemyActionEntry.cs** | 敵行動テーブル1行分。threshold + **SkillData**参照。DamageCategory enumもここで定義 | SkillData |
| **EncounterSystem.cs** | 通常エンカウント判定＋敵選択＋戦闘シーン遷移 | MonsterDatabase, BattleContext |
| **BattleContext.cs** | static。戦闘シーンへの敵データ受け渡し | Monster |

**削除済み:**
- ~~MonsterSkillData.cs~~ — SkillData に統合。MonsterActionType enum は SkillData.cs 内に移動

### Assets/Script/Skill/

| ファイル | 役割 | 主な依存先 |
|---------|------|-----------|
| **SkillData.cs** | ScriptableObject。スキル1つのマスターデータ。**プレイヤー・モンスター統合**。プレイヤー用: skillSource/cooldownTurns/mpCost。モンスター用: actionType（Idle/NormalAttack/SkillAttack）。共通: skillAttribute/damageCategory/damageMultiplier/fixedDamage/baseHitRate。**追加効果リスト（additionalEffects）**で毒・レベルドレイン・回復等を任意に組み合わせ可能。IsNonDamage/HasAdditionalEffects ヘルパー付き。SkillSource enum, MonsterActionType enum もここで定義 | SkillEffectEntry |
| **SkillEffectData.cs** | 抽象基底 ScriptableObject。追加効果のジャンルマーカー。effectName/description | なし |
| **SkillEffectEntry.cs** | Serializable。追加効果1件分。effectData（SOジャンル参照）+ ailmentMode + targetStatusEffect + chance + intValue | SkillEffectData, AilmentMode, StatusEffect |
| **SkillEffectProcessor.cs** | static。バトル中に追加効果リストを実行。ProcessEffects()→ジャンル分岐→ProcessStatusAilment/ProcessLevelDrain/ProcessHeal。HealFormulaType対応 | SkillEffectEntry, StatusEffectSystem, GameState |
| **StatusAilmentEffectData.cs** | SkillEffectData継承。状態異常効果（付与/回復）のジャンルマーカー。AilmentMode enum もここで定義 | SkillEffectData |
| **HealEffectData.cs** | SkillEffectData継承。HP回復効果。formulaType（Fixed/MaxHpPercent/IntMultiplier/StrMultiplier）をSO側に持つ。HealFormulaType enum もここで定義 | SkillEffectData |
| **LevelDrainEffectData.cs** | SkillEffectData継承。レベルドレイン効果のジャンルマーカー | SkillEffectData |
| **StatusEffectSystem.cs** | 状態異常の付与判定・ダメージ計算・耐性取得の静的ユーティリティ | GameState, PassiveCalculator, EquipmentCalculator |
| **PassiveCalculator.cs** | パッシブ効果の集計 | ItemBoxManager, PassiveEffect |
| **PassiveEffect.cs** | パッシブ効果1件の定義 | なし |
| **EquipmentCalculator.cs** | 装備中武器のステータス補正取得 | GameState, ItemBoxManager |
| **EquipResistance.cs** | 装備品の属性耐性1件分 | なし |
| **EquipStatusEffectResistance.cs** | 装備品の状態異常耐性1件分 | なし |

**削除済み:**
- ~~PoisonEffectData.cs~~ — StatusAilmentEffectData に置き換え

### Assets/Script/Item/

| ファイル | 役割 | 主な依存先 |
|---------|------|-----------|
| **Item.cs** (ItemData) | ScriptableObject。アイテムマスターデータ | SkillData, PassiveEffect, EquipResistance, EquipStatusEffectResistance |
| **ItemBoxManager.cs** | シングルトン。インベントリ管理 | InventoryItem |
| **InventoryItem.cs** | インベントリ内のアイテム1個 | ItemData |
| **ItemboxContext.cs** | Itemboxシーン用コントローラー | StatusEffectSystem, ItemBoxManager |
| **ItemDetailPanel.cs** | アイテム詳細パネルUI | IItemContext |
| **ItemSlotView.cs** | アイテムスロット1個分のUI | InventoryItem |
| **ItemPickupWindow.cs** | アイテム取得ポップアップ | ItemData |
| **ItemDatabase.cs** | アイテム一覧 | ItemData |
| **TowerItemTrigger.cs** | 塔内でのアイテム取得判定 | ItemDatabase |
| **IItemContext.cs** | アイテム操作のインターフェース | なし |
| **OpenItemBoxButton.cs** | Itemboxシーンへの遷移ボタン | なし |

### Assets/Script/Save/

| ファイル | 役割 | 主な依存先 |
|---------|------|-----------|
| **Savemanager.cs** | セーブ/ロード/削除。JSON形式 | GameState, ItemBoxManager, StorageManager |

### Assets/Script/SceneGo/

| ファイル | 役割 |
|---------|------|
| **SceneLoader.cs** | シングルトン。シーン遷移ユーティリティ |
| **SceneLink.cs** | ボタンにアタッチしてシーン遷移 |
| **SceneLoaderAutoCreate.cs** | SceneLoader自動生成 |

### Assets/Script/Start/

| ファイル | 役割 |
|---------|------|
| **TitleManager.cs** | タイトル画面 |

### Assets/Script/Talk/

| ファイル | 役割 | 主な依存先 |
|---------|------|-----------|
| **TalkEvent.cs** | ScriptableObject。会話イベント1件 | EventCondition |
| **TalkEventDatabase.cs** | 会話イベント一覧 | TalkEvent |
| **TowerEventTrigger.cs** | 塔内の会話イベント発火判定 | TalkEventDatabase, GameState |
| **TalkRunner.cs** | Talk シーンのコントローラー | TalkEventDatabase, GameState |
| **EventConditon.cs** | 会話イベントの追加条件（抽象基底） | GameState |
| **TimeRangeCondition.cs** | 時間帯条件の実装 | EventCondition |

### Assets/Script/Status/

| ファイル | 役割 | 主な依存先 |
|---------|------|-----------|
| **Statusview.cs** | ステータス画面UI | GameState, AdManager |

### Assets/Script/Storage/

倉庫管理（StorageManager等）

### Assets/Editor/

| ファイル | 役割 | 主な依存先 |
|---------|------|-----------|
| **ItemDatabaseViewer.cs** | Item Database Viewer ウィンドウ | ItemDatabase, ItemData |
| **ItemDetailWindow.cs** | アイテム詳細ウィンドウ | ItemData, SkillData |
| **MonsterDatabaseViewer.cs** | Monster Database Viewer ウィンドウ。通常+ボスの2セクション | MonsterDatabase, Monster |
| **MonsterDetailWindow.cs** | モンスター詳細ウィンドウ。追加効果リスト表示対応 | Monster, SkillData, StatusAilmentEffectData, HealEffectData, LevelDrainEffectData |
| **SkillDatabaseViewer.cs** | Skill & Effect Viewer ウィンドウ。スキル一覧（Skilllistフォルダ）+エフェクト一覧（Skilleffectフォルダ）の2セクション構成 | SkillData, SkillEffectData |
| **SkillDetailWindow.cs** | スキル詳細ウィンドウ。全フィールド+追加効果パラメータ付き | SkillData, SkillEffectEntry, StatusAilmentEffectData, HealEffectData, LevelDrainEffectData |
| **SkillEffectDetailWindow.cs** | エフェクト詳細ウィンドウ。ジャンル固有情報+参照スキル逆引き | SkillEffectData, SkillData |
| **SkillEffectEntryDrawer.cs** | SkillEffectEntry のカスタム PropertyDrawer。effectData のジャンルに応じてフィールド表示を動的切替 | SkillEffectEntry, StatusAilmentEffectData, HealEffectData, LevelDrainEffectData |
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
- ボスモンスターは MonsterDatabase に登録しない
- 勝利会話の TalkEvent は ID を "BOSS_F{階:D2}_VICTORY" にする

### ステータス計算
- `GameState.Attack` = baseSTR × 1 + EquipmentCalculator(100%) + PassiveCalculator(減衰ルール)
- 他のサブステータスも同構造

### レベルアップシステム
- 必要経験値: レベル × 100
- レベルアップ時: statusPoint に (lv - 1) / 10 + 1 を加算
- 複数レベルアップ対応

### パッシブ重複ルール
- 同種効果複数: 最大値100% + 2個目以降は各値の10%加算

### スキルシステム（追加効果対応済み）
- **SkillData 1クラスでプレイヤー・モンスター両方のスキルを表現**
- プレイヤー用: skillSource（Weapon/Magic）, cooldownTurns, mpCost
- モンスター用: actionType（Idle/NormalAttack/SkillAttack）
- **非ダメージスキル判定**: damageMultiplier == 0 かつ fixedDamage == 0 → IsNonDamage == true
- **追加効果リスト（additionalEffects）**: SkillEffectEntry のリストで毒・レベルドレイン・回復等を任意組み合わせ
- バトル中の追加効果実行は SkillEffectProcessor.ProcessEffects() で一元処理

### 追加効果システム
- **ジャンル分離**: SkillEffectData（抽象SO）を継承した具象クラスでジャンルを表現
  - StatusAilmentEffectData: 状態異常（付与/回復）。ailmentMode + targetStatusEffect で制御
  - HealEffectData: HP回復。formulaType（Fixed/MaxHpPercent/IntMultiplier/StrMultiplier）で計算式決定
  - LevelDrainEffectData: レベルドレイン
- **パラメータはEntry側**: chance（発動率）、intValue（回復量/ドレイン量）をスキルごとに個別設定
- **カスタムPropertyDrawer**: effectData のジャンルに応じてインスペクター表示を動的切替
- **新効果追加手順**: SOクラス作成→Processor追加→Drawer追加→SOアセット作成→スキルに設定

### 街に戻る = 全回復ルール
- 敗北 → FullRecover() → Main
- ロード復帰 → ClearAllStatusEffects() + HP/MP全回復 → Main

---

## 5. データ管理ルール

### アイテム
- ID: `C001_Yakusou`, `W001_Bokutou`, `M001_Fire`
- フォルダ: `Assets/ScriptableAsset/Itemlist/`

### モンスター
- 通常ID: `001_Slime` / ボスID: `F03B_Boslime`
- フォルダ: `Assets/ScriptableAsset/Monsterlist/Normal/`, `Boss/`

### スキル
- ID: `001_Strattack`
- フォルダ: `Assets/ScriptableAsset/Skilllist/`

### エフェクト
- フォルダ: `Assets/ScriptableAsset/Skilleffect/`
- 各ジャンル原則1アセット（HealEffectData は formulaType 別に複数作成可能）

---

## 6. セーブデータ構造 (SaveData)

floor, step, reachedFloor, level, currentExp, expToNext, HP/MP, 5基礎ステータス×2(current/initial),
statusPoint, equippedWeaponUid, isPoisoned, playedEventIds[], inventoryItems[], storageItems[]

---

## 7. 未実装・今後の予定

### 次回優先
- 追加効果システムの動作確認（スキル付き武器・アイテム導入）
- 未実装の状態異常（麻痺・睡眠等）

### その他の残課題
- 塔からの帰還ボタン
- 逃げるコマンド（ボス戦逃走不可）
- ゴールド報酬（付与処理未実装）
- ボス戦専用BGM / 演出
- 武器の毒付与を SkillEffectData に統合（現在は Item.weaponInflictEffect で独立管理）
- 敵図鑑
- レベルアップ時のUI演出