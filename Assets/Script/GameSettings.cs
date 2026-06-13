using UnityEngine;

/// <summary>
/// 音量以外のゲームプレイ設定。PlayerPrefs で永続化する。
/// （音量設定は AudioManager が管理しているため、ここでは扱わない）
/// </summary>
public static class GameSettings
{
    private const string KeyKeepMagicSelection = "opt_keepMagicSelection";

    private static bool? keepMagicSelectionCache;

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
}