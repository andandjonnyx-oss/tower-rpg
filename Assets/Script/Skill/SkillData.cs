using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// HP依存ダメージの種類。
/// 通常のダメージ計算（倍率+固定+防御ダイス）をバイパスし、
/// 対象の現在HPに基づいたダメージを与える。
///
/// 【共通仕様】
///   - 命中判定: 通常通り行う（回避可能）
///   - 防御/属性耐性/バフ/クリティカル: 全てスキップ
///   - ダメージ最低保証: なし（0ダメージもあり得る）
///   - 端数処理: 切り下げ（FloorToInt）
///   - 双方向: プレイヤー→敵、敵→プレイヤー両方で使用可能
/// </summary>
public enum HpDependentType
{
    /// <summary>通常スキル（HP依存なし）</summary>
    None = 0,

    /// <summary>
    /// 対象の現在HPを半分にする。
    /// ダメージ = FloorToInt(対象の現在HP / 2)
    /// 例: HP99 → ダメージ49 → HP50
    /// 例: HP1  → ダメージ0  → HP1(変化なし)
    /// </summary>
    HalfCurrentHp = 1,

    /// <summary>
    /// 対象のHPを1にする。
    /// ダメージ = 対象の現在HP - 1
    /// 例: HP100 → ダメージ99 → HP1
    /// 例: HP1   → ダメージ0  → HP1(変化なし)
    /// </summary>
    ReduceToOne = 2,

    /// <summary>
    /// 対象の最大HPの一定割合をダメージとして与える。
    /// ダメージ = FloorToInt(対象の最大HP × hpDependentPercent / 100)
    /// 最低1ダメージ保証。
    /// 残りHPがダメージ以下なら倒せる(即死ではなく通常のダメージ適用)。
    /// プレイヤー→敵: ボス(IsBoss)またはメタル系(immuneToAllAilments)には無効。
    /// 敵→プレイヤー: 制限なし。
    /// 例: hpDependentPercent=20, 最大HP500 → ダメージ100
    /// </summary>
    MaxHpPercent = 3,

    /// <summary>
    /// 使用者の現在HPをダメージとして与える。
    /// ダメージ = 使用者の現在HP
    /// 他のHP依存タイプと異なり、防御ダイス・属性耐性を適用する。
    /// defenseIgnoreRate で防御貫通率を制御可能。
    /// skillAttribute で属性を設定可能(None なら無属性)。
    /// 主に自爆系の敵が SelfDestructEffectData と組み合わせて使用する。
    /// 例: HP500の敵が自爆 → 500ダメージ(防御・耐性で軽減あり)
    /// </summary>
    CurrentHpDamage = 4,

}



/// <summary>
/// スキル1つ分のマスターデータ。
/// プレイヤー用(武器スキル/魔法スキル)と敵用の両方をこの1つのクラスで表現する。
///
/// 【統合設計】
///   プレイヤー専用フィールド(skillSource, cooldownTurns, mpCost)はモンスター使用時は参照しない。
///   モンスター専用フィールド(actionType)はプレイヤー使用時は参照しない。
///
/// 【追加効果システム】
///   additionalEffects リストに SkillEffectEntry を追加することで、
///   毒付与・レベルドレイン・回復等の効果を任意に組み合わせられる。
///   各効果のパラメータ(付与率・回復量等)は SkillEffectEntry 側で持つため、
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
///
/// 【マルチ属性ヒット】
///   multiHitEntries が設定されている場合（Length > 0）、
///   ヒットごとに属性・威力倍率・追加固定ダメージを個別に指定できる。
///   配列の長さは hitCount と一致させること。
///   未設定（空配列）の場合は全ヒットが skillAttribute / damageMultiplier を使用（従来動作）。
///   GetHitAttribute / GetHitDamageMultiplier / GetHitBonusDamage ヘルパーで参照する。
///
/// 【HP依存ダメージ】
///   hpDependentType != None の場合、通常のダメージ計算をバイパスし、
///   対象の現在HPに基づいたダメージを与える。
///   防御/属性耐性/バフ/クリティカルは全てスキップ。
///   命中判定は通常通り行う。多段攻撃とは併用不可(hitCount=1前提)。
/// </summary>
[CreateAssetMenu(menuName = "Skills/Skill Data")]
public class SkillData : ScriptableObject
{
    [Header("Basic")]
    public string skillId;
    public string skillName;
    [TextArea] public string description;

