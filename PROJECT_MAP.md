# PROJECT_MAP

リポジトリ: andandjonnyx-oss/tower-rpg
Unity: 6000.3.9f1
最終更新: 2026-04-09（午後セッション）

---

## シーン構成

| シーン | 用途 | 主要スクリプト |
|--------|------|----------------|
| Main | 拠点画面 | MainSceneRecovery.cs |
| TowerEntrance | 塔入口（階選択） | TowerEntranceView.cs, FloorButton.cs |
| Tower | 塔探索（移動・イベント） | TowerState.cs, TowerEventTrigger, TowerItemTrigger, EncounterSystem.cs, BossEncounterSystem.cs |
| Battle | 戦闘 | BattleSceneController (4ファイル partial) |
| Itembox | アイテム管理 | ItemBoxManager, ItemboxContext |
| Storage | 倉庫 | StorageContext |
| Status | ステータス画面 | Statusview.cs |
| Talk | 会話イベント | TalkSceneController |
| Debug | デバッグ用 | DebugSceneManager.cs |

ループ: Main → TowerEntrance → Tower → Battle → Tower（またはMain）

---

## スクリプト一覧（主要ファイル）

### バトル関連（Assets/Script/Battle/）
| ファイル | 役割 | 今回の変更 |
|----------|------|-----------|
| BattleSceneController.cs | 戦闘メイン（UI初期化・状態管理） | `enemyForcedNextSkill` フィールド追加、初期化追加 |
| BattleSceneController_PlayerAction.cs | プレイヤー行動（攻撃/スキル/魔法/防御/アイテム） | 武器Rage付与case追加 |
| BattleSceneController_EnemyAction.cs | 敵行動（行動選択/LUC判定/ターン終了処理） | 強制行動チェック追加、スタン/麻痺キャンセル追加 |
| BattleSceneController_CombatUtils.cs | 共通計算（命中/防御ダイス/属性耐性） | 変更なし |
| BattleContext.cs | 戦闘コンテキスト（シーン間データ受け渡し） | 変更なし |
| Monster.cs | モンスターデータ定義 | 変更なし |
| MonsterDatabase.cs | モンスターDB | 変更なし |
| EnemyActionEntry.cs | 敵行動テーブルエントリ | 変更なし |
| EncounterSystem.cs | エンカウント判定 | 変更なし |
| Bossencountersystem.cs | ボスエンカウント | 変更なし |
| Enemyhpbar.cs | 敵HPバー（ルーペ所持時） | 変更なし |

### スキル関連（Assets/Script/Skill/）
| ファイル | 役割 | 今回の変更 |
|----------|------|-----------|
| SkillData.cs | スキルマスターデータ（SO） | `enemyNextForceSkill` フィールド追加 |
| SkillEffectEntry.cs | 追加効果エントリ | 変更なし |
| SkillEffectData.cs | 追加効果ベースクラス（SO） | 変更なし |
| SkillEffectProcessor.cs | 追加効果実行エンジン | 変更なし |
| StatusEffectSystem.cs | 状態異常判定・ダメージ計算 | `RageDuration` 3→4 |
| StatusAilmentEffectData.cs | 状態異常効果データ（SO） | 変更なし |
| HealEffectData.cs | HP回復効果データ（SO） | 変更なし |
| RecoilEffectData.cs | 反動ダメージ効果データ（SO） | 変更なし |
| SelfDestructEffectData.cs | 自爆効果データ（SO） | 変更なし |
| LevelDrainEffectData.cs | レベルドレイン効果データ（SO） | 変更なし |
| EquipmentCalculator.cs | 装備効果計算 | 変更なし |
| PassiveCalculator.cs | パッシブ効果計算 | 変更なし |
| EquipStatusEffectResistance.cs | 装備の状態異常耐性 | 変更なし |
| PassiveEffect.cs | パッシブ効果定義 | 変更なし |

### タワー関連（Assets/Script/）
| ファイル | 役割 | 今回の変更 |
|----------|------|-----------|
| TowerState.cs | 塔探索メイン | 魔法ログ自動消去コルーチン追加 |
| GameState.cs | ゲーム全体の状態管理 | 変更なし |
| MagicSelector.cs | 自作魔法選択ドロップダウン | Blocker方式の外部クリック閉じ追加 |
| HpMpDisplay.cs | HP/MP表示 | 変更なし |
| MainSceneRecovery.cs | Main復帰時の回復処理 | 変更なし |

### その他
| ファイル | 役割 | 今回の変更 |
|----------|------|-----------|
| AttributeTypes.cs | 属性・状態異常列挙型 | 変更なし |
| DebugSceneManager.cs | デバッグシーン管理 | 変更なし |

---

## ScriptableObject アセット構成

