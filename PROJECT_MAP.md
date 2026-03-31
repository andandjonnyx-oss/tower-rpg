# PROJECT_MAP

リポジトリ: andandjonnyx-oss/tower-rpg
最終更新: 2026-04-01

---

## ディレクトリ構成

```
Assets/
├── Editor/                          ← エディタ専用スクリプト（ビューアー/カスタムDrawer）
│   ├── ItemDatabaseViewer.cs
│   ├── ItemDetailWindow.cs
│   ├── MonsterDatabaseViewer.cs
│   ├── MonsterDetailWindow.cs
│   ├── SkillDatabaseViewer.cs        (Skilldatabaseviewer.cs)
│   ├── SkillDetailWindow.cs          (Skilldetailwindow.cs)
│   ├── SkillEffectDetailWindow.cs    (Skilleffectdetailwindow.cs)
│   ├── SkillEffectEntryDrawer.cs
│   └── TalkEventViewerWindow.cs
│
├── Script/
│   ├── AttributeTypes.cs            ← 属性enum（WeaponAttribute, StatusEffect等）
│   ├── FloorButton.cs               ← 塔入口のフロア選択ボタン
│   ├── GameState.cs                  ← グローバル状態管理（シングルトン）
│   ├── GameStateautocreate.cs        ← GameState自動生成
│   ├── HpMpDisplay.cs               ← HP/MP表示UI
│   ├── TowerEntranceView.cs          ← 塔入口画面
│   ├── TowerState.cs                 ← 塔内の階/ステップ管理 + 毒ダメージ
│   ├── DebugSceneManager.cs          ← ★新規: デバッグシーン用コントローラー
│   ├── MainSceneRecovery.cs          ← ★新規: メインシーン全回復
│   │
│   ├── Battle/
│   │   ├── BattleContext.cs          ← 戦闘受け渡し用static（★変更: デバッグフラグ追加）
│   │   ├── BattleSceneController.cs  ← 戦闘メイン（★変更: デバッグ戦闘対応）
│   │   ├── BattleSceneController_PlayerAction.cs  ← プレイヤー行動
│   │   ├── BattleSceneController_EnemyAction.cs   ← 敵行動
│   │   ├── BattleSceneController_CombatUtils.cs   ← 命中/クリティカル/ダメージ計算
│   │   ├── BossEncounterSystem.cs    ← ボスエンカウント管理
│   │   ├── EncounterSystem.cs        ← 通常エンカウント管理
│   │   ├── EnemyActionEntry.cs       ← 敵行動テーブルエントリ
│   │   ├── Monster.cs                ← モンスターSO定義
│   │   └── MonsterDatabase.cs        ← モンスターDB（SO）
│   │
│   ├── Item/
│   │   ├── Item.cs                   ← アイテムSO定義（ItemData）
│   │   ├── ItemDatabase.cs           ← アイテムDB（SO）
│   │   ├── ItemBoxManager.cs         ← 所持品管理（シングルトン）
│   │   ├── ItemboxContext.cs         ← Itemboxシーン用コントローラー
│   │   ├── ItemDetailPanel.cs        ← アイテム詳細パネルUI
│   │   ├── ItemPickupWindow.cs       ← アイテム入手ウィンドウ
│   │   ├── ItemSlotView.cs           ← アイテムスロットUI
│   │   ├── InventoryItem.cs          ← 所持品1個分のデータ（uid + data + cooldown）
│   │   ├── IItemContext.cs           ← アイテム操作インターフェース
│   │   ├── OpenItemBoxButton.cs      ← アイテムボックスを開くボタン
│   │   └── TowerItemTrigger.cs       ← 塔でのアイテムイベント発火
│   │
│   ├── Save/
│   │   └── SaveManager.cs           ← セーブ/ロード管理
│   │
│   ├── SceneGo/
│   │   ├── SceneLink.cs             ← ボタン→シーン遷移
│   │   ├── SceneLoader.cs           ← シーンロードユーティリティ（シングルトン）
│   │   └── SceneLoaderAutoCreate.cs  ← SceneLoader自動生成
│   │
│   ├── Skill/
│   │   ├── SkillData.cs              ← スキルSO定義（統合済み）
│   │   ├── SkillEffectData.cs        ← 追加効果ベースクラス（抽象SO）
│   │   ├── SkillEffectEntry.cs       ← スキル→効果の接続エントリ
│   │   ├── SkillEffectProcessor.cs   ← 追加効果の実行処理
│   │   ├── StatusAilmentEffectData.cs ← 状態異常効果SO
│   │   ├── HealEffectData.cs         ← 回復効果SO
│   │   ├── LevelDrainEffectData.cs   ← レベルドレイン効果SO
│   │   ├── PassiveCalculator.cs      ← パッシブ効果計算（重複ルール適用）
│   │   ├── PassiveEffect.cs          ← パッシブ効果定義
│   │   ├── EquipmentCalculator.cs    ← 装備品ステータス計算（100%反映）
│   │   ├── EquipResistance.cs        ← 装備属性耐性
│   │   ├── EquipStatusEffectResistance.cs ← 装備状態異常耐性
│   │   └── StatusEffectSystem.cs     ← 状態異常処理（毒ダメージ等）
│   │
│   ├── Start/
│   │   └── TitleManager.cs           ← タイトル画面管理
│   │
│   ├── Status/
│   │   └── （ステータス画面関連）
│   │
│   ├── Storage/
│   │   ├── StorageContext.cs         ← 倉庫シーン用コントローラー
│   │   └── StorageManager.cs         ← 倉庫データ管理（シングルトン）
│   │
│   └── Talk/
│       └── TalkRunner.cs等           ← 会話イベント関連
│
├── ScriptableAsset/
│   ├── ItemDatabase.asset
│   ├── MonsterDatabase.asset
│   ├── MainTalkDatabase.asset
│   ├── Itemlist/                     ← アイテムSOアセット（C001_Yakusou等）
│   ├── Monsterlist/                  ← モンスターSOアセット
│   │   └── （フロア帯別サブフォルダ）
│   ├── Skilllist/                    ← スキルSOアセット（001_Strattack等）
│   ├── Skilleffect/                  ← スキル効果SOアセット
│   │   ├── 001_HealFIX.asset
│   │   ├── 002_HealINT.asset
│   │   ├── 003_Joutaiijou.asset
│   │   └── 004_Leveldrain.asset
│   ├── Talklist/                     ← 会話イベントSOアセット
│   └── Talkcondition/                ← 会話条件SOアセット
│
└── Scenes/
    ├── Main                          ← メイン（街）シーン
    ├── Tower                         ← 塔探索シーン
    ├── Battle                        ← 戦闘シーン
    ├── Itembox                       ← 所持品シーン
    ├── Itemsouko                     ← 倉庫シーン
    ├── Talk                          ← 会話シーン
    ├── Status                        ← ステータス画面シーン
    └── Debug                         ← ★新規: デバッグシーン
```

