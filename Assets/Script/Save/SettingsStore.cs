using System;
using UnityEngine;

/// <summary>
/// ゲーム設定（音量・オプション）の永続化データ。
/// セーブデータ（savedata.json）とは独立したファイルに保存する。
/// 「セーブデータ初期化」で設定まで消えないようにするための分離。
/// </summary>
[Serializable]
public class SettingsData
{
    // --- 音量（AudioManager） ---
    public float bgmVolume = 1f;
    public float seVolume = 1f;
    public bool bgmMuted = false;
    public bool seMuted = false;

    // --- ゲームプレイ設定（GameSettings） ---
    public bool keepMagicSelection = false;
    public bool noItemMode = false;
    public int handedness = (int)global::Handedness.Right;
    public bool analyticsOptOut = false;
}

/// <summary>
/// 設定の読み書きを一元管理する静的ストア。
///
/// 【経緯（2026-08-30）】
///   旧来は AudioManager / GameSettings がそれぞれ PlayerPrefs に直接書いていた。
///   Switch には PlayerPrefs に相当する暗黙ストレージが無いため、
///   セーブ層と同じ ISaveBackend（settings.json）に集約した。
///   書き込みは SaveManager と同じ遅延コミット方式で、SaveCommitter が
///   安全地点で CommitIfDirty() を呼ぶ。
///
/// 【旧 PlayerPrefs からの移行】
///   settings.json が無い初回起動時、旧キーが PlayerPrefs にあれば取り込む。
///   旧キーは消さない（このコミットを巻き戻しても設定が失われないように）。
///
/// 【使い方】
///   読み: SettingsStore.Data.bgmVolume
///   書き: SettingsStore.Data.bgmVolume = x; SettingsStore.MarkDirty();
///   MarkDirty() を忘れると次回起動で巻き戻る（即時反映はメモリ上で効くため
///   テストでは気付きにくい。書いたら必ず MarkDirty）。
/// </summary>
public static class SettingsStore
{
    private const string FileName = "settings.json";

    private static SettingsData data;
    private static bool dirty;

    /// <summary>設定データ本体。初回アクセス時にロード（無ければ移行→既定値）。</summary>
    public static SettingsData Data
    {
        get
        {
            EnsureLoaded();
            return data;
        }
    }

    /// <summary>未書き込みの変更があるか（デバッグ/テスト用）。</summary>
    public static bool IsDirty => dirty;

    /// <summary>Data を書き換えたら必ず呼ぶこと。実書き込みは安全地点まで遅延される。</summary>
    public static void MarkDirty()
    {
        dirty = true;
    }

    /// <summary>未書き込みの変更があればディスクへ確定する（SaveCommitter から呼ばれる）。</summary>
    public static void CommitIfDirty()
    {
        if (!dirty || data == null) return;
        string json = JsonUtility.ToJson(data, true);
        SaveBackend.Instance.WriteAllText(FileName, json);
        SaveBackend.Instance.Commit();
        dirty = false;
        Debug.Log($"[SettingsStore] 設定を保存: {FileName}");
    }

    private static void EnsureLoaded()
    {
        if (data != null) return;

        string json = SaveBackend.Instance.ReadAllText(FileName);
        if (!string.IsNullOrEmpty(json))
        {
            try
            {
                data = JsonUtility.FromJson<SettingsData>(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SettingsStore] settings.json の解析に失敗。既定値で継続: {e.Message}");
            }
        }

        if (data == null)
        {
            data = new SettingsData();
            MigrateFromPlayerPrefs();
            // 移行結果（または既定値）を次の安全地点で書き出す
            dirty = true;
        }
    }

    /// <summary>
    /// 旧 PlayerPrefs キーからの一回限りの移行。
    /// settings.json が存在しない場合のみ呼ばれる。キー名は旧実装
    /// （AudioManager / GameSettings）の定数と一致させてある。
    /// </summary>
    private static void MigrateFromPlayerPrefs()
    {
        bool found = false;

        if (PlayerPrefs.HasKey("audio_bgm_volume")) { data.bgmVolume = PlayerPrefs.GetFloat("audio_bgm_volume"); found = true; }
        if (PlayerPrefs.HasKey("audio_se_volume")) { data.seVolume = PlayerPrefs.GetFloat("audio_se_volume"); found = true; }
        if (PlayerPrefs.HasKey("audio_bgm_muted")) { data.bgmMuted = PlayerPrefs.GetInt("audio_bgm_muted") == 1; found = true; }
        if (PlayerPrefs.HasKey("audio_se_muted")) { data.seMuted = PlayerPrefs.GetInt("audio_se_muted") == 1; found = true; }
        if (PlayerPrefs.HasKey("opt_keepMagicSelection")) { data.keepMagicSelection = PlayerPrefs.GetInt("opt_keepMagicSelection") == 1; found = true; }
        if (PlayerPrefs.HasKey("opt_noItemMode")) { data.noItemMode = PlayerPrefs.GetInt("opt_noItemMode") == 1; found = true; }
        if (PlayerPrefs.HasKey("opt_handedness")) { data.handedness = PlayerPrefs.GetInt("opt_handedness"); found = true; }
        if (PlayerPrefs.HasKey("opt_analyticsOptOut")) { data.analyticsOptOut = PlayerPrefs.GetInt("opt_analyticsOptOut") == 1; found = true; }

        if (found)
            Debug.Log("[SettingsStore] 旧 PlayerPrefs から設定を移行しました");
    }
}
