using UnityEngine;

/// <summary>
/// レベルドレイン効果。
/// 対象のレベルを下げる（現在はプレイヤーのみ対象）。
///
/// 【パラメータ（SkillEffectEntry 側）】
///   intValue: ドレイン量（デフォルト1）。何レベル下げるか。
///   chance:   発動率（%）。デフォルト100（必中）。
///
/// 【仕様】
///   - プレイヤーのレベルを intValue 分下げる（最低レベル1）
///   - ステータスポイント（statusPoint）は変更しない
///   - 経験値は0にリセット、必要経験値を再計算
///   - レベル1の場合は効果なし
///
/// 【アセット作成】
///   Create > Skills > Effects > LevelDrain Effect で作成。
/// </summary>
[CreateAssetMenu(menuName = "Skills/Effects/LevelDrain Effect")]
public class LevelDrainEffectData : SkillEffectData
{
    // パラメータは SkillEffectEntry.intValue（ドレイン量）で持つ。
    // 将来的に「ステータスドレイン」等のバリエーションを作る場合はここに追加する。
}