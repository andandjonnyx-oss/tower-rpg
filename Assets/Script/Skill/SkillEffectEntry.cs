using System;
using UnityEngine;

/// <summary>
/// スキルの追加効果1つ分のエントリ。
/// SkillData.additionalEffects リストの要素として使用する。
///
/// 【構造】
///   effectData: どの効果か（ScriptableObject への参照）
///   以下のパラメータ: 効果ごとにスキル単位で個別設定する値
///
/// 【パラメータの使い分け】
///   全ての効果タイプで共通のパラメータフィールドを持ち、
///   効果タイプに応じて必要なフィールドだけを使用する。
///   使わないフィールドはインスペクターで非表示にする（カスタムエディタで対応予定）。
///
///   PoisonEffectData  → chance を使用（毒の基礎付与率）
///   LevelDrainEffectData → intValue を使用（ドレイン量、デフォルト1）
///   HealEffectData    → intValue を使用（回復量%）
/// </summary>
[Serializable]
public class SkillEffectEntry
{
    [Tooltip("追加効果の種類（ScriptableObject アセットへの参照）")]
    public SkillEffectData effectData;

    [Tooltip("効果の基礎発動率（%）。\n"
           + "PoisonEffectData: 毒の基礎付与率\n"
           + "その他: 100 = 確定発動")]
    [Range(0, 100)]
    public int chance = 100;

    [Tooltip("効果の数値パラメータ（整数）。\n"
           + "LevelDrainEffectData: ドレイン量（デフォルト1）\n"
           + "HealEffectData: 回復量（最大HPの%）")]
    public int intValue = 0;
}