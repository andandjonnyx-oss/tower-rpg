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
///   LevelDrain   → プレイヤーのレベルを1下げる（必中）
///
/// ★ブラッシュアップ:
///   effectOnly フラグを追加。SkillData と同様に、
///   ダメージ無し＋状態異常付与のみのスキルを表現可能にした。
///   モンスターは CT/MP を無視するルールのため、
///   プレイヤー側の SkillData と完全統一ではないが、
///   振る舞いとフィールド構成を揃えている。
///
/// ★レベルドレイン追加:
///   MonsterActionType.LevelDrain を追加。
///   プレイヤーのレベルを1下げる（必中、耐性なし）。
///   レベル1の場合は効果なし。
///   スキルポイント（statusPoint）は変更しない。
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
           + "SkillAttack = 下記のパラメータでダメージ計算\n"
           + "LevelDrain = プレイヤーのレベルを1下げる（必中）")]
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
           + "Idle / LevelDrain の場合は使用しない。")]
    public int baseHitRate = 90;

    // =========================================================
    // 状態異常付与（追加）
    // =========================================================

    [Header("Status Effect Infliction")]
    [Tooltip("このスキルが命中時に付与する状態異常。None なら付与しない。\n"
           + "敵スキル・敵魔法で使用する。")]
    public StatusEffect inflictEffect = StatusEffect.None;

    [Tooltip("状態異常の基礎付与率（%）。\n"
           + "実質命中率 = inflictChance × (1 - プレイヤーの耐性/100)\n"
           + "例: 敵のポイズン = 80")]
    [Range(0, 100)]
    public int inflictChance = 0;

    // =========================================================
    // ★ブラッシュアップ: effectOnly フラグ追加
    // =========================================================

    [Tooltip("true の場合、ダメージを与えず状態異常の付与のみ行う。\n"
           + "SkillData の effectOnly と同等の機能。\n"
           + "例: 敵の「ポイズン」魔法（ダメージ0、毒付与80%）\n"
           + "actionType=SkillAttack で effectOnly=true にすると、\n"
           + "ダメージ計算をスキップして状態異常のみ付与する。")]
    public bool effectOnly = false;
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
    ///
    /// ★ブラッシュアップ: effectOnly=true ならダメージスキップ。
    /// </summary>
    SkillAttack,

    /// <summary>
    /// レベルドレイン（プレイヤーのレベルを1下げる）。
    /// 必中。耐性なし。レベル1以下にはならない。
    /// ステータスポイント（statusPoint）は変更しない。
    /// 経験値は0にリセットされ、必要経験値も再計算される。
    /// </summary>
    LevelDrain,
}