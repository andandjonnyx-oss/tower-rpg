using UnityEngine;

/// <summary>
/// HP回復効果。
/// スキル使用時に自身のHPを回復する。
///
/// 【パラメータ（SkillEffectEntry 側）】
///   intValue: 回復量（最大HPの%）
///   chance:   発動率（%）。デフォルト100（確定発動）。
///
/// 【使用例】
///   吸血攻撃:  intValue=10（最大HPの10%回復）
///   癒しの光:  intValue=30（最大HPの30%回復、ダメージ倍率0の非ダメージスキル）
///
/// 【アセット作成】
///   Create > Skills > Effects > Heal Effect で作成。
/// </summary>
[CreateAssetMenu(menuName = "Skills/Effects/Heal Effect")]
public class HealEffectData : SkillEffectData
{
    // パラメータは SkillEffectEntry.intValue（回復量%）で持つ。
    // 将来的にMP回復等のバリエーションを作る場合はここに追加する。
}