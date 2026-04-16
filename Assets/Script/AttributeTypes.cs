/// <summary>
/// 武器属性の列挙型。
/// 内部処理は英語enum、表示は ToJapanese() で日本語に変換する。
/// </summary>
public enum WeaponAttribute
{
    Strike,   // 殴（素手）
    Slash,    // 斬
    Pierce,   // 突
    Fire,     // 火
    Ice,      // 氷
    Thunder,  // 雷
    Holy,     // 聖
    Dark,     // 闇
    None,     // 無（属性耐性の対象外）

}

/// <summary>
/// 状態異常の列挙型。
///
/// 【分類】
///   持続型デバフ（戦闘後も残る、塔移動中にも効果あり）:
///     Poison   - 毒
///     Paralyze - 麻痺
///     Blind    - 暗闇
///     Silence  - 沈黙（味方: 魔法使用不可 / 敵: 魔法系スキル70%失敗）
///     Petrify  - 石化（戦闘中も塔内でも継続、DEF/MDEF倍率持ち、残ターン0で敗北/撃破）
///
///   戦闘限定デバフ:
///     Stun     - 気絶（1ターン限定）
///
///   戦闘限定バフ（使用者自身に付与）:
///     Rage     - 怒り（バーサク）。攻撃力UP+通常攻撃のみ。3ターン or 戦闘終了で解除。
///
///   戦闘限定パラメータバフ/デバフ:
///     DefenseDown / DefenseUp     - 物理防御（実装済み）
///     AttackDown / AttackUp       - 攻撃力
///     MagicAttackDown / MagicAttackUp - 魔法攻撃力（敵の場合は回避力として扱う）
///     MagicDefenseDown / MagicDefenseUp - 魔法防御力
///     LuckDown / LuckUp           - 運
///
///   耐性カテゴリ（実際の状態異常ではなく、耐性指定用の集約シンボル）:
///     Debuff   - デバフ全体の耐性を表す。個別のDown系ではなく一括で耐性処理する。
///
///   予約（未使用）:
///     Sleep, Burn, Freeze
/// </summary>
public enum StatusEffect
{
    None,
    Poison,      // 毒
    Paralyze,    // 麻痺
    Sleep,       // 睡眠（予約）
    Blind,       // 暗闇
    Silence,     // 沈黙（実装済み）
    Burn,        // 火傷（予約）
    Freeze,      // 凍結（予約）
    Petrify,     // 石化（持続型デバフ・戦闘中も塔内でも継続）
    Stun,        // 気絶
    Rage,        // 怒り（バーサク）
    DefenseDown,      // 防御ダウン（戦闘限定バフ/デバフ）
    DefenseUp,        // 防御アップ（戦闘限定バフ/デバフ）
    AttackDown,       // 攻撃ダウン
    AttackUp,         // 攻撃アップ
    MagicAttackDown,  // 魔攻ダウン（敵: 回避ダウン）
    MagicAttackUp,    // 魔攻アップ（敵: 回避アップ）
    MagicDefenseDown, // 魔防ダウン
    MagicDefenseUp,   // 魔防アップ
    LuckDown,         // 運ダウン
    LuckUp,           // 運アップ
    Debuff,           // デバフ全体（耐性カテゴリ用）— 実際の状態異常ではなく、耐性指定用
}

/// <summary>
/// enum の表示名変換用拡張メソッド。
/// </summary>
public static class AttributeExtensions
{
    public static string ToJapanese(this WeaponAttribute attr)
    {
        switch (attr)
        {
            case WeaponAttribute.Strike: return "殴";
            case WeaponAttribute.Slash: return "斬";
            case WeaponAttribute.Pierce: return "突";
            case WeaponAttribute.Fire: return "火";
            case WeaponAttribute.Ice: return "氷";
            case WeaponAttribute.Thunder: return "雷";
            case WeaponAttribute.Holy: return "聖";
            case WeaponAttribute.Dark: return "闇";
            case WeaponAttribute.None: return "無";

            default: return attr.ToString();
        }
    }

    public static string ToJapanese(this StatusEffect effect)
    {
        switch (effect)
        {
            case StatusEffect.None: return "なし";
            case StatusEffect.Poison: return "毒";
            case StatusEffect.Paralyze: return "麻痺";
            case StatusEffect.Sleep: return "睡眠";
            case StatusEffect.Blind: return "暗闇";
            case StatusEffect.Silence: return "沈黙";
            case StatusEffect.Burn: return "火傷";
            case StatusEffect.Freeze: return "凍結";
            case StatusEffect.Petrify: return "石化";
            case StatusEffect.Stun: return "気絶";
            case StatusEffect.Rage: return "怒り";
            case StatusEffect.DefenseDown: return "防御↓";
            case StatusEffect.DefenseUp: return "防御↑";
            case StatusEffect.AttackDown: return "攻撃↓";
            case StatusEffect.AttackUp: return "攻撃↑";
            case StatusEffect.MagicAttackDown: return "魔攻↓";
            case StatusEffect.MagicAttackUp: return "魔攻↑";
            case StatusEffect.MagicDefenseDown: return "魔防↓";
            case StatusEffect.MagicDefenseUp: return "魔防↑";
            case StatusEffect.LuckDown: return "運↓";
            case StatusEffect.LuckUp: return "運↑";
            case StatusEffect.Debuff: return "デバフ";
            default: return effect.ToString();
        }
    }

    /// <summary>
    /// 敵にとっての表示名を返す。
    /// 敵は MagicAttack の概念がないため、MagicAttackDown/Up は「回避↓/↑」と表示する。
    /// </summary>
    public static string ToJapaneseEnemy(this StatusEffect effect)
    {
        switch (effect)
        {
            case StatusEffect.MagicAttackDown: return "回避↓";
            case StatusEffect.MagicAttackUp: return "回避↑";
            default: return effect.ToJapanese();
        }
    }

    public static string ToJapanese(this PassiveType type)
    {
        switch (type)
        {
            case PassiveType.AttributeResistance: return "属性耐性";
            case PassiveType.StatBonus: return "ステータスアップ";
            case PassiveType.AttributeAttackBonus: return "属性攻撃力";
            case PassiveType.MaxHpBonus: return "最大HP";
            case PassiveType.MaxMpBonus: return "最大MP";
            case PassiveType.StatusEffectResistance: return "状態異常耐性";
            case PassiveType.DefenseBonus: return "防御力";
            case PassiveType.MagicDefenseBonus: return "魔法防御力";
            // ---- 命中・回避・クリティカル（追加） ----
            case PassiveType.AccuracyBonus: return "命中力";
            case PassiveType.EvasionBonus: return "回避率";
            case PassiveType.CriticalBonus: return "クリティカル率";
            default: return type.ToString();
        }
    }

    public static string ToJapanese(this SkillSource source)
    {
        switch (source)
        {
            case SkillSource.Weapon: return "武器スキル";
            case SkillSource.Magic: return "魔法スキル";
            default: return source.ToString();
        }
    }
}