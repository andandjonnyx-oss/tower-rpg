using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ダンジョン背景の種類。現在地（floor / step）から決まる。
/// </summary>
public enum DungeonBackgroundType
{
    /// <summary>塔内部（通常ステップ）。</summary>
    Interior = 0,
    /// <summary>塔内部・階段が見える（階段ステップ）。</summary>
    Stairs = 1,
    /// <summary>頂上（100F 最終ステップ）。</summary>
    Summit = 2,
}

/// <summary>
/// 現在地から背景タイプを判定し、3枚の背景 Sprite から適切なものを適用する共通ヘルパー。
/// Tower シーン・Battle シーンの両方から利用する。
///
/// ステップ仕様:
///   通常階 (1〜99F)  … STEP 1〜19 = 塔内部 / STEP 20 = 階段
///   100F            … STEP 1〜18 = 塔内部 / STEP 19 = 階段 / STEP 20 = 頂上
/// </summary>
public static class DungeonBackground
{
    /// <summary>頂上に到達するフロア（最上階）。</summary>
    public const int SummitFloor = 100;

    /// <summary>各階の最終ステップ番号（= 階段ステップ。通常はこの STEP で次の階へ進む）。</summary>
    public const int MaxStepPerFloor = 20;

    /// <summary>
    /// 現在地（floor / step）から背景タイプを判定する。
    /// </summary>
    /// <param name="floor">現在の階数（1〜100）。</param>
    /// <param name="step">現在のステップ（1〜20）。</param>
    public static DungeonBackgroundType Resolve(int floor, int step)
    {
        if (floor >= SummitFloor)
        {
            // 100F: STEP20 = 頂上, STEP19 = 階段, それ以外 = 塔内部
            if (step >= MaxStepPerFloor) return DungeonBackgroundType.Summit;
            if (step == MaxStepPerFloor - 1) return DungeonBackgroundType.Stairs;
            return DungeonBackgroundType.Interior;
        }

        // 通常階: STEP20 = 階段, それ以外 = 塔内部
        if (step >= MaxStepPerFloor) return DungeonBackgroundType.Stairs;
        return DungeonBackgroundType.Interior;
    }

    /// <summary>
    /// タイプに対応する Sprite を 3 枚の中から返す。
    /// 対応する Sprite が null の場合は interior にフォールバックする。
    /// </summary>
    public static Sprite Pick(
        DungeonBackgroundType type,
        Sprite interior, Sprite stairs, Sprite summit)
    {
        switch (type)
        {
            case DungeonBackgroundType.Summit:
                return summit != null ? summit : interior;
            case DungeonBackgroundType.Stairs:
                return stairs != null ? stairs : interior;
            default:
                return interior;
        }
    }

    /// <summary>
    /// 現在地に応じて backgroundImage に適切な背景 Sprite を適用する。
    /// backgroundImage が null の場合は何もしない（背景未設定のシーンでも安全）。
    /// </summary>
    /// <param name="backgroundImage">背景表示用 Image（Canvas 最背面）。</param>
    /// <param name="floor">現在の階数。</param>
    /// <param name="step">現在のステップ。</param>
    /// <param name="interior">塔内部の背景 Sprite。</param>
    /// <param name="stairs">階段の背景 Sprite。</param>
    /// <param name="summit">頂上の背景 Sprite。</param>
    public static void Apply(
        Image backgroundImage,
        int floor, int step,
        Sprite interior, Sprite stairs, Sprite summit)
    {
        if (backgroundImage == null) return;

        DungeonBackgroundType type = Resolve(floor, step);
        Sprite sprite = Pick(type, interior, stairs, summit);

        if (sprite == null)
        {
            // どの Sprite も設定されていない場合は背景 Image を非表示にする
            backgroundImage.enabled = false;
            return;
        }

        backgroundImage.enabled = true;
        backgroundImage.sprite = sprite;
    }
}