    [Header("Skill Source")]
    [Tooltip("Weapon = 武器スキル(CT制、MP消費なし)、Magic = 魔法スキル(MP制、CT なし)\n"
           + "モンスター使用時は参照しない。")]
    public SkillSource skillSource = SkillSource.Weapon;

    // =========================================================
    // モンスター行動タイプ(モンスター使用時のみ参照)
    // =========================================================

    [Header("Monster Action Type")]
    [Tooltip("モンスターの行動の種類。プレイヤー使用時は無視される。\n"
           + "Idle = 何もしない\n"
           + "SkillAttack = 下記のパラメータでダメージ計算(通常攻撃は倍率1.0で表現)\n"
           + "Preemptive = 先制攻撃(プレイヤー行動選択後、プレイヤー行動の前に割り込む)\n"
           + "FoodRaid = 食い荒らし(プレイヤーの消費アイテムからランダムに1つを食べる)")]
    public MonsterActionType actionType = MonsterActionType.SkillAttack;

    [Header("Attribute / Damage")]
    /// <summary>スキルの攻撃属性。武器スキル・魔法スキル共通。</summary>
    public WeaponAttribute skillAttribute = WeaponAttribute.Strike;

    /// <summary>
    /// スキルの物理/魔法区分。
    /// 敵の防御ダイス計算に使用する(物理→Monster.Defense、魔法→Monster.MagicDefense)。
    ///
    /// 設定例:
    ///   パワーアタック(物理剣攻撃)    → Physical
    ///   ファイアボール(炎魔法)         → Magical
    ///   ライトニング(雷魔法)           → Magical
    ///   火炎斬り(炎属性・物理攻撃)     → Physical(属性は Fire, DamageCategory は Physical)
    /// </summary>
    [Tooltip("物理か魔法か。敵の防御ダイスの計算に影響する(物理→Defense、魔法→MagicDefense)")]
    public DamageCategory damageCategory = DamageCategory.Physical;

    /// <summary>
    /// ダメージ倍率。武器スキルで使用。
    /// 例: 通常攻撃 = 1.0、強撃 = 2.0(STR + 武器攻撃力 の2倍ダメージ)
    /// 魔法スキルで倍率ベースにしたい場合にも使える。
    /// 0 の場合は bonusDamage のみ使用。
    /// damageMultiplier == 0 かつ bonusDamage == 0 → 非ダメージスキル(追加効果のみ)。
    /// </summary>
    public float damageMultiplier;

    /// <summary>
    /// 追加固定ダメージ。倍率ダメージに加算される。
    /// 計算式: (Attack × damageMultiplier) + bonusDamage
    ///
    /// 表現パターン:
    ///   固定ダメージのみ: damageMultiplier=0, bonusDamage=10  → 固定10(旧fixedDamage相当)
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

    // =========================================================
    // 乱数ダメージ(追加)
    // =========================================================

    [Header("Random Damage")]
    [Tooltip("乱数ダメージの最大値。0 = 使用しない。\n"
           + "1以上の場合、1〜この値の乱数がベースダメージになる。\n"
           + "属性は None(無属性)を設定すること。\n"
           + "クリティカルは無効、防御ダイスは有効。\n"
           + "damageMultiplier / bonusDamage は無視される。\n"
           + "例: randomDamageMax=100 → 1〜100のランダムダメージ")]
    public int randomDamageMax = 0;

    // =========================================================
    // HP依存ダメージ(追加)
    // =========================================================

    [Header("HP Dependent Damage")]
    [Tooltip("HP依存ダメージの種類。None = 通常スキル。\n"
           + "HalfCurrentHp = 対象の現在HPを半分にする(切り下げ)。\n"
           + "ReduceToOne = 対象のHPを1にする。\n"
           + "設定時は damageMultiplier=0, bonusDamage=0 にすること。\n"
           + "防御/属性耐性/バフ/クリティカルは全てスキップ。\n"
           + "命中判定は通常通り行う。\n"
           + "多段攻撃とは併用不可(hitCount=1前提)。")]
    public HpDependentType hpDependentType = HpDependentType.None;

