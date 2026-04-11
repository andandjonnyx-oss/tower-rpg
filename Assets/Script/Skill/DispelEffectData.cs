using UnityEngine;

/// <summary>
/// バフ/デバフ解除効果（ディスペル）。
///
/// 【設計】
///   このSOアセットは「バフ/デバフを解除する効果である」ことを示すマーカー。
///   解除モード（DispelMode）はSOアセット側で持つ。
///   同じ DispelEffectData を複数のスキルで共有可能。
///
/// 【解除モードの種類】
///   CureOwnDebuffs  : 使用者自身のデバフ（Down系5種）を全解除する。
///                     バフや状態異常（毒・麻痺・暗闇・怒り）は解除しない。
///   DispelEnemyBuffs : 相手のバフ（Up系5種）を全解除する。
///                     デバフや状態異常は解除しない。
///   DispelAll        : 敵味方双方のバフ/デバフ（Up/Down系5種全て）を全解除する。
///                     状態異常（毒・麻痺・暗闇・怒り）は解除しない。
///
/// 【パラメータ（SkillEffectEntry 側）】
///   chance: 発動率（%）。100 = 確定。
///   ※ intValue / duration は使用しない。
///
/// 【アセット作成】
///   Create > Skills > Effects > Dispel Effect で作成。
///   モードごとに1つずつ作成する想定（計3つ）。
/// </summary>
[CreateAssetMenu(menuName = "Skills/Effects/Dispel Effect")]
public class DispelEffectData : SkillEffectData
{
    [Tooltip("解除モード。\n"
           + "CureOwnDebuffs = 自分のデバフを全解除\n"
           + "DispelEnemyBuffs = 相手のバフを全解除\n"
           + "DispelAll = 敵味方のバフ/デバフを全解除")]
    public DispelMode dispelMode = DispelMode.CureOwnDebuffs;
}

/// <summary>
/// ディスペル（バフ/デバフ解除）のモード。
/// </summary>
public enum DispelMode
{
    /// <summary>使用者自身のデバフ（Down系5種）を全解除する。</summary>
    CureOwnDebuffs,

    /// <summary>相手のバフ（Up系5種）を全解除する。</summary>
    DispelEnemyBuffs,

    /// <summary>敵味方双方のバフ/デバフを全解除する。</summary>
    DispelAll,
}