using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// スキル1つ分のマスターデータ。
/// プレイヤー用（武器スキル/魔法スキル）と敵用の両方をこの1つのクラスで表現する。
///
/// 【統合設計】
///   プレイヤー専用フィールド（skillSource, cooldownTurns, mpCost）はモンスター使用時は参照しない。
///   モンスター専用フィールド（actionType）はプレイヤー使用時は参照しない。
///
/// 【追加効果システム】
///   additionalEffects リストに SkillEffectEntry を追加することで、
///   毒付与・レベルドレイン・回復等の効果を任意に組み合わせられる。
///   各効果のパラメータ（付与率・回復量等）は SkillEffectEntry 側で持つため、
///   同じ SkillEffectData アセットを異なるパラメータで複数スキルに設定可能。
///
/// 【非ダメージスキルの判定】
///   damageMultiplier == 0 かつ fixedDamage == 0 の場合、
///   ダメージ計算をスキップし追加効果のみ実行する。
///   （旧 effectOnly フラグを廃止し、この判定に一本化）
/// </summary>
[CreateAssetMenu(menuName = "Skills/Skill Data")]
public class SkillData : ScriptableObject
{
    [Header("Basic")]
    public string skillId;
    public string skillName;
    [TextArea] public string description;

    [Header("Skill Source")]
    [Tooltip("Weapon = 武器スキル（CT制、MP消費なし）、Magic = 魔法スキル（MP制、CT なし）\n"
           + "モンスター使用時は参照しない。")]
    public SkillSource skillSource = SkillSource.Weapon;

    // =========================================================
    // モンスター行動タイプ（モンスター使用時のみ参照）
    // =========================================================

    [Header("Monster Action Type")]
    [Tooltip("モンスターの行動の種類。プレイヤー使用時は無視される。\n"
           + "Idle = 何もしない\n"
           + "NormalAttack = Monster.Attack 依存ダメージ\n"
           + "SkillAttack = 下記のパラメータでダメージ計算")]
    public MonsterActionType actionType = MonsterActionType.SkillAttack;

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
    /// 魔法スキルで倍率ベースにしたい場合にも使える。
    /// 0 の場合は fixedDamage を使用。
    /// damageMultiplier == 0 かつ fixedDamage == 0 → 非ダメージスキル（追加効果のみ）。
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
    // 基礎命中率
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
    // 追加効果リスト
    // =========================================================

    [Header("Additional Effects")]
    [Tooltip("スキルの追加効果リスト。\n"
           + "命中後（またはダメージ計算後）に順番に実行される。\n"
           + "非ダメージスキル（倍率0 & 固定0）では追加効果のみ実行される。")]
    public List<SkillEffectEntry> additionalEffects = new List<SkillEffectEntry>();

    // =========================================================
    // ヘルパープロパティ
    // =========================================================

    /// <summary>
    /// ダメージを与えないスキルかどうか。
    /// damageMultiplier == 0 かつ fixedDamage == 0 の場合 true。
    /// 旧 effectOnly フラグの代替。
    /// </summary>
    public bool IsNonDamage => damageMultiplier <= 0f && fixedDamage <= 0;

    /// <summary>
    /// 追加効果を持つかどうか。
    /// </summary>
    public bool HasAdditionalEffects =>
        additionalEffects != null && additionalEffects.Count > 0;
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

/// <summary>
/// モンスターの行動の種類。
/// EnemyActionEntry で actionType として使用する。
/// LevelDrain は追加効果（LevelDrainEffectData）に移行したため削除。
/// </summary>
public enum MonsterActionType
{
    /// <summary>何もしない（ターン終了）。</summary>
    Idle,

    /// <summary>
    /// 通常攻撃（Monster.Attack 依存ダメージ）。
    /// SkillData のダメージ系パラメータは無視する。
    /// damageCategory は参照する（物理防御 or 魔法防御ダイス）。
    /// </summary>
    NormalAttack,

    /// <summary>
    /// スキル攻撃（SkillData のパラメータでダメージ計算）。
    /// fixedDamage > 0 ならそれを使用。
    /// そうでなければ damageMultiplier × Monster.Attack を使用。
    /// どちらも 0 なら非ダメージスキル（追加効果のみ）。
    /// </summary>
    SkillAttack,
}