using UnityEngine;

/// <summary>
/// スキル1つ分のマスターデータ。
/// 武器スキル（CT制）と魔法スキル（MP制）の両方をこの1つのクラスで表現する。
/// skillSource で武器スキルか魔法スキルかを区別する。
/// </summary>
[CreateAssetMenu(menuName = "Skills/Skill Data")]
public class SkillData : ScriptableObject
{
    [Header("Basic")]
    public string skillId;
    public string skillName;
    [TextArea] public string description;

    [Header("Skill Source")]
    [Tooltip("Weapon = 武器スキル（CT制、MP消費なし）、Magic = 魔法スキル（MP制、CT なし）")]
    public SkillSource skillSource = SkillSource.Weapon;

    [Header("Attribute / Damage")]
    /// <summary>スキルの攻撃属性。武器スキル・魔法スキル共通。</summary>
    public WeaponAttribute skillAttribute = WeaponAttribute.Strike;

    /// <summary>
    /// ダメージ倍率。武器スキルで使用。
    /// 例: 強撃 = 2.0（STR + 武器攻撃力 の2倍ダメージ）
    /// 魔法スキルで倍率ベースにしたい場合にも使える。0 の場合は fixedDamage を使用。
    /// </summary>
    public float damageMultiplier;

    /// <summary>
    /// 固定ダメージ。魔法スキルで使用。
    /// 例: ファイアボール = 10（INT やステータスに依存しない固定ダメージ）
    /// 0 の場合は damageMultiplier を使用。
    /// </summary>
    public int fixedDamage;

    [Header("Cost")]
    /// <summary>
    /// クールタイム（ターン数）。武器スキル用。
    /// 魔法スキルでは通常 0（MP消費で制御するため）。
    /// </summary>
    public int cooldownTurns;

    /// <summary>
    /// MP消費量。魔法スキル用。
    /// 武器スキルでは通常 0（CT制で制御するため）。
    /// 将来的に「CTもMPも消費する」スキルも表現可能。
    /// </summary>
    public int mpCost;
}

/// <summary>
/// スキルの発動元を区別する列挙型。
/// Weapon: 装備中の武器に紐づくスキル。クールタイム制。
/// Magic:  魔法アイテム所持で使えるスキル。MP消費制。
/// </summary>
public enum SkillSource
{
    Weapon, // 武器スキル（CT制、装備武器に紐づく）
    Magic,  // 魔法スキル（MP制、魔法アイテム所持で使用可能）
}