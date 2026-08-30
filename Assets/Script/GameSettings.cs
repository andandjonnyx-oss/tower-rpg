using UnityEngine;

/// <summary>
/// 音量以外のゲームプレイ設定。SettingsStore（settings.json）で永続化する。
/// （音量設定は AudioManager が管理しているため、ここでは扱わない）
///
/// 旧来は PlayerPrefs に直接書いていたが、Switch 対応のため SettingsStore に
/// 集約した（2026-08-30）。旧 PlayerPrefs からの移行は SettingsStore が行う。
/// 公開APIは変更していないので、呼び出し側は無改修。
/// </summary>
public static class GameSettings
{
    /// <summary>
    /// 魔法セレクターの選択保持オプション。既定は OFF。
    /// ON のとき、戦闘中（その戦闘の間）/ 塔内（戦闘に入るまでの間）で
    /// 前回選択した魔法を保持する。
    /// </summary>
    public static bool KeepMagicSelection
    {
        get => SettingsStore.Data.keepMagicSelection;
        set
        {
            SettingsStore.Data.keepMagicSelection = value;
            SettingsStore.MarkDirty();
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
        get => SettingsStore.Data.noItemMode;
        set
        {
            SettingsStore.Data.noItemMode = value;
            SettingsStore.MarkDirty();
        }
    }

    /// <summary>
    /// 利き手設定。既定は Right（右利き）。
    /// Left のとき、Tower の操作系UIを個別オフセットでずらす。
    /// </summary>
    public static Handedness Handedness
    {
        get => (Handedness)SettingsStore.Data.handedness;
        set
        {
            SettingsStore.Data.handedness = (int)value;
            SettingsStore.MarkDirty();
        }
    }

    /// <summary>左利き設定かどうかの簡易判定。</summary>
    public static bool IsLeftHanded => Handedness == Handedness.Left;

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
        get => SettingsStore.Data.analyticsOptOut;
        set
        {
            SettingsStore.Data.analyticsOptOut = value;
            SettingsStore.MarkDirty();
        }
    }
}
