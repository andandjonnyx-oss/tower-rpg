using UnityEngine;

/// <summary>
/// 毒付与効果。
/// スキル命中時に対象に毒状態を付与する。
///
/// 【パラメータ（SkillEffectEntry 側）】
///   chance: 毒の基礎付与率（%）
///           実質付与率 = chance × (1 - 対象の毒耐性/100)
///
/// 【使用例】
///   毒魔法:    chance=80（高確率で毒付与）
///   毒攻撃:    chance=40（ダメージ＋低確率毒）
///   猛毒の牙:  chance=60（中確率毒付与）
///
/// 【アセット作成】
///   Create > Skills > Effects > Poison Effect で作成。
///   通常は1つだけ作成し、複数のスキルで共有する。
/// </summary>
[CreateAssetMenu(menuName = "Skills/Effects/Poison Effect")]
public class PoisonEffectData : SkillEffectData
{
    // パラメータは SkillEffectEntry.chance で持つため、
    // このクラス自体には追加フィールドなし。
    // 将来的に毒の種類（通常毒・猛毒等）を区別する場合はここに追加する。
}