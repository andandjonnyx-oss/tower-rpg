using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// BGM/SE を一元管理するシングルトン。
/// DontDestroyOnLoad でシーンをまたいで存続する。
/// 音量・ミュート状態は PlayerPrefs に永続化する（音量 0.0〜1.0）。
///
/// 【BGM の2系統 AudioSource】
///   ・bgmSource     : メインBGM（拠点曲・タイトル曲・塔曲）。Pause/UnPause で位置保持。
///   ・overlaySource : オーバーレイBGM（バトル曲）。メインとは独立。
///   2本に分離することで、バトル曲を流している間もメイン曲の再生位置が
///   bgmSource 内に保持され、戻った時に確実に「続きから」再開できる。
///
/// 【API】
///   PlayMain(clip)  : メインBGMを再生。
///       ・同じ曲が再生中 → 継続
///       ・同じ曲が一時停止中 → 続きから再開
///       ・違う曲 → 新規再生（頭から）  ← 階層で曲が変わった時はこれで切替
///   PauseMain()     : メインBGMを一時停止（位置保持）。会話シーンで使用。
///   PlayOverlay(clip): メインを一時停止し、バトル曲を別 source で再生。
///   StopOverlay()        : バトル曲を止めてメインBGMを続きから再開。
///   StopOverlayKeepSilent(): バトル曲を止めるがメインは再開しない（勝利SE用の無音）。
///
/// 【自動再開】
///   会話（PauseMain）後、別シーン（何も置かない図鑑等）へ入った時に
///   止まったままにならないよう、シーンロードを監視し
///   「メインが一時停止中 かつ そのシーンが Pause を要求していない かつ オーバーレイ非アクティブ」
///   なら自動的に再開する。
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager I { get; private set; }

    [Header("Audio Sources")]
    [Tooltip("メインBGM 用の AudioSource（拠点/タイトル/塔）。Loop=ON 推奨。")]
    [SerializeField] private AudioSource bgmSource;
    [Tooltip("オーバーレイBGM 用の AudioSource（バトル曲）。未設定なら自動生成。")]
    [SerializeField] private AudioSource overlaySource;
    [Tooltip("SE 用の AudioSource（Loop=OFF、PlayOneShot で使用）")]
    [SerializeField] private AudioSource seSource;

    [Header("Common SE")]
    [Tooltip("自分の操作でポップアップを開いた瞬間に鳴らす共通SE。\n"
   + "（初期化確認・ポイントリセット・倉庫呼出・帰還・ギブアップ等）")]
    [SerializeField] private AudioClip popupSe;

    [Tooltip("アイテム発見ポップアップ表示時に鳴らす共通SE。\n"
   + "（塔内部での発見・バトル勝利ドロップ・会話リワード）")]
    [SerializeField] private AudioClip itemFoundSe;

    [Tooltip("アイテムが道具袋に入った時に鳴らす共通SE。\n"
   + "（ポップアップで入手選択時・GP交換成立時）")]
    [SerializeField] private AudioClip itemGetSe;

    [Tooltip("アイテムを手放した時に鳴らす共通SE。\n"
   + "（ポップアップで諦める・アイテム/倉庫画面で捨てる）")]
    [SerializeField] private AudioClip itemDiscardSe;

    [Tooltip("回復・ステータスアップ等の消費アイテム使用時のSE（食べる音）")]
    [SerializeField] private AudioClip eatItemSe;

    [Tooltip("攻撃アイテムを戦闘中に使用した時のSE（爆発音)")]
    [SerializeField] private AudioClip attackItemSe;

    [Tooltip("戦闘中の餌付け（与える）時のSE（猫の鳴き声）")]
    [SerializeField] private AudioClip feedSe;

    [Tooltip("武器の装備/外す時に鳴らす共通SE")]
    [SerializeField] private AudioClip equipSe;

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
    // 状態
    // =========================================================

    /// <summary>現在のメインBGMクリップ。Pause しても保持される。</summary>
    private AudioClip mainClip;

    /// <summary>メインBGMが一時停止中かどうか。</summary>
    private bool mainPaused;

    /// <summary>オーバーレイ（バトル曲）再生中かどうか。</summary>
    private bool overlayActive;

    /// <summary>このシーンが Pause を要求したか（自動再開の抑止判定用）。</summary>
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
        if (overlaySource == null)
        {
            overlaySource = gameObject.AddComponent<AudioSource>();
            overlaySource.loop = true;
            overlaySource.playOnAwake = false;
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
        StartCoroutine(AutoResumeNextFrame());
    }

    private System.Collections.IEnumerator AutoResumeNextFrame()
    {
        // SceneBgm.Start()（Pause/Play 要求）が走るのを待つ
        yield return null;

        // オーバーレイ中は触らない。メインが一時停止中で、このシーンが
        // Pause も Play も要求していない（＝何も置いていないシーン）なら再開する。
        if (!overlayActive && mainPaused && !pauseRequestedThisScene)
        {
            ResumeMainInternal();
        }

        pauseRequestedThisScene = false;
    }

    // =========================================================
    // メインBGM
    // =========================================================

    /// <summary>
    /// メインBGMを再生する。SceneBgm（Playモード）から呼ぶ。
    ///   ・同じ曲が再生中 → 継続
    ///   ・同じ曲が一時停止中 → 続きから再開
    ///   ・違う曲 → 新規再生（頭から）
    /// オーバーレイ（バトル曲）中に呼ばれた場合は、メイン source を直接いじらず
    /// 「戻り先メイン曲」として記憶のみ更新する（StopOverlay 時に反映）。
    /// </summary>
    public void PlayMain(AudioClip clip, bool loop = true)
    {
        if (clip == null) return;

        // オーバーレイ中: 戻り先メイン曲を更新するだけ（bgmSource は触らない）
        if (overlayActive)
        {
            if (mainClip != clip)
            {
                // 違う曲に変わった → bgmSource を新しい曲で準備し直す（停止状態で待機）
                mainClip = clip;
                bgmSource.Stop();
                bgmSource.clip = clip;
                bgmSource.loop = loop;
            }
            mainPaused = true; // StopOverlay で再生/再開対象にする
            return;
        }

        // 同じ曲 → 継続 or 再開
        if (mainClip == clip)
        {
            if (mainPaused)
            {
                ResumeMainInternal();
            }
            else if (!bgmSource.isPlaying)
            {
                bgmSource.clip = clip;
                bgmSource.loop = loop;
                bgmSource.volume = CurrentBgmVolume();
                bgmSource.Play();
            }
            return;
        }

        // 違う曲 → 新規再生（頭から）
        mainClip = clip;
        mainPaused = false;
        bgmSource.Stop();
        bgmSource.clip = clip;
        bgmSource.loop = loop;
        bgmSource.volume = CurrentBgmVolume();
        bgmSource.Play();
    }

    /// <summary>
    /// メインBGMを一時停止する（位置保持）。会話シーンの SceneBgm から呼ぶ。
    /// </summary>
    public void PauseMain()
    {
        pauseRequestedThisScene = true; // 自動再開を抑止

        if (overlayActive) return; // バトル中はメインは既に退避済み

        if (bgmSource.isPlaying)
        {
            bgmSource.Pause();
        }
        // 再生中でなくても、メイン曲があるなら一時停止状態として記録
        if (mainClip != null) mainPaused = true;
    }

    /// <summary>
    /// メインBGMを続きから再開する（内部用）。
    /// </summary>
    private void ResumeMainInternal()
    {
        if (mainClip == null) { mainPaused = false; return; }

        // bgmSource.clip がメイン曲と一致していれば UnPause で続きから。
        // 一致していない（オーバーレイ準備等で差し替えた）場合は頭から再生。
        if (bgmSource.clip == mainClip)
        {
            bgmSource.volume = CurrentBgmVolume();
            bgmSource.UnPause();
            if (!bgmSource.isPlaying) bgmSource.Play();
        }
        else
        {
            bgmSource.Stop();
            bgmSource.clip = mainClip;
            bgmSource.loop = true;
            bgmSource.volume = CurrentBgmVolume();
            bgmSource.Play();
        }
        mainPaused = false;
    }

    // =========================================================
    // オーバーレイBGM（バトル曲）
    // =========================================================

    /// <summary>
    /// バトル曲を再生する。メインBGM（塔曲など）を一時停止して退避（位置保持）し、
    /// 別 source で上から流す。同じバトル曲が既に鳴っているなら鳴らし直さない。
    /// </summary>
    public void PlayOverlay(AudioClip clip)
    {
        if (clip == null) return;

        // 既に同じバトル曲が鳴っているなら継続（Itembox 往復で途切れない）
        if (overlayActive && overlaySource.clip == clip && overlaySource.isPlaying) return;

        // メインBGMを一時停止して退避（bgmSource の位置はそのまま保持される）
        if (!overlayActive)
        {
            if (bgmSource.isPlaying)
            {
                bgmSource.Pause();
                mainPaused = true;
            }
            else if (mainClip != null)
            {
                mainPaused = true;
            }
        }

        overlayActive = true;
        overlaySource.clip = clip;
        overlaySource.loop = true;
        overlaySource.volume = CurrentBgmVolume();
        overlaySource.Play();
    }

    /// <summary>
    /// バトル曲を止めてメインBGM（塔曲など）を続きから再開する。
    /// </summary>
    public void StopOverlay()
    {
        overlayActive = false;
        overlaySource.Stop();
        overlaySource.clip = null;

        if (mainClip != null)
        {
            ResumeMainInternal();
        }
    }

    /// <summary>
    /// バトル曲を止めるが、メインBGM（塔曲など）は再開しない（無音のまま）。
    /// 勝利演出で勝利SE・レベルアップSE・ドロップSEを聞かせたい時に使う。
    /// メインBGMは一時停止状態のまま保持され、次に Tower 等へ戻った時に
    /// その SceneBgm(Play) の PlayMain で「続きから」再開する。
    /// </summary>
    public void StopOverlayKeepSilent()
    {
        overlayActive = false;
        overlaySource.Stop();
        overlaySource.clip = null;
        // bgmSource は Pause 済みのまま。位置は保持される。
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

    /// <summary>メイン・オーバーレイ両方の BGM 音量を現在値に反映する。</summary>
    private void ApplyBgmVolume()
    {
        float v = CurrentBgmVolume();
        if (bgmSource != null) bgmSource.volume = v;
        if (overlaySource != null) overlaySource.volume = v;
    }

    /// <summary>共通ポップアップSEを鳴らす。未設定なら何もしない。</summary>
    public void PlayPopupSe()
    {
        if (popupSe != null) PlaySe(popupSe);
    }

    /// <summary>アイテム発見SEを鳴らす。未設定なら何もしない。</summary>
    public void PlayItemFoundSe()
    {
        if (itemFoundSe != null) PlaySe(itemFoundSe);
    }

    /// <summary>アイテム入手SEを鳴らす。未設定なら何もしない。</summary>
    public void PlayItemGetSe()
    {
        if (itemGetSe != null) PlaySe(itemGetSe);
    }

    /// <summary>アイテム破棄SEを鳴らす。未設定なら何もしない。</summary>
    public void PlayItemDiscardSe()
    {
        if (itemDiscardSe != null) PlaySe(itemDiscardSe);
    }

    public void PlayEatItemSe() { if (eatItemSe != null) PlaySe(eatItemSe); }
    public void PlayAttackItemSe() { if (attackItemSe != null) PlaySe(attackItemSe); }
    public void PlayFeedSe() { if (feedSe != null) PlaySe(feedSe); }

    public void PlayEquipSe() { if (equipSe != null) PlaySe(equipSe); }

}