    [Tooltip("MaxHpPercent 用: 最大HPの何%をダメージにするか。\n"
       + "例: 20 = 最大HPの20%。HalfCurrentHp / ReduceToOne では使用しない。")]
    [Range(1, 100)]
    public int hpDependentPercent = 20;

    // =========================================================
    // 防御貫通(追加)
    // =========================================================

    [Header("Defense Penetration")]
    [Tooltip("防御貫通率。0.0〜1.0。デフォルト0(貫通なし)。\n"
           + "このスキルで攻撃する際、対象の防御力をこの割合だけ無視する。\n"
           + "0.5 = 防御力50%無視(アシッドブレス等)。\n"
           + "1.0 = 防御力完全無視。\n"
           + "敵→プレイヤー: 防御行動の2倍化はこの適用後に乗算される。\n"
           + "プレイヤー→敵: 敵の防御ダイスに渡す前に適用。")]
    [Range(0f, 1f)]
    public float defenseIgnoreRate = 0f;


    [Header("Cost")]
    /// <summary>
    /// クールタイム(ターン数)。武器スキル用。
    /// 魔法スキルでは通常 0(MP消費で制御するため)。
    /// </summary>
    public int cooldownTurns;

    /// <summary>
    /// MP消費量。魔法スキル用。
    /// 武器スキルでは通常 0(CT制で制御するため)。
    /// 将来的に「CTもMPも消費する」スキルも表現可能。
    /// </summary>
    public int mpCost;

    // =========================================================
    // 基礎命中率
    // =========================================================

    [Header("Hit Rate")]
    /// <summary>
    /// このスキルの基礎命中率(%)。デフォルト95。
    /// プレイヤースキル攻撃の命中率 = baseHitRate × (1 - (敵回避力 - 命中力)/100)
    /// ただし最低25%保証。
    /// 武器スキル・魔法スキル共通で使用する。
    /// </summary>
    [Tooltip("このスキルの基礎命中率(%)。デフォルト95。\n"
           + "プレイヤー攻撃の命中率 = baseHitRate × (1 - (敵回避力 - 命中力)/100)\n"
           + "ただし最低25%保証。")]
    public int baseHitRate = 95;

    // =========================================================
    // 多段攻撃(追加)
    // =========================================================

    [Header("Multi-Hit")]
    [Tooltip("攻撃回数。デフォルト1(単発)。\n"
           + "2以上にすると、命中判定→ダメージ計算を hitCount 回繰り返す。\n"
           + "各ヒットは独立して命中・防御ダイス・クリティカルを判定する。\n"
           + "途中で対象HPが0になったら残りをスキップする。\n"
           + "追加効果は全ヒット完了後に1回だけ実行する。\n"
           + "例: 三連突き = hitCount=3, damageMultiplier=0.6")]
    public int hitCount = 1;

    // =========================================================
    // マルチ属性ヒット(追加)
    // =========================================================

    [Header("Multi-Hit Attribute Override")]
    [Tooltip("ヒットごとの属性・威力を個別指定する。\n"
           + "配列の長さ = hitCount にすること。\n"
           + "空(Length==0)の場合は全ヒットが skillAttribute / damageMultiplier を使用（従来動作）。\n"
           + "例: 炎→氷→雷の3連撃 = hitCount=3, multiHitEntries[0]=Fire, [1]=Ice, [2]=Thunder")]
    public MultiHitEntry[] multiHitEntries;

    [Header("Field Usage")]
    [Tooltip("true の場合、塔シーン(非バトル時)でも使用可能。\n"
       + "所持魔法のうちこのフラグが true のものだけが塔の魔法ドロップダウンに表示される。\n"
       + "ヒール・毒消し等の回復系魔法に設定する。")]
    public bool noBattleOk = false;

    // =========================================================
    // 追加効果リスト
    // =========================================================

