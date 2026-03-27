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

    [Header("Weapon")]
    public WeaponAttribute weaponAttribute = WeaponAttribute.Strike;
    public int attackPower;

    [Header("Weapon Skills")]
    /// <summary>この武器が持つスキル一覧（武器スキル）。category=Weapon の場合に使用。</summary>
    public SkillData[] skills;

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

    [Header("Weapon - Equipment Resistances")]
    [Tooltip("装備時のみ適用される属性耐性配列。各属性の耐性値を100%反映。\n"
           + "例: 炎耐性50の武器 + 炎耐性50のパッシブアイテム = 炎耐性100(完全耐性)")]
    public EquipResistance[] equipResistances;

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