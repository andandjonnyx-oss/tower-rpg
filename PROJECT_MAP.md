# PROJECT_MAP

リポジトリ: andandjonnyx-oss/tower-rpg
更新日: 2026-04-06

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
| BattleSceneController.cs | 戦闘メイン。UI・ログキュー・勝敗処理・先制抽選・ドロップアイテム処理・MagicSelector連携 |
| BattleSceneController_PlayerAction.cs | プレイヤー行動。攻撃/スキル/魔法/防御/先制割り込み |
| BattleSceneController_EnemyAction.cs | 敵行動。LUC判定/SkillAttack統合処理/先制チェック/ターン終了/RefreshMagicSelector |
| BattleSceneController_CombatUtils.cs | 命中/クリティカル/防御ダイス/属性耐性/ダメージ適用 |
| BattleContext.cs | 戦闘シーン間データ受け渡し（static） |
| Monster.cs | モンスターSO定義（ステータス/行動パターン/属性耐性/Weight(float)/ドロップ設定/StunResistance） |
| MonsterDatabase.cs | モンスターDB（フォルダスキャン自動登録/Weight重み付き抽選） |
| EncounterSystem.cs | エンカウント判定（20%/STEP） |
| Bossencountersystem.cs | ボス戦制御（撃破フラグ/勝利Talk） |
| EnemyActionEntry.cs | 敵行動テーブルエントリ + DamageCategory enum |
| Enemyhpbar.cs | 敵HPバー表示 |

### Assets/Script/Skill/
| ファイル | 役割 |
|---------|------|
| SkillData.cs | スキルSO定義（bonusDamage/noBattleOk追加済み）+ MonsterActionType/SkillSource enum |
| SkillEffectData.cs | 追加効果基底クラス（abstract SO） |
| SkillEffectEntry.cs | 追加効果エントリ（パラメータ保持） |
| SkillEffectProcessor.cs | 追加効果実行（毒/スタン/レベドレ/回復/自爆） |
| SelfDestructEffectData.cs | 自爆エフェクトSO |
| HealEffectData.cs | 回復エフェクトSO |
| LevelDrainEffectData.cs | レベルドレインエフェクトSO |
| StatusAilmentEffectData.cs | 状態異常エフェクトSO |
| StatusEffectSystem.cs | 状態異常中央管理（毒/スタン判定/ダメージ計算） |
| PassiveCalculator.cs | パッシブ効果計算/魔法スキル収集/非バトル魔法収集/属性耐性合算 |
| PassiveEffect.cs | パッシブ効果データ |
| EquipmentCalculator.cs | 装備ステータス計算 |
| EquipResistance.cs | 装備属性耐性 |
| EquipStatusEffectResistance.cs | 装備状態異常耐性 |
| MonsterAttributeResistance.cs | モンスター属性耐性（attribute + value） |

### Assets/Script/Item/
| ファイル | 役割 |
|---------|------|
| Item.cs (ItemData) | アイテムSO定義（healAmount/curesPoison/statusPointGain/isEdible/eatHeal/transformInto/mpHealAmount/武器/魔法/パッシブ） |
| InventoryItem.cs | インベントリ内アイテムインスタンス |
| ItemBoxManager.cs | アイテムボックス管理 |
| ItemDatabase.cs | アイテムDB |
| ItemPickupWindow.cs | アイテム拾得ポップアップ（Tower/Battle共用） |
| ItemDetailPanel.cs | アイテム詳細パネル |
| ItemSlotView.cs | アイテムスロット表示 |
| ItemboxContext.cs | Itemboxシーン制御（消費/食べる/transformInto/MP回復対応） |
| TowerItemTrigger.cs | Tower中アイテム拾得トリガー |
| OpenItemBoxButton.cs | アイテムボックス開くボタン |
| IItemContext.cs | アイテムコンテキストインターフェース |

### Assets/Script/Storage/
| ファイル | 役割 |
|---------|------|
| StorageContext.cs | 倉庫シーン制御（消費/食べる/transformInto/MP回復対応） |
| Storagemanager.cs | 倉庫データ管理 |

### Assets/Script/ (ルート)
| ファイル | 役割 |
|---------|------|
| GameState.cs | ゲーム全体の状態管理（HP/MP/レベル/EXP/ステータスポイント/セーブ） |
| GameStateautocreate.cs | GameState自動生成 |
| AttributeTypes.cs | WeaponAttribute enum + ToJapanese拡張 |
| TowerState.cs | タワー探索制御（MagicSelector連携/非バトル魔法） |
| TowerEntranceView.cs | タワー入口UI |
| FloorButton.cs | 階選択ボタン |
| HpMpDisplay.cs | HP/MP表示 |
| MainSceneRecovery.cs | メインシーン復帰時の全回復 |
| DebugSceneManager.cs | デバッグシーン制御 |
| MagicSelector.cs | 自作ドロップダウンUI（TMP_Dropdown代替、Battle/Tower共用） |

