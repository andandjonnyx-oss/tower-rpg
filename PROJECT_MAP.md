# PROJECT_MAP

リポジトリ: andandjonnyx-oss/tower-rpg
更新日: 2026-04-04

---

## シーン構成
```
Title → Main（街） → TowerEntrance → Tower（探索） → Battle（戦闘）
                                                    → Talk（会話イベント）
                   → Status（ステータス振り分け）
                   → Itembox（アイテム管理）
                   → Itemsouko（アイテム倉庫）
                   → Debug（デバッグ）
```

## スクリプト構成

### Assets/Script/Battle/
| ファイル | 役割 |
|---------|------|
| BattleSceneController.cs | 戦闘メイン。UI・ログキュー・勝敗処理・先制抽選 |
| BattleSceneController_PlayerAction.cs | プレイヤー行動。攻撃/スキル/魔法/防御/先制割り込み |
| BattleSceneController_EnemyAction.cs | 敵行動。LUC判定/各種攻撃/先制チェック/ターン終了 |
| BattleSceneController_CombatUtils.cs | 命中/クリティカル/防御ダイス/属性耐性/ダメージ適用 |
| BattleContext.cs | 戦闘シーン間データ受け渡し（static） |
| Monster.cs | モンスターSO定義（ステータス/行動パターン/属性耐性） |
| MonsterDatabase.cs | モンスターDB（フォルダスキャン自動登録） |
| EncounterSystem.cs | エンカウント判定（20%/STEP） |
| Bossencountersystem.cs | ボス戦制御（撃破フラグ/勝利Talk） |
| EnemyActionEntry.cs | 敵行動テーブルエントリ + DamageCategory enum |
| Enemyhpbar.cs | 敵HPバー表示 |

### Assets/Script/Skill/
| ファイル | 役割 |
|---------|------|
| SkillData.cs | スキルSO定義 + MonsterActionType/SkillSource enum |
| SkillEffectData.cs | 追加効果基底クラス（abstract SO） |
| SkillEffectEntry.cs | 追加効果エントリ（パラメータ保持） |
| SkillEffectProcessor.cs | 追加効果実行（毒/レベドレ/回復/自爆） |
| SelfDestructEffectData.cs | 自爆エフェクトSO |
| HealEffectData.cs | 回復エフェクトSO |
| LevelDrainEffectData.cs | レベルドレインエフェクトSO |
| StatusAilmentEffectData.cs | 状態異常エフェクトSO |
| StatusEffectSystem.cs | 状態異常中央管理（毒判定/ダメージ計算） |
| PassiveCalculator.cs | パッシブ効果計算/魔法スキル収集/属性耐性合算 |
| PassiveEffect.cs | パッシブ効果データ |
| EquipmentCalculator.cs | 装備ステータス計算 |
| EquipResistance.cs | 装備属性耐性 |
| EquipStatusEffectResistance.cs | 装備状態異常耐性 |
| MonsterAttributeResistance.cs | モンスター属性耐性（attribute + value） |

### Assets/Script/ (ルート)
| ファイル | 役割 |
|---------|------|
| GameState.cs | ゲーム全体の状態管理（HP/MP/レベル/EXP/セーブ） |
| GameStateautocreate.cs | GameState自動生成 |
| AttributeTypes.cs | WeaponAttribute enum + ToJapanese拡張 |
| TowerState.cs | タワー探索制御 |
| TowerEntranceView.cs | タワー入口UI |
| FloorButton.cs | 階選択ボタン |
| HpMpDisplay.cs | HP/MP表示 |
| MainSceneRecovery.cs | メインシーン復帰時の全回復 |
| DebugSceneManager.cs | デバッグシーン制御 |

### Assets/Editor/
| ファイル | 役割 |
|---------|------|
| SkillEffectEntryDrawer.cs | 追加効果インスペクター表示（ジャンル別） |
| ItemDatabaseViewer.cs / ItemDetailWindow.cs | アイテムDB閲覧ツール |
| MonsterDatabaseViewer.cs / MonsterDetailWindow.cs | モンスターDB閲覧ツール |
| Skilldatabaseviewer.cs / Skilldetailwindow.cs / Skilleffectdetailwindow.cs | スキルDB閲覧ツール |
| TalkEventViewerWindow.cs | 会話イベント閲覧ツール |

## ScriptableAsset 構成
```
Assets/ScriptableAsset/
  ItemDatabase.asset          ← アイテムDB
  MonsterDatabase.asset       ← モンスターDB
  MainTalkDatabase.asset      ← 会話イベントDB
  Itemlist/
    consume/                  ← 消費アイテム（C001_Yakusou等）
    magic/                    ← 魔法アイテム（M001_Fire等）
    Weapon/                   ← 武器（W001_Bokutou等）
  Monsterlist/
    Normal/F1-F10/            ← 1〜10階通常敵（001〜005作成済み）
    Boss/                     ← ボス敵（未作成）
  Skilllist/                  ← スキルデータ（001〜010 + 008a作成済み + 009_PoisonSting）
  Skilleffect/                ← スキル効果SO（001〜005: HealFIX/HealINT/状態異常/レベドレ/自爆）
  Talklist/                   ← 会話イベントデータ
  Talkcondition/              ← 会話発生条件
```

## 作成済みモンスター（F1-F10）
| ID | 名前 | 出現階 | HP | 特徴 |
|----|------|--------|-----|------|
| 001_Slime | スライム | 1-5 | 20 | 殴・突半減 |
| 002_inpu | インプ | 1-5 | 15 | Fire50%耐性、ファイアボール |
| 003_goblin | ゴブリン | 1-5 | 30 | 斬攻撃+強撃 |
| 004_zonbie | ゾンビ | 4-8 | 70 | Fire5倍弱点、半分待機 |
| 005_mitubati | ミツバチ | 4-8 | 15 | 回避60、先制毒の一刺し+自爆 |

## 作成済みスキル
| ID | 名前 | タイプ | 備考 |
|----|------|--------|------|
| 001_Strattack | 通常殴攻撃 | NormalAttack | 殴属性 |
| 001a_Slaattack | 通常斬攻撃 | NormalAttack | 斬属性 |
| 002_Idle | 様子を見ている | Idle | |
| 003_leveldrain | レベルドレイン | SkillAttack | 非ダメージ+追加効果 |
| 004_fireball | ファイアボール | SkillAttack | Fire固定10dmg |
| 005_lightning | ライトニング | SkillAttack | Lightning |
| 006_poison | 毒攻撃（効果のみ） | SkillAttack | 非ダメージ+毒付与 |
| 007_poisonattack | 毒攻撃（ダメージ付） | SkillAttack | ダメージ+毒付与 |
| 008_powerattackstr | 強撃（殴） | SkillAttack | 倍率2.0 CT3 |
| 008a_powerattacksla | 強撃（斬） | SkillAttack | 倍率2.0 CT3 |
| 009_heal | HP回復（固定値） | SkillAttack | 非ダメージ+回復 |
| 009_PoisonSting | 毒の一刺し | Preemptive | 突×2.0+毒60%+自爆 |
| 010_healint | HP回復（INT倍率） | SkillAttack | 非ダメージ+回復 |