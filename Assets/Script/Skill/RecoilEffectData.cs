using UnityEngine;

/// <summary>
/// 反動ダメージ効果データ（ScriptableObject）。
/// スキル命中時、与えたダメージの一定割合を使用者自身が受ける。
///
/// 【使用方法】
///   1. Unity で CreateAssetMenu → Skills/Effects/Recoil Effect を作成
///   2. スキルアセットの additionalEffects に追加
///   3. SkillEffectEntry.intValue に反射率（%）を設定
///      例: intValue=30 → 与ダメージの30%を自分が受ける
///
/// 【計算式】
///   recoilDamage = floor(lastDamageDealt × intValue / 100)
///   ※ intValue > 0 かつダメージ発生時、最低1ダメージ保証
///   ※ 与ダメージが0の場合は反動なし
///
/// 【対応範囲】
///   プレイヤー使用時のみ（isPlayerAttack == true）。
///   敵使用は現状未対応（ログ出力のみ）。
/// </summary>
[CreateAssetMenu(menuName = "Skills/Effects/Recoil Effect")]
public class RecoilEffectData : SkillEffectData
{
    // パラメータは SkillEffectEntry 側で持つ:
    //   intValue = 反射率（%）
    //   chance   = 発動率（%）
}