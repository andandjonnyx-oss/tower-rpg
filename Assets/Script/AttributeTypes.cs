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
}

/// <summary>
/// 状態異常の列挙型。
/// </summary>
public enum StatusEffect
{
    None,
    Poison,    // 毒
    Paralyze,  // 麻痺
    Sleep,     // 睡眠
    Blind,     // 暗闇
    Silence,   // 沈黙
    Burn,      // 火傷
    Freeze,    // 凍結
    Stun,      // 気絶
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
            default: return effect.ToString();
        }
    }
}