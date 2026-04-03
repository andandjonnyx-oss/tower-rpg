using UnityEngine;

/// <summary>
/// 自爆エフェクトの ScriptableObject。
/// スキル使用者（通常はモンスター）が自滅する効果。
///
/// 【用途】
///   ミツバチの「毒の一刺し」など、自身を犠牲にして攻撃する技に使用。
///   ダメージ計算は SkillData 側（fixedDamage / damageMultiplier）で行い、
///   自爆はダメージ適用後の追加効果として処理する。
///
/// 【パラメータ（SkillEffectEntry 側）】
///   chance: 自爆の発動率（%）。通常は 100（確定自爆）。
///   intValue: 使用しない。
///
/// 【処理】
///   SkillEffectProcessor で処理。
///   isPlayerAttack == false の場合: 敵の currentHp を 0 にする。
///   isPlayerAttack == true の場合: プレイヤーの currentHp を 0 にする（通常は使わない）。
/// </summary>
[CreateAssetMenu(menuName = "Skills/Effects/Self Destruct Effect")]
public class SelfDestructEffectData : SkillEffectData
{
    // パラメータなし（発動率は SkillEffectEntry.chance で制御）
}