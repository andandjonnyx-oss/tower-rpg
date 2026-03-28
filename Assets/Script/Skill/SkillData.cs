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
    /// スキルの物理/魔法区分。
    /// 敵の防御ダイス計算に使用する（物理→Monster.Defense、魔法→Monster.MagicDefense）。
    ///
    /// 設定例:
    ///   パワーアタック（物理剣攻撃）    → Physical
    ///   ファイアボール（炎魔法）         → Magical
    ///   ライトニング（雷魔法）           → Magical
    ///   火炎斬り（炎属性・物理攻撃）     → Physical（属性は Fire, DamageCategory は Physical）
    /// </summary>
    [Tooltip("物理か魔法か。敵の防御ダイスの計算に影響する（物理→Defense、魔法→MagicDefense）")]
    public DamageCategory damageCategory = DamageCategory.Physical;

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

    // =========================================================
    // 基礎命中率（追加）
    // =========================================================

    [Header("Hit Rate")]
    /// <summary>
    /// このスキルの基礎命中率（%）。デフォルト95。
    /// プレイヤースキル攻撃の命中率 = baseHitRate × (1 - (敵回避力 - 命中力)/100)
    /// ただし最低25%保証。
    /// 武器スキル・魔法スキル共通で使用する。
    /// </summary>
    [Tooltip("このスキルの基礎命中率（%）。デフォルト95。\n"
           + "プレイヤー攻撃の命中率 = baseHitRate × (1 - (敵回避力 - 命中力)/100)\n"
           + "ただし最低25%保証。")]
    public int baseHitRate = 95;

    // =========================================================
    // 状態異常付与（追加）
    // =========================================================

    [Header("Status Effect Infliction")]
    [Tooltip("このスキルが命中時に付与する状態異常。None なら付与しない。\n"
           + "プレイヤー・敵双方の魔法/スキルで共通に使う。")]
    public StatusEffect inflictEffect = StatusEffect.None;

    [Tooltip("状態異常の基礎付与率（%）。\n"
           + "実質命中率 = inflictChance × (1 - 対象の耐性/100)\n"
           + "例: ポイズン魔法 = 80、毒攻撃スキル = 40")]
    [Range(0, 100)]
    public int inflictChance = 0;

    [Tooltip("true の場合、ダメージ0（fixedDamage=0 かつ damageMultiplier=0）でも\n"
           + "状態異常の付与だけを行うスキルとして機能する。\n"
           + "false の場合、ダメージ計算後に追加で状態異常を付与する。")]
    public bool effectOnly = false;
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