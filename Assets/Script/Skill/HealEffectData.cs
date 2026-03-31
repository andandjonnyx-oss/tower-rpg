using UnityEngine;

/// <summary>
/// HP回復効果。
/// スキル使用時に自身のHPを回復する。
///
/// 【設計】
///   回復量の計算式タイプをこのSO側で持つ。
///   SOアセットを「固定回復」「INT依存回復」「STR依存回復」等と分けて作成し、
///   スキル側ではどのSOを使うかを選ぶだけで計算式が決まる。
///
/// 【formulaType の種類】
///   Fixed:          固定値回復。intValue がそのまま回復量。
///   MaxHpPercent:   最大HPの%回復。intValue が%値。
///   IntMultiplier:  INT × intValue で回復量を計算。
///   StrMultiplier:  STR × intValue で回復量を計算。
///
/// 【パラメータ（SkillEffectEntry 側）】
///   intValue: 回復量（固定値 / %値 / 倍率値。formulaType に応じて解釈が変わる）
///   chance:   発動率（%）。デフォルト100。
///
/// 【アセット作成】
///   Create > Skills > Effects > Heal Effect で作成。
///   計算式タイプ別にアセットを分けて作成する。
///   例: 「固定回復」アセット（formulaType=Fixed）、「INT回復」アセット（formulaType=IntMultiplier）
/// </summary>
[CreateAssetMenu(menuName = "Skills/Effects/Heal Effect")]
public class HealEffectData : SkillEffectData
{
    [Tooltip("回復量の計算式タイプ。\n"
           + "Fixed = 固定値回復\n"
           + "MaxHpPercent = 最大HPの%回復\n"
           + "IntMultiplier = INT × 倍率\n"
           + "StrMultiplier = STR × 倍率")]
    public HealFormulaType formulaType = HealFormulaType.Fixed;
}

/// <summary>
/// HP回復の計算式タイプ。
/// </summary>
public enum HealFormulaType
{
    /// <summary>固定値回復。intValue がそのまま回復量。</summary>
    Fixed,

    /// <summary>最大HPの%回復。intValue が%値（例: 10 = 最大HPの10%）。</summary>
    MaxHpPercent,

    /// <summary>INT × intValue で回復量を計算。</summary>
    IntMultiplier,

    /// <summary>STR × intValue で回復量を計算。</summary>
    StrMultiplier,
}