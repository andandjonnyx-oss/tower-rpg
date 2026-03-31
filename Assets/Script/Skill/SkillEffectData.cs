using UnityEngine;

/// <summary>
/// スキル追加効果の抽象基底クラス（ScriptableObject）。
/// 各効果タイプ（毒付与・レベルドレイン・回復等）がこのクラスを継承する。
///
/// 【設計思想】
///   SkillEffectData アセットは「効果の種類」を表す。
///   効果ごとの数値パラメータ（付与率・回復量等）は SkillEffectEntry 側で持つため、
///   同じ PoisonEffectData アセットを複数のスキルで共有しつつ、
///   スキルごとに異なるパラメータ（毒付与率80% / 40%等）を設定できる。
///
///   新しい効果タイプを追加する手順:
///     1. SkillEffectData を継承した新クラスを作成（CreateAssetMenu 付き）
///     2. SkillEffectProcessor に処理を追加
///     3. Unity で ScriptableObject アセットを1つ作成
///     4. スキルアセットの additionalEffects に追加
/// </summary>
public abstract class SkillEffectData : ScriptableObject
{
    [Tooltip("効果の識別名（Editor表示用）")]
    public string effectName;

    [TextArea(2, 4)]
    [Tooltip("効果の説明（Editor・図鑑用）")]
    public string description;
}