### Assets/Editor/
| ファイル | 役割 |
|---------|------|
| SkillEffectEntryDrawer.cs | 追加効果インスペクター表示（ジャンル別） |
| ItemDatabaseViewer.cs / ItemDetailWindow.cs | アイテムDB閲覧ツール（bonusDamage対応） |
| MonsterDatabaseViewer.cs / MonsterDetailWindow.cs | モンスターDB閲覧ツール（bonusDamage対応） |
| Skilldatabaseviewer.cs / Skilldetailwindow.cs / Skilleffectdetailwindow.cs | スキルDB閲覧ツール（bonusDamage対応） |
| TalkEventViewerWindow.cs | 会話イベント閲覧ツール |

## ScriptableAsset 構成
```
Assets/ScriptableAsset/
  ItemDatabase.asset          ← アイテムDB
  MonsterDatabase.asset       ← モンスターDB
  MainTalkDatabase.asset      ← 会話イベントDB
  Itemlist/
    consume/                  ← 消費アイテム（C001_Yakusou〜C007_18ice）
    magic/                    ← 魔法アイテム（M001_Fire等）
    Weapon/                   ← 武器（W001_Bokutou〜W008_icenobou）
  Monsterlist/
    Normal/F1-F10/            ← 1〜10階通常敵（001〜009作成済み）
    Boss/                     ← ボス敵（F3B_Boslime作成済み）
  Skilllist/                  ← スキルデータ（001〜017作成済み）
  Skilleffect/                ← スキル効果SO
  Talklist/                   ← 会話イベントデータ
  Talkcondition/              ← 会話発生条件
```

## 作成済みモンスター（F1-F10）
| ID | 名前 | 出現階 | HP | ATK | DEF | 特徴 |
|----|------|--------|-----|-----|-----|------|
| 001_Slime | スライム | 1-5 | 20 | - | - | 殴・突半減 |
| 002_inpu | インプ | 1-5 | 15 | - | - | Fire50%耐性、ファイアボール |
| 003_goblin | ゴブリン | 1-5 | 30 | - | - | 斬攻撃+強撃 |
| 004_zonbie | ゾンビ | 4-8 | 70 | 15 | 0 | Fire5倍弱点、半分待機 |
| 005_mitubati | ミツバチ | 4-8 | 15 | 10 | 0 | 回避60、先制毒の一刺し+自爆 |
| 006_ork | オーク | 4-8 | 40 | 10 | 10 | 斬攻撃+ぶん回し(x3,命中30%) |
| 007_Pslime | ポイズンスライム | 6-10 | 50 | 10 | 5 | 毒攻撃+毒付与、炎-200%弱点 |
| 008_kaenhousyaki | 火炎放射器 | 6-10 | 61 | 10 | 15 | 炎無効、雷-200%弱点、MDEF10 |
| 009_1pa-kun | 1%で999ダメージくん | 6-10 | 70 | 0 | 0 | 99% Idle + 1% 固定999ダメージ |

## 作成済みスキル
| ID | 名前 | タイプ | 備考 |
|----|------|--------|------|
| 001_Strattack | 通常殴攻撃 | NormalAttack(=1,Obsolete) | 殴属性、倍率1 |
| 001a_Slaattack | 通常斬攻撃 | NormalAttack(=1,Obsolete) | 斬属性、倍率1 |
| 001c_fireattack | 通常炎攻撃 | NormalAttack(=1,Obsolete) | 炎属性、倍率1、Magical |
| 002_Idle | 様子を見ている | Idle | |
| 003_leveldrain | レベルドレイン | SkillAttack | 非ダメージ+追加効果 |
| 004_fireball | ファイアボール | SkillAttack | Fire bonusDamage=10 |
| 005_lightning | ライトニング | SkillAttack | Lightning bonusDamage=20 |
| 006_poison | 毒攻撃（効果のみ） | SkillAttack | 非ダメージ+毒付与 |
| 007_poisonattack | 毒攻撃（ダメージ付） | SkillAttack | ダメージ+毒付与 |
| 008_powerattackstr | 強撃（殴） | SkillAttack | 倍率2.0 CT3 |
| 008a_powerattacksla | 強撃（斬） | SkillAttack | 倍率2.0 CT3 |
| 009_heal | HP回復（固定値） | SkillAttack | 非ダメージ+回復、noBattleOk=true |
| 010_healint | HP回復（INT倍率） | SkillAttack | 非ダメージ+回復、noBattleOk=true |
| 011_dokusasi | 毒の一刺し | Preemptive | 突×2.0+毒60%+自爆 |
| 012_bunmawasi | ぶん回し | SkillAttack | 斬×3.0、命中30% |
| 013_999dame | 999ダメージ | SkillAttack | bonusDamage=999、殴属性 |
| 014_crash | クラッシュ | SkillAttack | 殴×2.0+50%スタン、CT5 |
| 015_powerattacksla2 | 強撃（斬）CT3 | SkillAttack | 斬×2.0 CT3 |
| 016_sannrenntuki | 三連突き | SkillAttack | 突×0.6×3回、CT4 |
| 017_18attack | 18アタック | SkillAttack | 氷×0.1×18回、CT18 |