    [Header("Additional Effects")]
    [Tooltip("スキルの追加効果リスト。\n"
           + "命中後(またはダメージ計算後)に順番に実行される。\n"
           + "非ダメージスキル(倍率0 & ボーナス0)では追加効果のみ実行される。")]
    public List<SkillEffectEntry> additionalEffects = new List<SkillEffectEntry>();


    // 力溜め→攻撃のようなターンをまたがった行動用
    [Tooltip("敵がこのスキルを使用した次のターンに強制実行するスキル。\n"
       + "null の場合は通常の行動抽選を行う。")]
    public SkillData enemyNextForceSkill;

    // =========================================================
    // 敵行動：再抽選条件（スマート抽選）
    // =========================================================

    [Header("Enemy Reroll")]
    [Tooltip("敵がこのスキルを選んだとき、指定条件を満たしていたら再抽選する。\n"
           + "None = 再抽選なし（従来通り）\n"
           + "TargetAlreadyAilment = 対象が既にこのスキルの状態異常にかかっている\n"
           + "UserNotAilment = 使用者がこのスキルの状態異常にかかっていない（怒り等の自己付与用）\n"
           + "UserHpAboveHalf = 使用者のHPが50%以上（回復技用）\n"
           + "UserHpAboveThreshold = 使用者のHPがカスタム閾値以上（汎用版）")]
    public RerollCondition rerollCondition = RerollCondition.None;

    [Tooltip("UserHpAboveThreshold 用: 再抽選する HP 閾値（0〜1）。\n"
           + "例: 0.8 = HPが80%以上なら再抽選。\n"
           + "UserHpAboveHalf の場合はこの値を無視して 0.5 固定。")]
    [Range(0f, 1f)]
    public float rerollHpThreshold = 0.5f;


    // =========================================================
    // ヘルパープロパティ
    // =========================================================

    /// <summary>
    /// ダメージを与えないスキルかどうか。
    /// damageMultiplier == 0 かつ bonusDamage == 0 かつ randomDamageMax == 0
    /// かつ hpDependentType == None の場合 true。
    /// HP依存ダメージが設定されている場合はダメージスキル扱い。
    /// </summary>
    public bool IsNonDamage => damageMultiplier <= 0f && bonusDamage <= 0 && randomDamageMax <= 0
                               && hpDependentType == HpDependentType.None
                               && !HasMultiHitEntries;

    /// <summary>
    /// HP依存ダメージスキルかどうか。
    /// </summary>
    public bool IsHpDependent => hpDependentType != HpDependentType.None;


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

    /// <summary>
    /// マルチ属性ヒットが有効かどうか。
    /// multiHitEntries が設定されていて要素が1つ以上ある場合 true。
    /// </summary>
    public bool HasMultiHitEntries =>
        multiHitEntries != null && multiHitEntries.Length > 0;

    // =========================================================
    // マルチ属性ヒット ヘルパー
    // =========================================================

    /// <summary>
    /// ヒットインデックスに応じた攻撃属性を返す。
    /// multiHitEntries が設定されていればそちらを使用、なければ skillAttribute。
    /// </summary>
    public WeaponAttribute GetHitAttribute(int hitIndex)
    {
        if (multiHitEntries != null && multiHitEntries.Length > hitIndex)
            return multiHitEntries[hitIndex].attribute;
        return skillAttribute;
    }

    /// <summary>
    /// ヒットインデックスに応じたダメージ倍率を返す。
    /// multiHitEntries が設定されていてかつ値が 0 より大きければそちらを使用。
    /// 0 以下の場合はスキル本体の damageMultiplier をフォールバック。
    /// </summary>
    public float GetHitDamageMultiplier(int hitIndex)
    {
        if (multiHitEntries != null && multiHitEntries.Length > hitIndex
            && multiHitEntries[hitIndex].damageMultiplier > 0f)
            return multiHitEntries[hitIndex].damageMultiplier;
        return damageMultiplier;
    }

    /// <summary>
    /// ヒットインデックスに応じた追加固定ダメージを返す。
    /// multiHitEntries が設定されていてかつ bonusDamage >= 0 ならそちらを使用。
    /// -1 の場合はスキル本体の bonusDamage をフォールバック。
    /// </summary>
    public int GetHitBonusDamage(int hitIndex)
    {
        if (multiHitEntries != null && multiHitEntries.Length > hitIndex
            && multiHitEntries[hitIndex].bonusDamage >= 0)
            return multiHitEntries[hitIndex].bonusDamage;
        return bonusDamage;
    }


