using UnityEngine;

/// <summary>
/// MPダメージ効果。
/// 敵がプレイヤーのMPを直接削る。
///
/// 【設計】
///   敵→プレイヤー方向のみ有効。
///   プレイヤー→敵方向は敵にMP概念がないため、発動しない（ログも出さない）。
///
///   固定値でそのまま削る。乗数・ステータス依存なし。
///   MDEFで軽減されない（素通し）。
///   MP0でクランプ（マイナスにはならない）。
///
/// 【パラメータ（SkillEffectEntry 側）】
///   intValue: MPダメージ量（固定値）。例: 20 = MPを20削る。
///   chance:   発動率（%）。デフォルト100。
///
/// 【アセット作成】
///   Create > Skills > Effects > MpDamage Effect で作成。
///   基本的にアセットは1つだけ作成すればよい（計算式タイプがないため）。
///   スキルごとに intValue で削り量を調整する。
///
/// 【用途例】
///   攻撃なしMP削りスキル: damageMultiplier=0, bonusDamage=0,
///                          additionalEffects に MpDamage(intValue=20) を設定
///   攻撃+MP削りスキル:     damageMultiplier=1, bonusDamage=0,
///                          additionalEffects に MpDamage(intValue=10) を設定
/// </summary>
[CreateAssetMenu(menuName = "Skills/Effects/MpDamage Effect")]
public class MpDamageEffectData : SkillEffectData
{
    // 計算式タイプ等は持たない。
    // intValue をそのままダメージ量として使用する。
}