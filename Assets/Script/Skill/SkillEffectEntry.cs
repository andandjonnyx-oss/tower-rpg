using System;
using UnityEngine;

/// <summary>
/// スキルの追加効果1つ分のエントリ。
/// SkillData.additionalEffects リストの要素として使用する。
///
/// 【構造】
///   effectData: どの効果ジャンルか（ScriptableObject への参照）
///   以下のパラメータ: 効果ごとにスキル単位で個別設定する値
///
/// 【パラメータの使い分け（effectData のジャンル別）】
///
///   StatusAilmentEffectData:
///     ailmentMode        → Inflict（付与）/ Cure（回復）
///     targetStatusEffect → Poison / Paralyze / Sleep ...
///     chance             → 付与時の基礎付与率（%）。Cure時は不要。
///
///   HealEffectData:
///     intValue           → 回復量（計算式タイプにより解釈が変わる）
///     chance             → 発動率（%）
///     ※ 計算式タイプは HealEffectData（SO）側の formulaType で決まる
///
///   LevelDrainEffectData:
///     intValue           → ドレイン量（デフォルト1）
///     chance             → 発動率（%）
///
/// 【インスペクター表示】
///   SkillEffectEntryDrawer（カスタム PropertyDrawer）により、
///   effectData のジャンルに応じて必要なフィールドのみ表示する。
/// </summary>
[Serializable]
public class SkillEffectEntry
{
    [Tooltip("追加効果の種類（ScriptableObject アセットへの参照）")]
    public SkillEffectData effectData;

    // =========================================================
    // 状態異常系パラメータ（StatusAilmentEffectData 使用時）
    // =========================================================

    [Tooltip("状態異常の効果モード。\n"
           + "Inflict = 対象に状態異常を付与\n"
           + "Cure = 自身の状態異常を回復")]
    public AilmentMode ailmentMode = AilmentMode.Inflict;

    [Tooltip("対象の状態異常の種類。")]
    public StatusEffect targetStatusEffect = StatusEffect.Poison;

    // =========================================================
    // 共通パラメータ
    // =========================================================

    [Tooltip("効果の基礎発動率（%）。\n"
           + "StatusAilmentEffectData (Inflict): 状態異常の基礎付与率\n"
           + "LevelDrainEffectData: 発動率\n"
           + "HealEffectData: 発動率\n"
           + "StatusAilmentEffectData (Cure): 使用しない（確定回復）")]
    [Range(0, 100)]
    public int chance = 100;

    [Tooltip("効果の数値パラメータ（整数）。\n"
           + "LevelDrainEffectData: ドレイン量（デフォルト1）\n"
           + "HealEffectData: 回復量（計算式タイプにより解釈が変わる）")]
    public int intValue = 0;
}