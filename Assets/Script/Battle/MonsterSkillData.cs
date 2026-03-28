using UnityEngine;

/// <summary>
/// モンスタースキル1つ分のマスターデータ（ScriptableObject）。
/// 以前は EnemyActionEntry にインラインで持っていた「攻撃の中身」を分離し、
/// 複数のモンスターで同じスキルを参照できるようにした。
///
/// Monster.actions[i].skill にこの ScriptableObject をアサインして使う。
///
/// 行動の種類（EnemyActionType）もここで定義する:
///   Idle        → 何もしない
///   NormalAttack → Monster.Attack 依存の通常攻撃
///   SkillAttack  → このデータに定義されたスキル攻撃
/// </summary>
[CreateAssetMenu(menuName = "Battle/MonsterSkill")]
public class MonsterSkillData : ScriptableObject
{
    [Header("基本情報")]
    [Tooltip("スキルID（識別用）")]
    public string skillId;

    [Tooltip("戦闘ログに表示するスキル名")]
    public string skillName;

    [TextArea(2, 4)]
    [Tooltip("説明文（Editor・図鑑用）")]
    public string description;

    [Header("行動の種類")]
    [Tooltip("この行動の種類。\n"
           + "Idle = 何もしない\n"
           + "NormalAttack = Monster.Attack 依存ダメージ（倍率・固定ダメージは無視）\n"
           + "SkillAttack = 下記のパラメータでダメージ計算")]
    public MonsterActionType actionType = MonsterActionType.NormalAttack;

    [Header("ダメージ計算（actionType が SkillAttack の場合に使用）")]
    [Tooltip("物理か魔法か。防御ダイスの参照先が変わる（物理→Defense、魔法→MagicDefense）")]
    public DamageCategory damageCategory = DamageCategory.Physical;

    [Tooltip("攻撃の属性（耐性計算に使用）")]
    public WeaponAttribute attackAttribute = WeaponAttribute.Strike;

    [Tooltip("ダメージ倍率。fixedDamage が 0 の場合に使用。\n"
           + "Monster.Attack × damageMultiplier がダメージになる。\n"
           + "0 の場合は fixedDamage を参照する。")]
    public float damageMultiplier = 0f;

    [Tooltip("固定ダメージ。damageMultiplier より優先される。\n"
           + "0 の場合は damageMultiplier を参照する。\n"
           + "どちらも 0 なら Monster.Attack をそのまま使用。")]
    public int fixedDamage = 0;

    // =========================================================
    // 命中率（追加）
    // =========================================================

    [Header("Hit Rate")]
    [Tooltip("この敵スキルの基礎命中率（%）。デフォルト90。\n"
           + "敵スキル攻撃の命中率 = baseHitRate × (1 - プレイヤー回避率/100)\n"
           + "ただし最低10%保証。\n"
           + "Idle の場合は使用しない。")]
    public int baseHitRate = 90;
}

/// <summary>
/// モンスターの行動の種類。
/// EnemyActionEntry で actionType として使用する。
/// </summary>
public enum MonsterActionType
{
    /// <summary>何もしない（ターン終了）。</summary>
    Idle,

    /// <summary>
    /// 通常攻撃（Monster.Attack 依存ダメージ）。
    /// MonsterSkillData のダメージ系パラメータは無視する。
    /// damageCategory は参照する（物理防御 or 魔法防御ダイス）。
    /// </summary>
    NormalAttack,

    /// <summary>
    /// スキル攻撃（MonsterSkillData のパラメータでダメージ計算）。
    /// fixedDamage > 0 ならそれを使用。
    /// そうでなければ damageMultiplier × Monster.Attack を使用。
    /// どちらも 0 なら Monster.Attack をそのまま使用。
    /// </summary>
    SkillAttack,
}