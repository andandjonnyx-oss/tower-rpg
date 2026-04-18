using UnityEngine;

[CreateAssetMenu(menuName = "Items/Item Data")]
public class ItemData : ScriptableObject
{
    [Header("Basic")]
    public string itemId;
    public string itemName;
    [TextArea] public string description;
    public Sprite icon;

    [Header("Range")]
    public int Minfloor;
    public int Minstep;
    public int Maxfloor;
    public int Maxstep;

    [Header("Category")]
    public ItemCategory category;

    [Header("Stack")]
    public bool stackable = true;
    public int maxStack = 99;

    [Header("Consumable")]
    public int healAmount;
    public int mpHealAmount = 0;

    // =========================================================
    // 消費アイテム: 状態異常回復（追加）
    // =========================================================

    [Header("Consumable - Status Effect Cure")]
    [Tooltip("true の場合、使用時に毒状態を回復する。\n"
           + "毒消しアイテムに設定する。healAmount との併用可能。")]
    public bool curesPoison = false;
    [Tooltip("true の場合、使用時に麻痺状態を回復する。")]
    public bool curesParalyze = false;
    [Tooltip("true の場合、使用時に暗闇状態を回復する。")]
    public bool curesBlind = false;
    [Tooltip("true の場合、使用時に沈黙状態を回復する。")]
    public bool curesSilence = false;

    [Tooltip("true の場合、使用時に石化状態を回復する。\n"
           + "石化は CureAilments では解除不可。専用アイテムでのみ解除。")]
    public bool curesPetrify = false;
    [Tooltip("true の場合、使用時に魅了状態を回復する。")]
    public bool curesCharm = false;
    [Tooltip("true の場合、使用時に呪い状態を回復する。")]
    public bool curesCurse = false;
    [Tooltip("true の場合、使用時にガラス状態を回復する。")]
    public bool curesGlass = false;



    // =========================================================
    // 消費アイテム: ステータスポイント付与（追加）
    // =========================================================

    [Header("Consumable - Status Point")]
    [Tooltip("使用時に恒久的に加算されるステータスポイント。\n"
           + "0 の場合は効果なし。\n"
           + "例: ステータスアップの薬 = 5（使用で5ポイント獲得）")]
    public int statusPointGain = 0;

    // =========================================================
    // 消費アイテム: 攻撃アイテム（追加）
    // =========================================================
    //
    // 戦闘中に使用すると敵に固定ダメージを与える消費アイテム。
    // 例: 爆弾（物理・炎・50ダメージ）
    //
    // 処理フロー:
    //   1. Itembox で使用 → battleDamage 等を GameState に一時保存
    //   2. BattleSceneController に復帰 → ダメージ計算を実行
    //      - 敵の属性耐性を適用
    //      - 防御ダイスを適用（damageCategory に基づく）
    //      - 最終ダメージを敵HPから差し引く
    //   3. ターン消費（通常のアイテム使用と同じ）
    //
    // healAmount, curesPoison, statusPointGain との併用可能。
    // （回復しつつダメージも与えるアイテムも表現可能）
    // =========================================================

    [Header("Consumable - Battle Attack")]
    [Tooltip("戦闘中に使用した時の固定ダメージ。\n"
           + "0 の場合は攻撃効果なし。\n"
           + "例: 爆弾 = 50（敵に50ダメージ）")]
    public int battleDamage = 0;

    [Tooltip("攻撃アイテムの属性。敵の属性耐性計算に使用する。\n"
           + "例: 爆弾 = Fire（炎属性）")]
    public WeaponAttribute battleAttribute = WeaponAttribute.Strike;

    [Tooltip("攻撃アイテムの物理/魔法区分。\n"
           + "敵の防御ダイス計算に影響する（物理→Defense、魔法→MagicDefense）。\n"
           + "例: 爆弾 = Physical")]
    public DamageCategory battleDamageCategory = DamageCategory.Physical;

    [Tooltip("true の場合、戦闘中のみ使用可能。\n"
           + "非バトル時（Itembox通常画面・倉庫）では「使う」ボタンが表示されない。")]
    public bool battleOnly = false;

    // =========================================================
    // 消費アイテム: ボス餌付けアイテム（追加）
    // =========================================================

    [Header("Consumable - Boss Feed")]
    [Tooltip("true の場合、ボス戦中に使用すると即勝利（イベント勝利）扱いになる。\n"
           + "ボス戦中はボタンラベルが「使う」→「与える」に変化する。\n"
           + "通常戦闘・非バトル時は通常のアイテムとして使用可能（回復等）。")]
    public bool bossFeedItem = false;