    /// <summary>
    /// 敵を対象とする非ダメージスキルかどうか。
    /// IsNonDamage == true かつ、追加効果に敵対象の効果を含む場合 true。
    ///
    /// 敵対象の効果:
    ///   StatusAilmentEffectData (ailmentMode == Inflict) のうち:
    ///     - 毒/麻痺/暗闇/気絶: 相手に付与 → hostile
    ///     - 怒り: 自分に付与だが、hostile 判定なしで問題ない
    ///     - デバフ(Down系): 相手に付与 → hostile
    ///     - バフ(Up系): 自分に付与 → hostile ではない
    ///   LevelDrainEffectData → hostile
    ///
    /// true の場合、非ダメージスキルでも回避判定を行う。
    /// false の場合(ヒール、デポイズ、自己バフ等)、回避判定をスキップする。
    /// </summary>
    public bool IsHostileNonDamage
    {
        get
        {
            if (!IsNonDamage) return false;
            if (additionalEffects == null) return false;
            for (int i = 0; i < additionalEffects.Count; i++)
            {
                var entry = additionalEffects[i];
                if (entry == null || entry.effectData == null) continue;

                if (entry.effectData is StatusAilmentEffectData
                    && entry.ailmentMode == AilmentMode.Inflict)
                {
                    // バフ(Up系)は自分自身に付与するので hostile ではない
                    if (StatusEffectSystem.IsBuffDebuff(entry.targetStatusEffect)
                        && !StatusEffectSystem.IsDebuff(entry.targetStatusEffect))
                    {
                        continue; // Up系 → hostile ではないのでスキップ
                    }

                    // 怒り(Rage)も自分に付与するので hostile ではない
                    if (entry.targetStatusEffect == StatusEffect.Rage)
                    {
                        continue;
                    }

                    return true;
                }

                if (entry.effectData is LevelDrainEffectData)
                    return true;
            }
            return false;
        }
    }


}

/// <summary>
/// スキルの発動元を区別する列挙型。
/// Weapon: 装備中の武器に紐づくスキル。クールタイム制。
/// Magic:  魔法アイテム所持で使えるスキル。MP消費制。
/// </summary>
public enum SkillSource
{
    Weapon, // 武器スキル(CT制、装備武器に紐づく)
    Magic,  // 魔法スキル(MP制、魔法アイテム所持で使用可能)
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
    /// <summary>何もしない(ターン終了)。</summary>
    Idle = 0,

    /// <summary>
    /// [廃止] 旧・通常攻撃。処理上は SkillAttack と同一扱い。
    /// 新規作成時は SkillAttack(damageMultiplier=1)を使用すること。
    /// enum 値を維持するのは既存アセットの互換性のため。
    /// </summary>
    [System.Obsolete("NormalAttack は廃止。SkillAttack(damageMultiplier=1)を使用してください。")]
    NormalAttack = 1,

    /// <summary>
    /// スキル攻撃(SkillData のパラメータでダメージ計算)。
    /// baseDamage = (Monster.Attack × damageMultiplier) + bonusDamage
    /// すべて 0 なら非ダメージスキル(追加効果のみ)。
    /// 通常攻撃は damageMultiplier=1 で表現する。
    /// </summary>
    SkillAttack = 2,

    /// <summary>
    /// 先制攻撃。プレイヤーの行動選択後、プレイヤー行動実行の前に割り込む。
    /// ダメージ計算は SkillAttack と同じ(SkillData のパラメータを使用)。
    /// ターン開始時の事前抽選で選ばれた場合のみ発動する。
    /// </summary>
    Preemptive = 3,

