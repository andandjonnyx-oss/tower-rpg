using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// BGM/SE を一元管理するシングルトン。
/// DontDestroyOnLoad でシーンをまたいで存続する。
/// 音量・ミュート状態は PlayerPrefs に永続化する（音量 0.0〜1.0）。
///
/// 【BGM の2層モデル】
///   ・メインBGM: 拠点曲・タイトル曲・塔曲など「そのフィールドの主BGM」。
///       PlayMain(clip) で再生。同じ曲なら継続、一時停止中なら再開（位置保持）。
///       PauseMain() で一時停止（再生位置を保持）。次のシーンで自動再開される。
///   ・オーバーレイBGM: バトル曲など「メインを一時退避して上に重ねる曲」。
///       PlayOverlay(clip) でメインを一時停止してオーバーレイ再生。
///       StopOverlay() でオーバーレイを止めてメインBGMを再開。
///
/// 【自動再開（重要）】
///   会話シーン等が PauseMain() した後、別シーン（会話図鑑など何も置かないシーン）へ
///   入った時に BGM が止まったままにならないよう、シーンロードを監視して
///   「一時停止中 かつ そのシーンが Pause を要求していない」なら自動的に再開する。
///   SceneBgm が「このシーンは Pause したい」と宣言した場合はそのフレームの自動再開を抑止する。
///
/// 使い方:
///   AudioManager.I.PlayMain(clip);   // 拠点/タイトル/塔 の SceneBgm から
///   AudioManager.I.PauseMain();      // 会話シーンの SceneBgm から
///   AudioManager.I.PlayOverlay(clip);// バトル開始時（敵ごとの曲）
///   AudioManager.I.StopOverlay();    // バトル終了時（メインBGM再開）
///   AudioManager.I.PlaySe(clip);     // SE
///   AudioManager.I.SetBgmVolume(v); / SetSeVolume(v); / SetBgmMuted(b); / SetSeMuted(b);
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

    // =========================================================
    // BGM 状態管理
    // =========================================================

    /// <summary>現在のメインBGM（拠点曲・塔曲など）。Pause しても保持される。</summary>
    private AudioClip mainClip;

    /// <summary>メインBGMが一時停止中かどうか。</summary>
    private bool mainPaused;

    /// <summary>オーバーレイ（バトル曲）再生中かどうか。</summary>
    private bool overlayActive;

    /// <summary>
    /// 今フレーム、SceneBgm が「このシーンは Pause を要求している」と宣言したか。
    /// 宣言があった場合、その回のシーンロード自動再開を抑止する。
    /// </summary>
    private bool pauseRequestedThisScene;

    private void Awake()
    {
        if (I != null && I != this)
        {
            Destroy(gameObject);
            return;
        }
        I = this;
        DontDestroyOnLoad(gameObject);

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

        bgmVolume = PlayerPrefs.GetFloat(KeyBgmVolume, 1f);
        seVolume = PlayerPrefs.GetFloat(KeySeVolume, 1f);
        bgmMuted = PlayerPrefs.GetInt(KeyBgmMuted, 0) == 1;
        seMuted = PlayerPrefs.GetInt(KeySeMuted, 0) == 1;
        ApplyBgmVolume();

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (I == this) SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // =========================================================
    // シーンロード監視（自動再開）
    // =========================================================

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // このシーンの SceneBgm 群は Awake/Start でこのフレームに各種要求を出す。
        // sceneLoaded はそれらより前に来るため、1フレーム遅延させて判定する。
        StartCoroutine(AutoResumeNextFrame());
    }

    private System.Collections.IEnumerator AutoResumeNextFrame()
    {
        // SceneBgm の Start() が走るのを待つ（Pause/Play 要求の確定を待つ）
        yield return null;

        // オーバーレイ中（バトル中）は触らない。
        // メインが一時停止中で、このシーンが Pause を要求していなければ再開する。
        if (!overlayActive && mainPaused && !pauseRequestedThisScene)
        {
            ResumeMainInternal();
        }

        // 次シーンの判定に備えてリセット
        pauseRequestedThisScene = false;
    }

    // =========================================================
    // メインBGM（拠点・タイトル・塔）
    // =========================================================

    /// <summary>
    /// メインBGMを再生する。SceneBgm（Playモード）から呼ぶ。
    ///   ・同じ曲が再生中 → 何もしない（継続）
    ///   ・同じ曲が一時停止中 → 再開（続きから）
    ///   ・違う曲 → 新規再生
    /// オーバーレイ（バトル曲）中に呼ばれた場合は「戻り先のメイン曲」として記憶し、
    /// オーバーレイ終了時にこの曲を再開できるようにする。
    /// </summary>
    public void PlayMain(AudioClip clip, bool loop = true)
    {
        if (clip == null) return;

        // バトル中などオーバーレイ再生中は、戻り先メインだけ更新して再生はしない
        if (overlayActive)
        {
            mainClip = clip;
            mainPaused = true; // オーバーレイ終了時に再開対象とする
            return;
        }

        // 同じ曲なら継続 or 再開
        if (mainClip == clip)
        {
            if (mainPaused)
            {
                ResumeMainInternal();
            }
            else if (!bgmSource.isPlaying)
            {
                // 念のため: 同じ曲指定だが止まっている場合は鳴らす
                bgmSource.clip = clip;
                bgmSource.loop = loop;
                bgmSource.volume = CurrentBgmVolume();
                bgmSource.Play();
            }
            return;
        }

        // 違う曲 → 新規再生
        mainClip = clip;
        mainPaused = false;
        bgmSource.clip = clip;
        bgmSource.loop = loop;
        bgmSource.volume = CurrentBgmVolume();
        bgmSource.Play();
    }

    /// <summary>
    /// メインBGMを一時停止する（再生位置を保持）。会話シーンの SceneBgm から呼ぶ。
    /// 次のシーンで自動再開される（そのシーンが再び Pause を要求しない限り）。
    /// </summary>
    public void PauseMain()
    {
        pauseRequestedThisScene = true; // このシーンは Pause 要求あり → 自動再開を抑止

        if (overlayActive) return; // バトル中はメインは既に退避済み

        if (bgmSource.isPlaying)
        {
            bgmSource.Pause();
            mainPaused = true;
        }
        else if (mainClip != null)
        {
            // 既に止まっている場合も、状態としては一時停止扱いにしておく
            mainPaused = true;
        }
    }

    private void ResumeMainInternal()
    {
        if (mainClip == null) { mainPaused = false; return; }

        if (bgmSource.clip != mainClip)
        {
            bgmSource.clip = mainClip;
        }
        bgmSource.loop = true;
        bgmSource.volume = CurrentBgmVolume();
        bgmSource.UnPause();
        if (!bgmSource.isPlaying) bgmSource.Play();
        mainPaused = false;
    }

    // =========================================================
    // オーバーレイBGM（バトル曲）
    // =========================================================

    /// <summary>
    /// バトル曲を再生する。メインBGM（塔曲など）を一時停止して退避し、上から流す。
    /// 同じバトル曲が既に鳴っている場合は鳴らし直さない（Itembox 往復で途切れない）。
    /// </summary>
    public void PlayOverlay(AudioClip clip)
    {
        if (clip == null) return;

        // 既に同じオーバーレイ曲が鳴っているなら継続
        if (overlayActive && bgmSource.clip == clip && bgmSource.isPlaying) return;

        // メインBGMを退避（位置保持）。次回 PlayMain/StopOverlay で再開対象になる。
        if (!overlayActive)
        {
            if (bgmSource.isPlaying && bgmSource.clip != null)
            {
                // 現在鳴っているのがメイン曲なら退避
                if (mainClip == null) mainClip = bgmSource.clip;
                bgmSource.Pause();
            }
            mainPaused = (mainClip != null);
        }

        overlayActive = true;
        bgmSource.clip = clip;
        bgmSource.loop = true;
        bgmSource.volume = CurrentBgmVolume();
        bgmSource.Play();
    }

    /// <summary>
    /// バトル曲を止めてメインBGM（塔曲など）を続きから再開する。
    /// バトル終了（勝敗確定）時に呼ぶ。メインBGMが無い場合（タイトル/メイン経由の戦闘等）は停止のみ。
    /// </summary>
    public void StopOverlay()
    {
        overlayActive = false;
        bgmSource.Stop();

        // 退避していたメインBGMを再開
        if (mainClip != null)
        {
            ResumeMainInternal();
        }
    }

    /// <summary>
    /// バトル曲を止めるが、メインBGM（塔曲など）は再開しない（無音のまま）。
    /// 勝利演出中に勝利SE・レベルアップSE・ドロップSEを聞かせたい時に使う。
    /// メインBGMは退避状態（mainPaused）のまま保持され、次に Tower 等へ戻った時に
    /// その SceneBgm(Play) が PlayMain を呼んで「続きから」再開する。
    /// </summary>
    public void StopOverlayKeepSilent()
    {
        overlayActive = false;
        bgmSource.Stop();
        // mainClip / mainPaused はそのまま保持（再開しない）。
        // mainClip があるのに mainPaused が false だと自動再開判定に乗らないため、
        // 退避状態を明示しておく。
        if (mainClip != null) mainPaused = true;
    }

    // =========================================================
    // SE
    // =========================================================

    public void PlaySe(AudioClip clip)
    {
        if (clip == null) return;
        if (seMuted) return;
        seSource.PlayOneShot(clip, seVolume);
    }

    // =========================================================
    // 音量・ミュート
    // =========================================================

    public void SetBgmVolume(float volume)
    {
        bgmVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(KeyBgmVolume, bgmVolume);
        PlayerPrefs.Save();
        ApplyBgmVolume();
    }

    public void SetSeVolume(float volume)
    {
        seVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(KeySeVolume, seVolume);
        PlayerPrefs.Save();
    }

    public void SetBgmMuted(bool muted)
    {
        bgmMuted = muted;
        PlayerPrefs.SetInt(KeyBgmMuted, muted ? 1 : 0);
        PlayerPrefs.Save();
        ApplyBgmVolume();
    }

    public void SetSeMuted(bool muted)
    {
        seMuted = muted;
        PlayerPrefs.SetInt(KeySeMuted, muted ? 1 : 0);
        PlayerPrefs.Save();
    }

    private float CurrentBgmVolume()
    {
        return bgmMuted ? 0f : bgmVolume;
    }

    private void ApplyBgmVolume()
    {
        if (bgmSource != null) bgmSource.volume = CurrentBgmVolume();
    }
}