    // =========================================================
    // 捨てられないアイテム（追加）
    // =========================================================

    [Header("Cannot Discard")]
    [Tooltip("true の場合、このアイテムは捨てることができない。\n"
           + "Itembox の「捨てる」ボタンが「捨てるな」に変化し、選択不可になる。\n"
           + "入手ポップアップの「諦める」ボタンも非表示になる。")]
    public bool cannotDiscard = false;

    // =========================================================
    // 消費/変化: 使用後にアイテムが変化する（追加）
    // =========================================================
    //
    // 使用後に別のアイテムに変化する仕組み。
    // Consumable・Weapon どちらのカテゴリでも使用可能。
    //
    // 処理フロー:
    //   1. 元アイテムを RemoveItem で消す（装備中なら外す）
    //   2. transformInto が null でなければ AddItem で新アイテムを追加
    //   ※ 先に消してから追加するので枠は±0（満杯でも問題なし）
    //
    // 例: 18アイス（Consumable, healAmount=30, transformInto=アイスソード）
    //     → 使うと HP+30 回復し、アイスソード（Weapon）が所持品に入る
    // =========================================================

    [Header("Transform After Use")]
    [Tooltip("使用後に変化するアイテム。null なら変化なし（通常通り消滅）。\n"
           + "Consumable: 「使う」で消費後に変化先アイテムが所持品に追加される。\n"
           + "Weapon (isEdible=true): 「食べる」で消費後に変化先アイテムが追加される。\n"
           + "先に元アイテムを消してから追加するので枠は±0。")]
    public ItemData transformInto;

    // =========================================================
    // 武器: 食べられる武器（追加）
    // =========================================================
    //
    // category=Weapon でも消費アイテムのように「食べる」ことができる武器。
    // 食べると武器は消滅し、回復効果等が適用される。
    // transformInto と併用すれば、食べた後に別アイテムが出現する。
    //
    // 例: さくらぼー（Weapon, isEdible=true, eatHealAmount=15）
    //     → 装備して突武器として使えるが、食べると HP+15 回復して消滅
    // =========================================================

    [Header("Weapon - Edible")]
    [Tooltip("true の場合、この武器は「食べる」ことができる。\n"
           + "食べると武器が消滅し、eatHealAmount 分の HP 回復等が適用される。\n"
           + "装備中でも食べられる（自動的に外してから消費）。")]
    public bool isEdible = false;

    [Tooltip("食べた時の HP 回復量。0 なら回復なし。")]
    public int eatHealAmount = 0;

    [Tooltip("食べた時に毒状態を回復するかどうか。")]
    public bool eatCuresPoison = false;
    [Tooltip("食べた時に麻痺状態を回復するかどうか。")]
    public bool eatCuresParalyze = false;
    [Tooltip("食べた時に暗闇状態を回復するかどうか。")]
    public bool eatCuresBlind = false;
    [Tooltip("食べた時に沈黙状態を回復するかどうか。")]
    public bool eatCuresSilence = false;

    [Tooltip("食べた時に石化状態を回復するかどうか。")]
    public bool eatCuresPetrify = false;
    [Tooltip("食べた時に魅了状態を回復するかどうか。")]
    public bool eatCuresCharm = false;
    [Tooltip("食べた時に呪い状態を回復するかどうか。")]
    public bool eatCuresCurse = false;
    [Tooltip("食べた時にガラス状態を回復するかどうか。")]
    public bool eatCuresGlass = false;


    [Header("Weapon")]
    public WeaponAttribute weaponAttribute = WeaponAttribute.Strike;
    public int attackPower;

    [Header("Weapon Skills")]
    /// <summary>この武器が持つスキル一覧（武器スキル）。category=Weapon の場合に使用。</summary>
    public SkillData[] skills;

    // =========================================================
    // 武器: 通常攻撃時の状態異常付与（追加）
    // =========================================================

    [Header("Weapon - Status Effect on Hit")]
    [Tooltip("この武器の通常攻撃が命中した時に付与する状態異常。None なら付与しない。\n"
           + "例: ポイズンナイフ → Poison")]
    public StatusEffect weaponInflictEffect = StatusEffect.None;

    [Tooltip("武器通常攻撃の状態異常基礎付与率（%）。\n"
           + "実質命中率 = weaponInflictChance × (1 - 対象の耐性/100)\n"
           + "例: ポイズンナイフ = 20")]
    [Range(0, 100)]
    public int weaponInflictChance = 0;

