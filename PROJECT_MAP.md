# PROJECT_MAP

リポジトリ: andandjonnyx-oss/tower-rpg
最終更新: 2026-04-01

---

## ディレクトリ構成

```
Assets/
├── Scenes/
│   ├── Battle.unity        … 戦闘シーン
│   ├── Debug.unity         … デバッグシーン
│   ├── Itembox.unity       … アイテムボックス
│   ├── Itemsouko.unity     … アイテム倉庫
│   ├── Main.unity          … メイン（街）
│   ├── Status.unity        … ステータス画面
│   ├── Talk.unity          … 会話シーン
│   ├── Title.unity         … タイトル
│   ├── Tower.unity         … 塔探索
│   └── Towerin.unity       … 塔入口
├── Script/
│   ├── Battle/
│   │   ├── BattleSceneController.cs              … 戦闘メイン（フィールド/Start/ログ/勝敗/ポップアップ）
│   │   ├── BattleSceneController_PlayerAction.cs … プレイヤー行動（攻撃/スキル/魔法/アイテム）
│   │   ├── BattleSceneController_EnemyAction.cs  … 敵行動（LUC判定/各種攻撃/ターン終了）
│   │   ├── BattleSceneController_CombatUtils.cs  … 命中/クリティカル/防御ダイス/ダメージ適用
│   │   ├── BattleContext.cs         … 戦闘シーン間データ受け渡し（static）
│   │   ├── Bossencountersystem.cs   … ボスエンカウント管理
│   │   ├── EncounterSystem.cs       … 通常エンカウント管理
│   │   ├── EnemyActionEntry.cs      … 敵行動テーブルエントリ
│   │   ├── Monster.cs               … モンスターScriptableObject
│   │   └── MonsterDatabase.cs       … モンスターDB（SO一覧管理）
│   ├── Item/
│   │   ├── Item.cs                  … アイテムScriptableObject（ItemData）
│   │   ├── InventoryItem.cs         … 所持品インスタンス（uid+スキルCD管理）
│   │   ├── ItemBoxManager.cs        … 所持品管理（シングルトン）
│   │   ├── ItemDatabase.cs          … アイテムDB
│   │   ├── ItemDetailPanel.cs       … アイテム詳細表示
│   │   ├── ItemPickupWindow.cs      … アイテム取得ウィンドウ
│   │   ├── ItemSlotView.cs          … アイテムスロットUI
│   │   ├── ItemboxContext.cs        … Itemboxシーン間コンテキスト
│   │   ├── IItemContext.cs          … アイテムコンテキストI/F
│   │   ├── OpenItemBoxButton.cs     … アイテムボックス開くボタン
│   │   └── TowerItemTrigger.cs      … 塔内アイテム取得トリガー
│   ├── Skill/                       … スキル関連（SkillData, SkillEffect系）
│   ├── Save/                        … セーブ/ロード（SaveManager等）
│   ├── SceneGo/                     … シーン遷移（SceneLink等）
│   ├── Start/                       … タイトル/初期化
│   ├── Status/                      … ステータス表示
│   ├── Storage/                     … 倉庫管理
│   ├── Talk/                        … 会話システム
│   ├── GameState.cs                 … グローバル状態管理（シングルトン）
│   ├── TowerState.cs                … 塔探索状態
│   ├── DebugSceneManager.cs         … デバッグシーン管理
│   ├── MainSceneRecovery.cs         … メインシーン全回復
│   ├── AttributeTypes.cs            … 属性enum定義
│   ├── HpMpDisplay.cs               … HP/MP表示
│   ├── FloorButton.cs               … フロア選択ボタン
│   ├── TowerEntranceView.cs         … 塔入口表示
│   └── GameStateautocreate.cs       … GameState自動生成
└── ScriptableAsset/
    ├── Itemlist/
    │   ├── consume/                 … 消費アイテムSO
    │   ├── magic/                   … 魔法アイテムSO
    │   └── Weapon/                  … 武器SO
    └── Monsterlist/                 … モンスターSO
```

---

## 主要クラス関係

### 戦闘システム（partial class 4分割）
- `BattleSceneController.cs` … フィールド宣言、Start、ログ管理（全件保持+ポップアップ）、ターンカウンター、勝敗処理、武器/魔法ユーティリティ
- `BattleSceneController_PlayerAction.cs` … OnAttackClicked/OnSkillClicked/OnMagicClicked/OnItemClicked + BeginPlayerTurn呼び出し
- `BattleSceneController_EnemyAction.cs` … EnemyTurn/LUC判定/行動選択/各種攻撃/AfterEnemyAction
- `BattleSceneController_CombatUtils.cs` … CheckPlayerHit/CheckEnemyHit/CheckPlayerCrit/RollDefenseDice/ApplyDamageToPlayer/GetPlayerDefense/GetEnemyDefense

### データ受け渡し
- `BattleContext` (static) … EnemyMonster, Floor, Step, IsBossBattle, BossFloor, IsDebugBattle, DebugReturnScene
- `GameState` (singleton) … level, exp, HP/MP, ステータス, 装備, 状態異常, フラグ管理

### アイテム管理
- `ItemData` (ScriptableObject) … マスターデータ（装備ステータス、スキル、パッシブ効果）
- `InventoryItem` … 所持品インスタンス（uid, スキルクールダウン）
- `ItemBoxManager` (singleton) … 所持品リスト管理、セーブ復元