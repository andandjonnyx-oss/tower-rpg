using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// オプションシーンの音量設定UIコントローラー。
/// BGM/SE のスライダー（0.0〜1.0）とミュートボタンを AudioManager に接続する。
/// 戻るボタンは GameState.optionReturnScene（OpenOptionButton が記憶した値）へ遷移する。
///
/// 構成（Inspector でアサイン）:
///   bgmSlider / seSlider         : Slider（Min=0, Max=1, Whole Numbers=OFF）
///   bgmMuteButton / seMuteButton : ミュート切替ボタン
///   bgmMuteLabel / seMuteLabel   : ボタン上のラベル（任意。ミュート状態を表示）
///   backButton                   : 戻るボタン（記憶した戻り先へ遷移）
///
/// 注意:
///   AudioManager が存在しない場合（Option を単体起動した等）は音量UIは機能しない（無害）。
///   戻り先が未設定の場合は fallbackScene（既定 "Title"）へ戻る。
/// </summary>
public class OptionManager : MonoBehaviour
{
    [Header("BGM")]
    [Tooltip("BGM 音量スライダー（Min=0, Max=1）")]
    [SerializeField] private Slider bgmSlider;
    [Tooltip("BGM ミュート切替ボタン")]
    [SerializeField] private Button bgmMuteButton;
    [Tooltip("BGM ミュートボタンのラベル（任意）")]
    [SerializeField] private TMP_Text bgmMuteLabel;

    [Header("SE")]
    [Tooltip("SE 音量スライダー（Min=0, Max=1）")]
    [SerializeField] private Slider seSlider;
    [Tooltip("SE ミュート切替ボタン")]
    [SerializeField] private Button seMuteButton;
    [Tooltip("SE ミュートボタンのラベル（任意）")]
    [SerializeField] private TMP_Text seMuteLabel;

    [Header("Navigation")]
    [Tooltip("戻るボタン")]
    [SerializeField] private Button backButton;
    [Tooltip("戻り先が未設定だった場合のフォールバック先シーン名")]
    [SerializeField] private string fallbackScene = "Title";

    [Header("Mute Label Text")]
    [Tooltip("ミュートONのときのラベル文言")]
    [SerializeField] private string mutedText = "ミュート: ON";
    [Tooltip("ミュートOFFのときのラベル文言")]
    [SerializeField] private string unmutedText = "ミュート: OFF";

    [Header("Gameplay")]
    [Tooltip("魔法選択保持のON/OFF切替ボタン")]
    [SerializeField] private Button keepMagicButton;
    [Tooltip("魔法選択保持ボタンのラベル（任意）")]
    [SerializeField] private TMP_Text keepMagicLabel;

    private void Start()
    {
        // 戻るボタン（AudioManager の有無に関係なく機能させる）
        if (backButton != null)
            backButton.onClick.AddListener(OnBackClicked);

        // 魔法選択保持トグル（AudioManager 不要なのでここで登録）
        if (keepMagicButton != null)
            keepMagicButton.onClick.AddListener(OnKeepMagicClicked);
        UpdateKeepMagicLabel(GameSettings.KeepMagicSelection);

        var am = AudioManager.I;
        if (am == null)
        {
            Debug.LogWarning("[Option] AudioManager が存在しません。音量設定UIは機能しません。");
            return;
        }

        // 保存済みの値でUIを初期化（リスナー登録前に値を入れて初期化時の発火を避ける）
        if (bgmSlider != null)
        {
            bgmSlider.SetValueWithoutNotify(am.BgmVolume);
            bgmSlider.onValueChanged.AddListener(OnBgmSliderChanged);
        }
        if (seSlider != null)
        {
            seSlider.SetValueWithoutNotify(am.SeVolume);
            seSlider.onValueChanged.AddListener(OnSeSliderChanged);
        }

        if (bgmMuteButton != null)
            bgmMuteButton.onClick.AddListener(OnBgmMuteClicked);
        if (seMuteButton != null)
            seMuteButton.onClick.AddListener(OnSeMuteClicked);

        UpdateBgmMuteLabel(am.BgmMuted);
        UpdateSeMuteLabel(am.SeMuted);
    }

    // =========================================================
    // 戻る
    // =========================================================

    private void OnBackClicked()
    {
        string target = (GameState.I != null && !string.IsNullOrEmpty(GameState.I.optionReturnScene))
            ? GameState.I.optionReturnScene
            : fallbackScene;
        SceneManager.LoadScene(target);
    }

    // =========================================================
    // スライダー
    // =========================================================

    private void OnBgmSliderChanged(float value)
    {
        var am = AudioManager.I;
        if (am == null) return;

        am.SetBgmVolume(value);

        // ミュート中にスライダーを動かしたら「鳴らしたい意図」とみなしてミュート解除
        if (am.BgmMuted && value > 0f)
        {
            am.SetBgmMuted(false);
            UpdateBgmMuteLabel(false);
        }
    }

    private void OnSeSliderChanged(float value)
    {
        var am = AudioManager.I;
        if (am == null) return;

        am.SetSeVolume(value);

        if (am.SeMuted && value > 0f)
        {
            am.SetSeMuted(false);
            UpdateSeMuteLabel(false);
        }
    }

    // =========================================================
    // ミュートボタン
    // =========================================================

    private void OnBgmMuteClicked()
    {
        var am = AudioManager.I;
        if (am == null) return;

        bool next = !am.BgmMuted;
        am.SetBgmMuted(next);
        UpdateBgmMuteLabel(next);
    }

    private void OnSeMuteClicked()
    {
        var am = AudioManager.I;
        if (am == null) return;

        bool next = !am.SeMuted;
        am.SetSeMuted(next);
        UpdateSeMuteLabel(next);
    }

    private void UpdateBgmMuteLabel(bool muted)
    {
        if (bgmMuteLabel != null)
            bgmMuteLabel.text = muted ? mutedText : unmutedText;
    }

    private void UpdateSeMuteLabel(bool muted)
    {
        if (seMuteLabel != null)
            seMuteLabel.text = muted ? mutedText : unmutedText;
    }

    // =========================================================
    // 魔法選択保持トグル（追加）
    // =========================================================

    private void OnKeepMagicClicked()
    {
        bool next = !GameSettings.KeepMagicSelection;
        GameSettings.KeepMagicSelection = next;
        UpdateKeepMagicLabel(next);

        // OFF にした場合は記憶も即クリア（次の表示から先頭に戻る）
        if (!next) MagicSelectionMemory.ClearAll();
    }

    private void UpdateKeepMagicLabel(bool on)
    {
        if (keepMagicLabel != null)
            keepMagicLabel.text = on ? "魔法選択の保持: ON" : "魔法選択の保持: OFF";
    }
}