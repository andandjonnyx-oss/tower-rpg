using System;
using UnityEngine;

/// <summary>
/// パッシブ効果1つ分の定義。
/// 魔法アイテムの passiveEffects 配列に入れて使う。
/// 例: 炎の護符 → effectType=AttributeResistance, targetAttribute=Fire, value=50
/// 例: 力の指輪 → effectType=StatBonus, targetStat=STR, value=5
///
/// 重複ルール（PassiveCalculator で処理）:
///   同じ effectType + 同じ target の効果が複数ある場合、
///   最大 value を 100% 適用し、2個目以降は value の 10% ずつ加算する。
/// </summary>
[Serializable]
public class PassiveEffect
{
    [Tooltip("この効果の種類")]
    public PassiveType effectType;

    [Header("Target (効果対象)")]
    [Tooltip("属性耐性・属性攻撃力ボーナスの場合の対象属性")]
    public WeaponAttribute targetAttribute;

    [Tooltip("ステータスボーナスの場合の対象ステータス")]
    public StatType targetStat;

    [Header("Value")]
    [Tooltip("効果値。耐性なら耐性値、ステアップなら上昇量")]
    public int value;
}

/// <summary>
/// パッシブ効果の種類。
/// 今後の拡張で項目を追加していく。
/// </summary>
public enum PassiveType
{
    /// <summary>属性耐性アップ。targetAttribute で対象属性を指定。</summary>
    AttributeResistance,

    /// <summary>基礎ステータスアップ。targetStat で対象ステータスを指定。</summary>
    StatBonus,

    /// <summary>属性攻撃力アップ。targetAttribute で対象属性を指定。</summary>
    AttributeAttackBonus,

    /// <summary>最大HPアップ。targetAttribute/targetStat は不要。</summary>
    MaxHpBonus,

    /// <summary>最大MPアップ。targetAttribute/targetStat は不要。</summary>
    MaxMpBonus,

    /// <summary>状態異常耐性アップ。将来拡張用。</summary>
    StatusEffectResistance,
}