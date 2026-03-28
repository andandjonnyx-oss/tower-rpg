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

    // =========================================================
    // 消費アイテム: 状態異常回復（追加）
    // =========================================================

    [Header("Consumable - Status Effect Cure")]
    [Tooltip("true の場合、使用時に毒状態を回復する。\n"
           + "毒消しアイテムに設定する。healAmount との併用可能。")]
    public bool curesPoison = false;

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
    // パッシブ効果（PassiveCalculator）は複数アイテムの重複ルール
    // （2個目以降10%減衰）を適用するが、装備品はここに設定した値を
    // 常に100%そのまま加算する。
    //
    // 設計意図:
    //   装備は1つしか装備できないため重複ルールは不要。
    //   これにより「攻撃力は低いが防御・耐性が高い武器」
    //   「炎耐性50の武器 + 炎耐性50のパッシブアイテムで完全耐性」
    //   といったシステムが実現できる。
    //
    // 例: 攻撃力10の武器 + 攻撃力10のパッシブ×3 の場合
    //   Attack = baseSTR + 10(装備) + 10 + 1 + 1(パッシブ減衰) = baseSTR + 22
    //   ここで攻撃力5の武器に変更すると
    //   Attack = baseSTR + 5(装備) + 10 + 1 + 1(パッシブ減衰) = baseSTR + 17
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
    //
    // 装備品の命中力/回避率/クリティカル率は 100% そのまま加算する。
    // パッシブ側の同項目は PassiveCalculator の重複ルールを適用する。
    //
    // 回避率・クリティカル率は float（小数点2位精度）だが、
    // 装備品の値は int で設定し、そのまま加算する
    // （1 = 1.00%、装備品単体は整数%での補正を想定）。
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
    //
    // 武器（100%反映）でもパッシブアイテム（最大値以外10%反映）でも、
    // 持つパラメータのセットを統一するため追加。
    //
    // 属性耐性（EquipResistance[]）と同じ構造で、
    // 状態異常耐性を配列で持つ。
    //
    // 例: 毒耐性30の武器 + 毒耐性50のパッシブアイテム
    //   → 30(装備) + 50(パッシブ) = 80
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
}

public enum ItemCategory
{
    Consumable,
    Weapon,
    Magic
}