---

## シングルトン一覧（DontDestroyOnLoad）

| クラス | 役割 |
|--------|------|
| GameState.I | グローバル状態（レベル/HP/MP/ステ振り/状態異常/イベント既読等） |
| SceneLoader.Instance | シーン遷移ユーティリティ |
| ItemBoxManager.Instance | 所持品管理 |
| StorageManager.Instance | 倉庫管理 |
| EncounterSystem.Instance | 通常エンカウント管理 |
| BossEncounterSystem.Instance | ボスエンカウント管理 |
| TowerItemTrigger.Instance | 塔アイテムイベント管理 |

## 静的クラス

| クラス | 役割 |
|--------|------|
| BattleContext | 戦闘受け渡しデータ（EnemyMonster, Floor, IsBossBattle, IsDebugBattle等） |
| SaveManager | セーブ/ロード |
| EquipmentCalculator | 装備品ステータス計算 |
| PassiveCalculator | パッシブ効果計算 |
| StatusEffectSystem | 状態異常処理 |

---

## データID規則

| 種別 | 書式 | 例 |
|------|------|-----|
| アイテム（消耗品） | C000_ | C001_Yakusou |
| アイテム（武器） | W000_ | W001_Bokutou |
| アイテム（魔法書） | M000_ | M001_Fire |
| モンスター（通常） | 000_ | 001_Slime |
| モンスター（ボス） | F00B_ | F03B_Boslime |
| スキル | 000_ | 001_Strattack |
| スキル効果 | 000_ | 001_HealFIX |