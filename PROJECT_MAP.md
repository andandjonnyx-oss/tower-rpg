# PROJECT_MAP

最終更新: 2026-04-03

---

## ディレクトリ構成（主要部分）

```
Assets/
├── Script/
│   ├── Battle/
│   │   ├── BattleSceneController.cs          ← 戦闘メイン（UI・ログ・勝敗・防御フラグ）
│   │   ├── BattleSceneController_PlayerAction.cs ← プレイヤー行動（攻撃/スキル/魔法/防御/アイテム）
│   │   ├── BattleSceneController_EnemyAction.cs  ← 敵行動（LUC判定/各攻撃/防御対応/ターン終了）
│   │   ├── BattleSceneController_CombatUtils.cs  ← 命中/クリティカル/防御ダイス/属性耐性軽減
│   │   ├── BattleContext.cs                  ← 戦闘シーン間のデータ受け渡し
│   │   ├── Bossencountersystem.cs            ← ボスエンカウント制御
│   │   ├── EncounterSystem.cs                ← 通常エンカウント制御
│   │   ├── EnemyActionEntry.cs               ← 敵行動テーブル1件のデータ構造
│   │   ├── Enemyhpbar.cs                     ← 敵HPバー表示
│   │   ├── Monster.cs                        ← モンスターScriptableObject定義（属性耐性追加済み）
│   │   └── MonsterDatabase.cs                ← モンスターDB（フォルダスキャン自動登録）
│   │
│   ├── Item/
│   │   ├── Item.cs                           ← アイテムScriptableObject定義
│   │   ├── ItemBoxManager.cs                 ← アイテムボックス管理（capacity=20）
│   │   ├── ItemDatabase.cs                   ← アイテムDB
│   │   ├── ItemboxContext.cs                 ← Itemboxシーンのコンテキスト
│   │   ├── ItemDetailPanel.cs                ← アイテム詳細表示
│   │   ├── ItemPickupWindow.cs               ← 塔でのアイテム拾得ポップアップ
│   │   ├── ItemSlotView.cs                   ← アイテムスロットUI
│   │   ├── InventoryItem.cs                  ← 所持品1個（uid+スキルクールダウン管理）
│   │   ├── IItemContext.cs                   ← アイテム操作インターフェース
│   │   ├── OpenItemBoxButton.cs              ← アイテムボックス開くボタン
│   │   └── TowerItemTrigger.cs               ← 塔でのアイテムイベント発火
│   │
│   ├── Skill/
│   │   ├── SkillData.cs                      ← スキルScriptableObject定義（MonsterActionType含む）
│   │   ├── SkillEffectData.cs                ← 追加効果の基底クラス
│   │   ├── SkillEffectEntry.cs               ← 追加効果1件のデータ構造
│   │   ├── SkillEffectProcessor.cs           ← 追加効果の実行処理
│   │   ├── HealEffectData.cs                 ← 回復効果
│   │   ├── LevelDrainEffectData.cs           ← レベルドレイン効果
│   │   ├── StatusAilmentEffectData.cs        ← 状態異常付与効果
│   │   ├── EquipResistance.cs                ← 装備の属性耐性データ構造
│   │   ├── EquipStatusEffectResistance.cs    ← 装備の状態異常耐性データ構造
│   │   ├── EquipmentCalculator.cs            ← 装備品のステータス計算
│   │   ├── PassiveCalculator.cs              ← パッシブ効果計算（属性耐性含む）
│   │   ├── PassiveEffect.cs                  ← パッシブ効果データ構造
│   │   ├── StatusEffectSystem.cs             ← 状態異常（毒）処理
│   │   └── MonsterAttributeResistance.cs     ← ★新規: モンスター属性耐性データ構造
│   │
│   ├── Start/
│   │   └── TitleManager.cs                   ← タイトル画面（初期アイテム付与追加済み）
│   │
│   ├── Save/                                 ← セーブ/ロード関連
│   ├── SceneGo/                              ← シーン遷移関連
│   ├── Status/                               ← ステータス画面関連
│   ├── Storage/                              ← 倉庫関連
│   ├── Talk/                                 ← 会話イベント関連
│   │
│   ├── GameState.cs                          ← ゲーム状態管理（シングルトン）
│   ├── TowerState.cs                         ← 塔の進行管理
│   ├── AttributeTypes.cs                     ← 属性/状態異常/パッシブのenum定義
│   ├── DebugSceneManager.cs                  ← デバッグシーン管理
│   ├── HpMpDisplay.cs                        ← HP/MP表示
│   ├── MainSceneRecovery.cs                  ← メインシーン復帰時の全回復
│   ├── TowerEntranceView.cs                  ← 塔入口UI
│   └── FloorButton.cs                        ← 階層選択ボタン
│
├── ScriptableAsset/
│   ├── Monsterlist/
│   │   ├── Normal/F1-F10/                    ← 1〜10階の通常敵
│   │   │   ├── 001_Slime.asset
│   │   │   ├── 002_inpu.asset
│   │   │   ├── 003_goblin.asset
│   │   │   ├── 004_zonbie.asset
│   │   │   └── 005_mitubati.asset
│   │   └── Boss/                             ← ボス敵
│   │
│   ├── Skilllist/                            ← スキルデータ
│   │   ├── 001_Strattack.asset               ← 通常殴攻撃
│   │   ├── 001a_Slaattack.asset              ← ★新規: 通常斬攻撃
│   │   ├── 002_Idle.asset                    ← 待機
│   │   ├── 003_leveldrain.asset              ← レベルドレイン
│   │   ├── 004_fireball.asset                ← ファイアボール
│   │   ├── 005_lightning.asset               ← ライトニング
│   │   ├── 006_poison.asset                  ← ポイズン
│   │   ├── 007_poisonattack.asset            ← 毒攻撃
│   │   ├── 008_powerattackstr.asset          ← 強撃（殴）
│   │   ├── 008a_powerattacksla.asset         ← ★新規: 強撃（斬）
│   │   ├── 009_heal.asset                    ← ヒール
│   │   └── 010_healint.asset                 ← ヒール（INT）
│   │
│   ├── Itemlist/                             ← アイテムデータ
│   ├── Skilleffect/                          ← スキル追加効果アセット
│   ├── Talklist/                             ← 会話イベントデータ
│   └── Talkcondition/                        ← 会話条件データ
│
├── Editor/                                   ← エディタ拡張
├── Scenes/                                   ← シーンファイル（全10シーン）
├── Art/                                      ← 画像素材
└── Settings/                                 ← Unity設定
```

