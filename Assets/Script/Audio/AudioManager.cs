using UnityEngine;

/// <summary>
/// BGM/SE を一元管理するシングルトン。
/// DontDestroyOnLoad でシーンをまたいで存続し、BGM を途切れさせない。
/// 音量・ミュート状態は PlayerPrefs に永続化する（音量 0.0〜1.0）。
///
/// 使い方:
///   AudioManager.I.PlayBgm(clip);      // BGM 再生（同じ clip なら再スタートしない）
///   AudioManager.I.StopBgm();
///   AudioManager.I.PlaySe(clip);       // SE をワンショット再生
///   AudioManager.I.SetBgmVolume(0.5f); // 0.0〜1.0
///   AudioManager.I.SetSeVolume(0.8f);
///   AudioManager.I.SetBgmMuted(true);  // ミュート ON/OFF
///   AudioManager.I.SetSeMuted(true);
///   float v = AudioManager.I.BgmVolume;
///   bool m = AudioManager.I.BgmMuted;
///
/// シーンに1つ「AudioManager」オブジェクトを置き、このスクリプトをアタッチする。
/// 最初に起動したシーン（タイトル）に置いておけば、以降は自動で存続する。
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager I { get; private set; }

    [Header("Audio Sources")]
    [Tooltip("BGM 用の AudioSource（Loop=ON 推奨）")]
    [SerializeField] private AudioSource bgmSource;
    [Tooltip("SE 用の AudioSource（Loop=OFF、PlayOneShot で使用）")]
    [SerializeField] private AudioSource seSource;

    // PlayerPrefs キー
    private const string KeyBgmVolume = "audio_bgm_volume";
    private const string KeySeVolume = "audio_se_volume";
    private const string KeyBgmMuted = "audio_bgm_muted";
    private const string KeySeMuted = "audio_se_muted";

    private float bgmVolume = 1f;
    private float seVolume = 1f;
    private bool bgmMuted = false;
    private bool seMuted = false;

    public float BgmVolume => bgmVolume;
    public float SeVolume => seVolume;
    public bool BgmMuted => bgmMuted;
    public bool SeMuted => seMuted;

    private void Awake()
    {
        // シングルトン確立（重複は破棄）
        if (I != null && I != this)
        {
            Destroy(gameObject);
            return;
        }
        I = this;
        DontDestroyOnLoad(gameObject);

        // AudioSource が未アサインなら自動生成（保険）
        if (bgmSource == null)
        {
            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.loop = true;
            bgmSource.playOnAwake = false;
        }
        if (seSource == null)
        {
            seSource = gameObject.AddComponent<AudioSource>();
            seSource.loop = false;
            seSource.playOnAwake = false;
        }

        // 保存済み音量・ミュート状態を読み込み（初回は音量1.0・ミュートOFF）
        bgmVolume = PlayerPrefs.GetFloat(KeyBgmVolume, 1f);
        seVolume = PlayerPrefs.GetFloat(KeySeVolume, 1f);
        bgmMuted = PlayerPrefs.GetInt(KeyBgmMuted, 0) == 1;
        seMuted = PlayerPrefs.GetInt(KeySeMuted, 0) == 1;
        ApplyVolumes();
    }

    // =========================================================
    // BGM
    // =========================================================

    /// <summary>
    /// BGM を再生する。既に同じ clip を再生中なら何もしない（シーン遷移で途切れさせない）。
    /// </summary>
    public void PlayBgm(AudioClip clip, bool loop = true)
    {
        if (clip == null) return;
        if (bgmSource.clip == clip && bgmSource.isPlaying) return;

        bgmSource.clip = clip;
        bgmSource.loop = loop;
        bgmSource.volume = bgmMuted ? 0f : bgmVolume;
        bgmSource.Play();
    }

    public void StopBgm()
    {
        if (bgmSource != null) bgmSource.Stop();
    }

    public void PauseBgm()
    {
        if (bgmSource != null) bgmSource.Pause();
    }

    public void ResumeBgm()
    {
        if (bgmSource != null) bgmSource.UnPause();
    }

    // =========================================================
    // SE
    // =========================================================

    /// <summary>
    /// SE をワンショット再生する。複数同時に鳴らせる。ミュート中は鳴らさない。
    /// </summary>
    public void PlaySe(AudioClip clip)
    {
        if (clip == null) return;
        if (seMuted) return;
        seSource.PlayOneShot(clip, seVolume);
    }

    // =========================================================
    // 音量・ミュート設定（オプション画面から呼ぶ）
    // =========================================================

    /// <summary>BGM 音量を設定（0.0〜1.0）。即時反映＋永続化。</summary>
    public void SetBgmVolume(float volume)
    {
        bgmVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(KeyBgmVolume, bgmVolume);
        PlayerPrefs.Save();
        ApplyVolumes();
    }

    /// <summary>SE 音量を設定（0.0〜1.0）。永続化。SE は PlaySe 時に都度反映。</summary>
    public void SetSeVolume(float volume)
    {
        seVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(KeySeVolume, seVolume);
        PlayerPrefs.Save();
    }

    /// <summary>BGM ミュートの ON/OFF を設定。即時反映＋永続化。</summary>
    public void SetBgmMuted(bool muted)
    {
        bgmMuted = muted;
        PlayerPrefs.SetInt(KeyBgmMuted, muted ? 1 : 0);
        PlayerPrefs.Save();
        ApplyVolumes();
    }

    /// <summary>SE ミュートの ON/OFF を設定。永続化。次回 PlaySe から反映。</summary>
    public void SetSeMuted(bool muted)
    {
        seMuted = muted;
        PlayerPrefs.SetInt(KeySeMuted, muted ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void ApplyVolumes()
    {
        // ミュート中は音量0、それ以外は設定値。
        if (bgmSource != null) bgmSource.volume = bgmMuted ? 0f : bgmVolume;
        // seSource の volume は PlaySe 時に都度指定するため、ここでは触らない。
    }
}