### スキル（Assets/ScriptableAsset/Skilllist/）
| ID | 名前 | 概要 |
|----|------|------|
| 001_Strattack | 通常攻撃（斬） | 武器スキル、斬×1.0 |
| 001a_Slaattack | 通常攻撃（殴） | 武器スキル、殴×1.0 |
| 001c_fireattack | 炎攻撃 | 武器スキル、炎×1.0（actionType要修正） |
| 002_Idle | 何もしない | Idle |
| 003_leveldrain | レベルドレイン | 敵専用、レベル-1 |
| 004_fireball | ファイアボール | 魔法、炎、固定ダメージ |
| 004a_fireball2 | ファイアボール2 | 魔法、炎、上位 |
| 005_lightning | ライトニング | 魔法、雷 |
| 006_poison | ポイズン | 魔法、毒付与 |
| 007_poisonattack | 毒攻撃（殴） | 武器スキル+毒 |
| 007_poisonattackkiri | 毒攻撃（斬） | 武器スキル+毒 |
| 007_poisonattacksasi | 毒攻撃（刺） | 武器スキル+毒 |
| 008_powerattackstr | パワーアタック（斬） | 武器スキル、斬×2.0 |
| 008a_powerattacksla | パワーアタック（殴） | 武器スキル、殴×2.0 |
| 009_heal | ヒール | 魔法、HP回復 |
| 009a_heal2 | ヒール2 | 魔法、HP回復上位 |
| 010_healint | ヒールINT | 魔法、INT依存回復 |
| 011_dokusasi | 毒刺し | 武器スキル+毒 |
| 012_bunmawasi | ぶん回し | 武器スキル |
| 013_999dame | 999ダメ | デバッグ用固定999 |
| 014_3renduki | 三連突き | 武器スキル、3回攻撃 |
| 015_crash | クラッシュ | 武器スキル+反動 |
| 016_raikiri | 雷切 | 武器スキル、雷属性 |
| 017_18attack | 1/8アタック | 武器スキル |
| 018_depoizu | デポイズ | 魔法、毒回復+HP回復 |
| 019_mthun | Mサンダー | 魔法、雷 |
| 020_noroiken | 呪い拳 | 武器スキル+反動 |
| 021_mahinattacksasi | 麻痺攻撃（刺） | 武器スキル+麻痺 |
| 022_mahi | パライズ | 魔法、麻痺付与 |
| 023_demahi | デパララ | 魔法、麻痺回復+HP回復 |
| 024_kurayaminaguri | ブラインドアタック | 武器スキル、殴×1.0、40%暗闇付与 |
| 025_blind | ブラインド | 魔法、非ダメージ、80%暗闇付与 |
| 026_dekura | デブライ | 魔法、暗闇回復+HP30回復 |
| 027_rage | レイジ | 非ダメージ、自己怒り付与100% |

### モンスター（Assets/ScriptableAsset/Monsterlist/）

#### F1-F10（Normal/F1-F10/）
001_Slime ～ 009 + 009b（レア）

#### F11-F20（Normal/F11-F20/）
| ID | 名前 | 概要 | 今回の変更 |
|----|------|------|-----------|
| 011_livesword | リビングソード | 斬属性 | — |
| 012_livetate | リビングシールド | 高防御 | — |
| 013_livekittin | リビングテーブル | — | — |
| 014_kemusi | ケムシ | 毒付与 | — |
| 015_koumori | 蝙蝠 | 暗闇付与、BlindResistance=100 | — |
| 016_ragetroll | レイジトロール | 怒り使用、14-18F | 新規作成 |

#### ボス（Monsterlist/Boss/）
F10B_Golem 等

---

## 敵行動選択システム

```
actionRange = CalcActionRange(playerLUC, enemyLUC, baseActionRange)
roll = Random.Range(0, actionRange)
→ actions[i].threshold を昇順走査、roll < threshold の最初のアクションを選択
```

- LUC有利: actionRange × 0.8（敵弱体化）
- LUC不利: actionRange × 1.2（敵強化）
- 怒り中: actions[0] 強制（actionRange=1と同等）
- **enemyForcedNextSkill != null: 次ターン強制スキル実行（スタン/麻痺でキャンセル）**

---

## 状態異常一覧

| 状態異常 | 方向 | 持続 | セーブ | 特記 |
|----------|------|------|--------|------|
| 毒 | 双方向 | 戦闘+塔 | ○ | 5%/3%ダメージ、10%自然治癒 |
| 気絶 | P→E | 1T | × | 行動不能、forcedSkillキャンセル |
| 麻痺 | 双方向 | 戦闘+塔 | ○ | 20%行動キャンセル、forcedSkillキャンセル |
| 暗闇 | 双方向 | 戦闘+塔 | ○ | P暗闇→E回避2倍、E暗闇→命中半分 |
| 怒り | 自己 | 4T(実質3T) | × | ATK×1.5、攻撃のみ、武器付与可 |

---

## 今回追加された主要システム

### enemyNextForceSkill（敵の次ターン強制行動）
- SkillData のフィールド。nullでなければ、そのスキルを使った次のターンに指定スキルを強制実行
- BattleSceneController 側に `enemyForcedNextSkill` で一時保持
- EnemyTurn() で麻痺チェック後・怒りチェック前に判定
- 気絶/麻痺キャンセル時にクリアされる
- 戦闘初期化時にリセット

### MagicSelector Blocker（外部クリック閉じ）
- ドロップダウン展開時に Canvas 直下に透明パネル（Blocker）を生成
- MagicSelector の直前の sibling index に配置 → listPanel より背面に描画
- Blocker クリックで CloseList() → Blocker 破棄
- リスト項目クリックは正常に処理される（listPanel が Blocker より前面）