## 主要システム関連図

### 戦闘フロー
```
エンカウント → BattleContext にモンスター設定 → Battle シーン
  → プレイヤーターン（攻撃/スキル/魔法/防御/アイテム）
    → 属性耐性軽減（ApplyEnemyAttributeResistance）
    → 防御ダイス（RollDefenseDice）
  → 敵ターン（LUC判定で行動選択）
    → プレイヤー防御中なら防御力2倍+ダイス優遇
    → 属性耐性軽減（PassiveCalculator）
    → 防御ダイス
  → ターン終了（毒ダメージ → 勝敗判定）
```

### ダメージ計算フロー（プレイヤー→敵）
```
基礎ダメージ（STR+装備 or 固定値 or 倍率計算）
  → 属性耐性軽減（MonsterAttributeResistance）
  → クリティカル判定（成功なら防御無視・2倍）
  → 防御ダイス軽減（RollDefenseDice）
  → 最終ダメージ（最低1保証、完全無効なら0）
```

### enum定義（AttributeTypes.cs）
- WeaponAttribute: Strike/Slash/Pierce/Fire/Ice/Thunder/Holy/Dark
- StatusEffect: None/Poison/Paralyze/Sleep/Blind/Silence/Burn/Freeze/Stun
- MonsterActionType: Idle/NormalAttack/SkillAttack （次回 Preemptive 追加予定）
- SkillSource: Weapon/Magic
- DamageCategory: Physical/Magical
- PassiveType: AttributeResistance/StatBonus/AttributeAttackBonus/MaxHpBonus/MaxMpBonus/StatusEffectResistance/DefenseBonus/MagicDefenseBonus/AccuracyBonus/EvasionBonus/CriticalBonus

### 初期アイテム付与（TitleManager.cs）
```
新規開始 or 初期化 → GrantStartingItems()
  → startingItems配列のアイテムをItemBoxManagerに追加
  → 最初のWeaponを自動装備
```