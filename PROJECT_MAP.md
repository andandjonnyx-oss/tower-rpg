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
| BattleSceneController.cs | 戦闘メイン。UI・ログキュー・勝敗処理・先制抽選・ドロップアイテム処理・MagicSelector連携・状態異常UI表示 |
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
| ItemPickupWindow.cs | アイテム拾得ポップアップ（Tower/Battle/Talk報酬共用） |
| ItemDetailPanel.cs | アイテム詳細パネル |
| ItemSlotView.cs | アイテムスロット表示 |
| ItemboxContext.cs | Itemboxシーン制御（消費/食べる/transformInto/MP回復対応） |
| TowerItemTrigger.cs | Tower中アイテム拾得トリガー（Talk報酬のisRewardItem判定対応/魔法UI即時更新対応） |
| OpenItemBoxButton.cs | アイテムボックス開くボタン |
| IItemContext.cs | アイテムコンテキストインターフェース |

### Assets/Script/Talk/
| ファイル | 役割 |
|---------|------|
| TalkEvent.cs | 会話イベントSO定義（rewardItem フィールド追加済み） |
| TalkRunner.cs | 会話実行（報酬アイテム付与対応） |
| TalkEventDatabase.cs | 会話イベントDB |
| TowerEventTrigger.cs | Tower中会話イベントトリガー |
| EventConditon.cs | 会話発生条件基底 |
| TimeRangeCondition.cs | 時間範囲条件 |

### Assets/Script/Storage/
| ファイル | 役割 |
|---------|------|
| StorageContext.cs | 倉庫シーン制御（消費/食べる/transformInto/MP回復対応） |
| Storagemanager.cs | 倉庫データ管理 |

### Assets/Script/ (ルート)
| ファイル | 役割 |
|---------|------|
| GameState.cs | ゲーム全体の状態管理（HP/MP/レベル/EXP/ステータスポイント/セーブ/isRewardItem追加済み） |
| GameStateautocreate.cs | GameState自動生成 |
| AttributeTypes.cs | WeaponAttribute enum + ToJapanese拡張 |
| TowerState.cs | タワー探索制御（MagicSelector連携/非バトル魔法/RefreshFieldMagicFromExternal追加済み） |
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
    F01-10/
      consume/                ← 消費アイテム（C001_Yakusou〜C007_18ice）
      magic/                  ← 魔法アイテム（M001_Fire〜M011_depoizu）
      Weapon/                 ← 武器（W001_Bokutou〜W008_icenobou）
  Monsterlist/
    Normal/F1-F10/            ← 1〜10階通常敵（001〜010作成済み）
    Boss/                     ← ボス敵（F10B_Boslime作成済み）
  Skilllist/                  ← スキルデータ（001〜018作成済み）
  Skilleffect/                ← スキル効果SO
  Talklist/                   ← 会話イベントデータ
  Talkcondition/              ← 会話発生条件
