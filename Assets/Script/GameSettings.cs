using UnityEngine;

/// <summary>
/// 音量以外のゲームプレイ設定。PlayerPrefs で永続化する。
/// （音量設定は AudioManager が管理しているため、ここでは扱わない）
/// </summary>
public static class GameSettings
{
    private const string KeyKeepMagicSelection = "opt_keepMagicSelection";
    private const string KeyNoItemMode = "opt_noItemMode";

    private static bool? keepMagicSelectionCache;
    private static bool? noItemModeCache;

    /// <summary>
    /// 魔法セレクターの選択保持オプション。既定は OFF。
    /// ON のとき、戦闘中（その戦闘の間）/ 塔内（戦闘に入るまでの間）で
    /// 前回選択した魔法を保持する。
    /// </summary>
    public static bool KeepMagicSelection
    {
        get
        {
            if (keepMagicSelectionCache == null)
                keepMagicSelectionCache = PlayerPrefs.GetInt(KeyKeepMagicSelection, 0) == 1;
            return keepMagicSelectionCache.Value;
        }
        set
        {
            keepMagicSelectionCache = value;
            PlayerPrefs.SetInt(KeyKeepMagicSelection, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    /// <summary>
    /// 「アイテムが出ないモード」オプション。既定は OFF。
    /// ON のとき、塔内のアイテム判定をスキップし、エンカウントのみ独立判定（実質20%）になる。
    /// OFF のとき、アイテム判定（先）→ すり抜けた残りに対しエンカウント判定（実質20%になるよう
    /// EncounterSystem.encounterRate を 0.25 に設定する）。
    /// </summary>
    public static bool NoItemMode
    {
        get
        {
            if (noItemModeCache == null)
                noItemModeCache = PlayerPrefs.GetInt(KeyNoItemMode, 0) == 1;
            return noItemModeCache.Value;
        }
        set
        {
            noItemModeCache = value;
            PlayerPrefs.SetInt(KeyNoItemMode, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    private const string KeyHandedness = "opt_handedness";
    private static int? handednessCache;

    /// <summary>
    /// 利き手設定。既定は Right（右利き）。
    /// Left のとき、Tower の操作系UIを個別オフセットでずらす。
    /// </summary>
    public static Handedness Handedness
    {
        get
        {
            if (handednessCache == null)
                handednessCache = PlayerPrefs.GetInt(KeyHandedness, (int)Handedness.Right);
            return (Handedness)handednessCache.Value;
        }
        set
        {
            handednessCache = (int)value;
            PlayerPrefs.SetInt(KeyHandedness, (int)value);
            PlayerPrefs.Save();
        }
    }

    /// <summary>左利き設定かどうかの簡易判定。</summary>
    public static bool IsLeftHanded => Handedness == Handedness.Left;

    private const string KeyAnalyticsOptOut = "opt_analyticsOptOut";
    private static bool? analyticsOptOutCache;

    /// <summary>
    /// プレイ統計（UGS Analytics）の送信を拒否するオプション。既定は OFF（＝送信する）。
    ///
    /// ON にすると地域に関わらず収集を停止する。OFF のときは
    /// UMP の同意状態で判断される（<see cref="AnalyticsManager.ApplyConsent"/> 参照）。
    /// つまり「同意が必要な地域で未同意」なら、この設定が OFF でも収集はされない。
    ///
    /// Option シーンにトグルを置く場合は、この値を読み書きしたうえで
    /// <see cref="AnalyticsManager.ApplyConsent"/> を呼べば即座に反映される。
    /// </summary>
    public static bool AnalyticsOptOut
    {
        get
        {
            if (analyticsOptOutCache == null)
                analyticsOptOutCache = PlayerPrefs.GetInt(KeyAnalyticsOptOut, 0) == 1;
            return analyticsOptOutCache.Value;
        }
        set
        {
            analyticsOptOutCache = value;
            PlayerPrefs.SetInt(KeyAnalyticsOptOut, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

}