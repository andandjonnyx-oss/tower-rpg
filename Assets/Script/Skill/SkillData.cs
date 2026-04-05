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
///   damageMultiplier == 0 かつ bonusDamage == 0 の場合、
///   ダメージ計算をスキップし追加効果のみ実行する。
///
/// 【ダメージ計算式】
///   baseDamage = (Attack × damageMultiplier) + bonusDamage
///   ※ 四捨五入後に bonusDamage を加算
///
///   表現パターン:
///     倍率のみ:      damageMultiplier=2, bonusDamage=0   → Attack×2
///     固定ダメージ:  damageMultiplier=0, bonusDamage=10  → 固定10
///     倍率+追加:     damageMultiplier=2, bonusDamage=10  → Attack×2 + 10
///
/// 【多段攻撃】
///   hitCount > 1 の場合、命中判定→ダメージ計算を hitCount 回繰り返す。
///   各ヒットごとに独立して命中判定・防御ダイス・クリティカル判定を行う。
///   途中で対象のHPが0になったら残りの判定をスキップする。
///   追加効果は全ヒット完了後に1回だけ実行する。
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
           + "SkillAttack = 下記のパラメータでダメージ計算（通常攻撃は倍率1.0で表現）\n"
           + "Preemptive = 先制攻撃（プレイヤー行動選択後、プレイヤー行動の前に割り込む）")]
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
    /// 例: 通常攻撃 = 1.0、強撃 = 2.0（STR + 武器攻撃力 の2倍ダメージ）
    /// 魔法スキルで倍率ベースにしたい場合にも使える。
    /// 0 の場合は bonusDamage のみ使用。
    /// damageMultiplier == 0 かつ bonusDamage == 0 → 非ダメージスキル（追加効果のみ）。
    /// </summary>
    public float damageMultiplier;

    /// <summary>
    /// 追加固定ダメージ。倍率ダメージに加算される。
    /// 計算式: (Attack × damageMultiplier) + bonusDamage
    ///
    /// 表現パターン:
    ///   固定ダメージのみ: damageMultiplier=0, bonusDamage=10  → 固定10（旧fixedDamage相当）
    ///   倍率+追加:       damageMultiplier=2, bonusDamage=10  → Attack×2 + 10
    ///   倍率のみ:        damageMultiplier=2, bonusDamage=0   → Attack×2
    ///
    /// 0 の場合は加算なし。
    /// </summary>
    [Tooltip("追加固定ダメージ。倍率ダメージに加算される。\n"
           + "計算式: (Attack × damageMultiplier) + bonusDamage\n"
           + "固定ダメージのみの場合は damageMultiplier=0 にして bonusDamage に値を設定。\n"
           + "例: ファイアボール = damageMultiplier=0, bonusDamage=10 → 固定10\n"
           + "例: 雷切 = damageMultiplier=2, bonusDamage=10 → Attack×2 + 10")]
    public int bonusDamage = 0;

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
    // 多段攻撃（追加）
    // =========================================================

    [Header("Multi-Hit")]
    [Tooltip("攻撃回数。デフォルト1（単発）。\n"
           + "2以上にすると、命中判定→ダメージ計算を hitCount 回繰り返す。\n"
           + "各ヒットは独立して命中・防御ダイス・クリティカルを判定する。\n"
           + "途中で対象HPが0になったら残りをスキップする。\n"
           + "追加効果は全ヒット完了後に1回だけ実行する。\n"
           + "例: 三連突き = hitCount=3, damageMultiplier=0.6")]
    public int hitCount = 1;

    [Header("Field Usage")]
    [Tooltip("true の場合、塔シーン（非バトル時）でも使用可能。\n"
       + "所持魔法のうちこのフラグが true のものだけが塔の魔法ドロップダウンに表示される。\n"
       + "ヒール・毒消し等の回復系魔法に設定する。")]
    public bool noBattleOk = false;

    // =========================================================
    // 追加効果リスト
    // =========================================================

    [Header("Additional Effects")]
    [Tooltip("スキルの追加効果リスト。\n"
           + "命中後（またはダメージ計算後）に順番に実行される。\n"
           + "非ダメージスキル（倍率0 & ボーナス0）では追加効果のみ実行される。")]
    public List<SkillEffectEntry> additionalEffects = new List<SkillEffectEntry>();

    // =========================================================
    // ヘルパープロパティ
    // =========================================================

    /// <summary>
    /// ダメージを与えないスキルかどうか。
    /// damageMultiplier == 0 かつ bonusDamage == 0 の場合 true。
    /// </summary>
    public bool IsNonDamage => damageMultiplier <= 0f && bonusDamage <= 0;

    /// <summary>
    /// 追加効果を持つかどうか。
    /// </summary>
    public bool HasAdditionalEffects =>
        additionalEffects != null && additionalEffects.Count > 0;

    /// <summary>
    /// 多段攻撃かどうか。hitCount > 1 の場合 true。
    /// </summary>
    public bool IsMultiHit => hitCount > 1;

    /// <summary>
    /// 実効ヒット数を返す。最低1。
    /// </summary>
    public int EffectiveHitCount => hitCount > 1 ? hitCount : 1;
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
///
/// 【NormalAttack 廃止について】
///   旧 NormalAttack は Monster.Attack をそのまま使い、属性耐性を無視していた。
///   SkillAttack に統一し、通常攻撃は damageMultiplier=1 で表現する。
///   enum 値 1 は互換性のため残すが、処理上は SkillAttack と同一扱い。
/// </summary>
public enum MonsterActionType
{
    /// <summary>何もしない（ターン終了）。</summary>
    Idle = 0,

    /// <summary>
    /// [廃止] 旧・通常攻撃。処理上は SkillAttack と同一扱い。
    /// 新規作成時は SkillAttack（damageMultiplier=1）を使用すること。
    /// enum 値を維持するのは既存アセットの互換性のため。
    /// </summary>
    [System.Obsolete("NormalAttack は廃止。SkillAttack（damageMultiplier=1）を使用してください。")]
    NormalAttack = 1,

    /// <summary>
    /// スキル攻撃（SkillData のパラメータでダメージ計算）。
    /// baseDamage = (Monster.Attack × damageMultiplier) + bonusDamage
    /// すべて 0 なら非ダメージスキル（追加効果のみ）。
    /// 通常攻撃は damageMultiplier=1 で表現する。
    /// </summary>
    SkillAttack = 2,

    /// <summary>
    /// 先制攻撃。プレイヤーの行動選択後、プレイヤー行動実行の前に割り込む。
    /// ダメージ計算は SkillAttack と同じ（SkillData のパラメータを使用）。
    /// ターン開始時の事前抽選で選ばれた場合のみ発動する。
    /// </summary>
    Preemptive = 3,
}