    // =========================================================
    // 装備時のみ適用されるステータス補正
    // =========================================================

    [Header("Weapon - Equipment Stats")]
    [Tooltip("装備時のみ適用される防御力。パッシブとは別計算で100%反映。")]
    public int equipDefense;

    [Tooltip("装備時のみ適用される魔法攻撃力。パッシブとは別計算で100%反映。")]
    public int equipMagicAttack;

    [Tooltip("装備時のみ適用される魔法防御力。パッシブとは別計算で100%反映。")]
    public int equipMagicDefense;

    [Tooltip("装備時のみ適用される運の良さ。パッシブとは別計算で100%反映。")]
    public int equipLuck;

    [Tooltip("装備時のみ適用される最大HPボーナス。パッシブとは別計算で100%反映。")]
    public int equipMaxHp;

    [Tooltip("装備時のみ適用される最大MPボーナス。パッシブとは別計算で100%反映。")]
    public int equipMaxMp;

    // =========================================================
    // 装備時のみ適用される命中・回避・クリティカル補正（追加）
    // =========================================================

    [Header("Weapon - Hit / Evasion / Critical")]
    [Tooltip("装備時のみ適用される命中力ボーナス（int）。パッシブとは別計算で100%反映。")]
    public int equipAccuracy;

    [Tooltip("装備時のみ適用される回避率ボーナス（%）。パッシブとは別計算で100%反映。\n"
           + "int 値がそのまま float の回避率に加算される（1 = 1.00%）。")]
    public int equipEvasion;

    [Tooltip("装備時のみ適用されるクリティカル率ボーナス（%）。パッシブとは別計算で100%反映。\n"
           + "int 値がそのまま float のクリティカル率に加算される（1 = 1.00%）。")]
    public int equipCritical;

    [Header("Weapon - Hit Rate")]
    [Tooltip("この武器の通常攻撃の基礎命中率（%）。デフォルト95。\n"
           + "素手の場合はハードコード95を使用する。\n"
           + "プレイヤー通常攻撃の命中率 = baseHitRate × (1 - (敵回避力 - 命中力)/100)\n"
           + "ただし最低25%保証。")]
    public int baseHitRate = 95;

    [Header("Weapon - Equipment Resistances")]
    [Tooltip("装備時のみ適用される属性耐性配列。各属性の耐性値を100%反映。\n"
           + "例: 炎耐性50の武器 + 炎耐性50のパッシブアイテム = 炎耐性100(完全耐性)")]
    public EquipResistance[] equipResistances;

    // =========================================================
    // ★ブラッシュアップ: 装備時のみ適用される状態異常耐性（追加）
    // =========================================================

    [Header("Weapon - Equipment Status Effect Resistances")]
    [Tooltip("装備時のみ適用される状態異常耐性配列。各状態異常の耐性値を100%反映。\n"
           + "例: 毒耐性30の武器 + 毒耐性50のパッシブ = 毒耐性80")]
    public EquipStatusEffectResistance[] equipStatusEffectResistances;

    [Header("Magic - Skill")]
    /// <summary>
    /// この魔法アイテムを所持していると使用可能になるスキル。
    /// category=Magic の場合に使用。null ならスキル付与なし（パッシブ専用アイテム）。
    /// 例: 火の玉 → ファイアボールの SkillData を設定。
    /// </summary>
    public SkillData magicSkill;

    [Header("Magic - Passive Effects")]
    /// <summary>
    /// この魔法アイテムを所持しているだけで発動するパッシブ効果の一覧。
    /// category=Magic の場合に使用。空配列ならパッシブなし（スキル専用アイテム）。
    /// magicSkill と passiveEffects は併存可能（魔法が使えて能力も上がるアイテム）。
    /// 例: 炎の護符 → PassiveEffect(AttributeResistance, Fire, 50) を設定。
    /// 例: 万能の守り → 複数の PassiveEffect を設定（火耐性+20, 氷耐性+20, 雷耐性+20）。
    /// </summary>
    public PassiveEffect[] passiveEffects;

    [Header("Sort")]
    public int sortOrder = 0;

    // =========================================================
    // ヘルパープロパティ
    // =========================================================

    /// <summary>
    /// このアイテムが戦闘中に敵にダメージを与える攻撃アイテムかどうか。
    /// battleDamage > 0 の場合 true。
    /// </summary>
    public bool IsBattleAttackItem => battleDamage > 0;
}

public enum ItemCategory
{
    Consumable,
    Weapon,
    Magic
}