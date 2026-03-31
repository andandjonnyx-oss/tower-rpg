using UnityEngine;

/// <summary>
/// 状態異常効果（付与・回復の両方を扱う）。
/// 旧 PoisonEffectData を改名・拡張したもの。
///
/// 【設計】
///   このSOアセットは「状態異常系の効果である」ことを示すマーカー。
///   具体的な状態異常の種類（毒・麻痺等）や付与/回復の区別は
///   SkillEffectEntry 側で設定する。
///
/// 【パラメータ（SkillEffectEntry 側）】
///   ailmentMode:       付与 / 回復
///   targetStatusEffect: Poison / Paralyze / Sleep / ...
///   chance:            付与時の基礎付与率（%）。回復時は不要。
///
/// 【アセット作成】
///   Create > Skills > Effects > StatusAilment Effect で作成。
///   通常は1つだけ作成し、複数のスキルで共有する。
/// </summary>
[CreateAssetMenu(menuName = "Skills/Effects/StatusAilment Effect")]
public class StatusAilmentEffectData : SkillEffectData
{
    // パラメータは全て SkillEffectEntry 側で持つ。
    // 将来的に「状態異常の持続ターン数」等のジャンル固有設定を追加する場合はここに追加する。
}

/// <summary>
/// 状態異常効果のモード。
/// 付与（敵/プレイヤーに状態異常をかける）か回復（自身の状態異常を治す）かを区別する。
/// </summary>
public enum AilmentMode
{
    /// <summary>対象に状態異常を付与する。</summary>
    Inflict,

    /// <summary>自身の状態異常を回復する。</summary>
    Cure,
}