    /// <summary>
    /// 食い荒らし。プレイヤーの消費アイテム(Consumable)からランダムに1つ選んで消費する。
    /// 選ばれたアイテムが battleDamage > 0(攻撃アイテム)の場合:
    ///   → 「まずい!」敵は最大HPの半分のダメージを受ける(即死可、クランプなし)
    /// 選ばれたアイテムがそれ以外の場合:
    ///   → 「うまい!」敵は最大HPの半分を回復する(MaxHpでクランプ)
    /// プレイヤーが消費アイテムを持っていない場合:
    ///   → 「道具袋を漁ったが、不満そうに戻って行った...」(不発)
    /// 命中判定・防御・耐性・バフは一切考慮しない。
    /// 多段・追加効果とは非対応(SkillData の他フィールドは参照しない)。
    /// </summary>
    FoodRaid = 4,
}

/// <summary>
/// 敵の行動抽選時の再抽選条件。
/// SkillData に設定し、SelectEnemyAction() で使用する。
///
/// 【仕様】
///   抽選されたスキルの rerollCondition が None 以外の場合、
///   条件を満たしていたら別のスキルを再抽選する（最大5回）。
///   5回超えたら元の抽選結果をそのまま使用する（無限ループ防止）。
///
/// 【使用例】
///   毒攻撃スキル:       TargetAlreadyAilment → プレイヤーが既に毒なら再抽選
///   バーサク(自己付与):  UserNotAilment       → 敵が怒り状態でないなら再抽選（＝既に怒りなら使わない）
///   回復スキル:         UserHpAboveHalf      → HPが50%以上なら再抽選
///   緊急回復スキル:     UserHpAboveThreshold → HPがカスタム閾値以上なら再抽選
/// </summary>
public enum RerollCondition
{
    /// <summary>再抽選なし（従来通り）。</summary>
    None = 0,

    /// <summary>
    /// 対象（プレイヤー）が既にこのスキルの状態異常にかかっている場合に再抽選。
    /// additionalEffects から StatusAilmentEffectData (Inflict) の targetStatusEffect を探し、
    /// プレイヤーがその状態異常にかかっていれば再抽選する。
    /// バフ/デバフ系は BattleBuffState から判定する。
    /// </summary>
    TargetAlreadyAilment = 1,

    /// <summary>
    /// 使用者（敵）がこのスキルの状態異常にかかっていない場合に再抽選。
    /// 自己付与型スキル（怒り等）で、既にかかっているときだけ使いたい場合の逆条件。
    /// ※ 通常は「かかっていないなら使う意味がない自己回復」のような用途。
    /// 実際には「かかっていない＝再抽選」なので、既にかかっているときだけ通過する。
    /// </summary>
    UserNotAilment = 2,

    /// <summary>
    /// 使用者（敵）のHPが50%以上の場合に再抽選。回復技の無駄撃ち防止。
    /// </summary>
    UserHpAboveHalf = 3,

    /// <summary>
    /// 使用者（敵）のHPがカスタム閾値以上の場合に再抽選（汎用版）。
    /// SkillData.rerollHpThreshold で閾値を指定する。
    /// </summary>
    UserHpAboveThreshold = 4,
}

/// <summary>
/// マルチ属性ヒット用のエントリ。
/// ヒットごとに属性・威力倍率・追加固定ダメージを個別指定する。
///
/// 【使用方法】
///   SkillData.multiHitEntries にこの構造体の配列を設定する。
///   配列の長さは hitCount と一致させること。
///   未設定（空配列）の場合は従来動作（全ヒット同一属性）。
///
/// 【フォールバック】
///   damageMultiplier = 0  → スキル本体の damageMultiplier を使用
///   bonusDamage      = -1 → スキル本体の bonusDamage を使用
///
/// 【設定例: 炎→氷→雷の3連撃】
///   hitCount = 3
///   multiHitEntries[0] = { Fire,    0.8, -1 }
///   multiHitEntries[1] = { Ice,     0.8, -1 }
///   multiHitEntries[2] = { Thunder, 0.8, -1 }
/// </summary>
[System.Serializable]
public struct MultiHitEntry
{
    [Tooltip("このヒットの攻撃属性")]
    public WeaponAttribute attribute;

    [Tooltip("ダメージ倍率の上書き。0 = スキルの damageMultiplier を使用")]
    public float damageMultiplier;

    [Tooltip("追加固定ダメージの上書き。-1 = スキルの bonusDamage を使用")]
    public int bonusDamage;
}