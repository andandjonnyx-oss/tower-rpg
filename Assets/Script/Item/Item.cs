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