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
///
///   戦闘限定デバフ:
///     Stun     - 気絶（1ターン限定）
///
///   戦闘限定バフ（使用者自身に付与）:
///     Rage     - 怒り（バーサク）。攻撃力UP+通常攻撃のみ。3ターン or 戦闘終了で解除。
///
///   予約（未使用）:
///     Sleep, Silence, Burn, Freeze
/// </summary>
public enum StatusEffect
{
    None,
    Poison,    // 毒
    Paralyze,  // 麻痺
    Sleep,     // 睡眠（予約）
    Blind,     // 暗闘
    Silence,   // 沈黙（予約）
    Burn,      // 火傷（予約）
    Freeze,    // 凍結（予約）
    Stun,      // 気絶
    Rage,      // 怒り（バーサク）
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
            case StatusEffect.Stun: return "気絶";
            case StatusEffect.Rage: return "怒り";
            default: return effect.ToString();
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