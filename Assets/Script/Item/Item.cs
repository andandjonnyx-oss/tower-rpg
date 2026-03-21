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

    [Header("Magic")]
    public string magicId;

    [Header("Sort")]
    public int sortOrder = 0;

    [Header("Skills")]
    public SkillData[] skills;  // ‚±‚Ì•Ší‚ª‚ÂƒXƒLƒ‹ˆê——
}

public enum ItemCategory
{
    Consumable,
    Weapon,
    Magic
}