```

## 作成済みモンスター（F1-F10）
| ID | 名前 | 出現階 | HP | ATK | DEF | 特徴 |
|----|------|--------|-----|-----|-----|------|
| 001_Slime | スライム | 1-5 | 20 | 5 | 1 | 殴・突半減 |
| 002_inpu | インプ | 1-5 | 15 | 5 | 2 | Fire50%耐性、回避20 |
| 003_goblin | ゴブリン | 1-5 | 30 | 10 | 3 | 斬攻撃+強撃 |
| 004_zonbie | ゾンビ | 4-8 | 70 | 15 | 0 | Fire5倍弱点、半分待機、毒無効 |
| 005_mitubati | ミツバチ | 4-8 | 15 | 10 | 0 | 回避60、先制毒の一刺し+自爆、毒無効 |
| 006_ork | オーク | 4-8 | 40 | 10 | 10 | 殴攻撃+強撃 |
| 007_Pslime | ポイズンスライム | 6-10 | 50 | 10 | 5 | 毒攻撃、炎-200%弱点、毒無効 |
| 008_kaenhousyaki | 火炎放射器 | 6-10 | 65 | 10 | 15 | 炎無効、雷-200%弱点、MDEF10、毒無効 |
| 009_1pa-kun | 1%で999ダメージくん | 6-10 | 70 | 0 | 0 | 99% Idle + 1% 固定999ダメージ |
| 010_seireikaminari | 雷の精霊 | 10のみ | 80 | 15 | 10 | 雷無効、Weight0.3、確定ドロップ:ステUP3、MDEF10 |
| F10B_Boslime | ボスライム | ボス | 500 | 30 | 20 | 毒攻撃+炎攻撃、毒耐性なし |

## 作成済みスキル
| ID | 名前 | タイプ | 備考 |
|----|------|--------|------|
| 001_Strattack | 通常殴攻撃 | SkillAttack | 殴属性、倍率1 |
| 001a_Slaattack | 通常斬攻撃 | SkillAttack | 斬属性、倍率1 |
| 001c_fireattack | 通常炎攻撃 | SkillAttack | 炎属性、倍率1、Magical |
| 002_Idle | 様子を見ている | Idle | |
| 003_leveldrain | レベルドレイン | SkillAttack | 非ダメージ+追加効果 |
| 004_fireball | ファイアボール | SkillAttack | Fire bonusDamage=10、MP5 |
| 005_lightning | ライトニング | SkillAttack | Lightning bonusDamage=20、MP8 |
| 006_poison | 毒攻撃（効果のみ） | SkillAttack | 非ダメージ+毒付与 |
| 007_poisonattack | 毒攻撃（ダメージ付） | SkillAttack | ダメージ+毒付与 |
| 008_powerattackstr | 強撃（殴） | SkillAttack | 倍率2.0 CT3 |
| 008a_powerattacksla | 強撃（斬） | SkillAttack | 倍率2.0 CT3 |
| 009_heal | HP回復（固定値） | SkillAttack | 非ダメージ+回復、noBattleOk=true |
| 009a_heal2 | HP回復2 | SkillAttack | 非ダメージ+回復 |
| 010_healint | HP回復（INT倍率） | SkillAttack | 非ダメージ+回復、noBattleOk=true |
| 011_dokusasi | 毒の一刺し | Preemptive | 突×2.0+毒60%+自爆 |
| 012_bunmawasi | ぶん回し | SkillAttack | 斬×3.0、命中30% |
| 013_999dame | 999ダメージ | SkillAttack | bonusDamage=999、殴属性 |
| 014_3renduki | 三連突き | SkillAttack | 突×0.6×3回、CT4 |
| 015_crash | クラッシュ | SkillAttack | 殴×2.0+50%スタン、CT5 |
| 016_raikiri | 雷切 | SkillAttack | 斬×2.0+bonusDamage10、CT5 |
| 017_18attack | 18アタック | SkillAttack | 氷×0.1×18回、CT18 |
| 018_depoizu | デポイズ | SkillAttack | 非ダメージ+毒消し、noBattleOk=true |

## 拾得アイテム一覧（F1-F10）

### 消耗品
| ID | 名前 | 出現階 | 効果 |
|----|------|--------|------|
| C001 | 薬草 | 1-10 | HP50回復 |
| C002 | 毒消し草 | 4-10 | 毒治療+HP20回復 |
| C004 | 爆弾 | 4-10 | 戦闘専用、火属性固定50ダメ |
| C005 | 上薬草 | 6-10 | HP75回復 |
| C006 | マナポーション | 6-10 | MP30回復 |
| C007 | 18アイス | 8-10 | HP18回復（使用後アイテム変化） |

### 武器
| ID | 名前 | 出現階 | ATK | 属性 | 特殊 |
|----|------|--------|-----|------|------|
| W001 | 木刀 | 1-3 | +3 | Slash | スキル:強撃(CT3,2倍) |
| W002 | 木の槍 | 1-5 | +5 | Pierce | スキル:三連突き(CT4,0.6倍×3) |
| W003 | ポイズンナイフ | 4-10 | +3 | Slash | 通常20%毒付与、毒耐性50 |
| W004 | ピコハン | 4-7 | +6 | Strike | 通常10%スタン、スキル:クラッシュ |
| W005 | 火の剣 | 6-10 | +10 | Fire | スキル:強撃、火耐性50装備 |
| W006 | 雷の剣 | 6-10 | +10 | Slash | スキル:雷切、雷耐性50装備 |
| W007 | さくらぼー | 8-10 | +15 | Pierce | スキル:三連突き、食べるとHP100回復 |
| W008 | 18アイスブレード | 拾得不可 | +18 | Slash | スキル:18アタック(CT18) |

### マジックアイテム
| ID | 名前 | 出現階 |
|----|------|--------|
| M001 | ファイア書 | 1-5 |
| M002 | ライトニング書 | 1-5 |
| M003 | 炎の護符 | 3-7 |
| M004 | 革の盾 | 3-7 |
| M005 | 革の鎧 | 3-7 |
| M006 | 攻撃UPリング | 5-10 |
| M007 | 毒の護符 | 5-10 |
| M008 | ルーペ | 1-10 |
| M009 | ヒール書 | 3-8 |
| M010 | ヒーラ書 | 6-10 |
| M011 | デポイズ書 | 4-10 |