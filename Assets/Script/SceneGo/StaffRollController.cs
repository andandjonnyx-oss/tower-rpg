using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// スタッフロールシーンのコントローラー。
/// スライド（画像＋クレジットテキスト）を一定間隔でフェード切替しながら表示する。
/// BGM はこのシーンに置いた SceneBgm に任せる（このスクリプトでは扱わない）。
///
/// 2つのモードで動く:
///   1) エンディングモード（通常）: 全スライド表示後、エピローグ会話へ遷移。
///   2) 閲覧モード: GameState.staffRollReturnScene が設定されている場合
///      （会話図鑑のスタッフロールボタン経由）。
///      終了後（または戻るボタン）でその戻り先シーンへ遷移する。
///      endingPhase は変更しない。
/// </summary>
public class StaffRollController : MonoBehaviour
{
    [System.Serializable]
    public class StaffRollSlide
    {
        [Tooltip("表示する画像（null可: テキストのみのスライド）")]
        public Sprite image;

        [Tooltip("クレジットテキスト（空可: 画像のみのスライド）")]
        [TextArea(2, 6)]
        public string creditText;

        [Tooltip("このスライドの表示秒数。0以下なら defaultSlideDuration を使う")]
        public float durationOverride = 0f;
    }

    [Header("Slides")]
    [Tooltip("表示順にスライドを登録する")]
    [SerializeField] private StaffRollSlide[] slides;

    [Tooltip("1スライドの表示時間（秒）。フェード時間は含まない")]
    [SerializeField] private float defaultSlideDuration = 4f;

    [Tooltip("フェードイン/アウトの時間（秒）")]
    [SerializeField] private float fadeDuration = 0.5f;

    [Tooltip("最終スライド後、遷移までの待機秒数")]
    [SerializeField] private float endWaitSeconds = 1.5f;

    [Header("UI")]
    [Tooltip("スライド全体（画像+テキスト）を入れた CanvasGroup。フェードに使用")]
    [SerializeField] private CanvasGroup slideGroup;

    [Tooltip("スライド画像を表示する Image")]
    [SerializeField] private Image slideImage;

    [Tooltip("クレジットテキストを表示する TMP_Text")]
    [SerializeField] private TMP_Text creditText;

    [Tooltip("スキップボタン（任意）。\n"
           + "エンディングモード: スタッフロールを省略してエピローグへ。\n"
           + "閲覧モード: 戻り先シーン（図鑑）へ戻る。")]
    [SerializeField] private Button backButton;

    /// <summary>閲覧モード（図鑑等から）かどうか。</summary>
    private bool isReplayMode;
    private string replayReturnScene;

    /// <summary>二重遷移防止。</summary>
    private bool finished;

    private void Start()
    {
        var gs = GameState.I;

        // 閲覧モード判定（図鑑側で staffRollReturnScene をセットして遷移してくる）
        if (gs != null && !string.IsNullOrEmpty(gs.staffRollReturnScene))
        {
            isReplayMode = true;
            replayReturnScene = gs.staffRollReturnScene;
            gs.staffRollReturnScene = null; // 使用後クリア
        }

        // スキップボタン: 両モードで表示する。
        // Finish() がモードに応じて遷移先を振り分ける
        // （エンディングモード→エピローグ / 閲覧モード→戻り先シーン）。
        if (backButton != null)
        {
            backButton.gameObject.SetActive(true);
            backButton.onClick.AddListener(Finish);
        }

        // エンディングモード: フェーズを記録
        // （ここで中断した場合、スタッフロール先頭から再開される）
        if (!isReplayMode && gs != null && gs.endingPhase < EndingManager.PhaseStaffRoll)
        {
            gs.endingPhase = EndingManager.PhaseStaffRoll;
            SaveManager.Save();
        }

        // スライド画像はサイズ・縦横比がバラバラなため、
        // 枠内に縦横比を保ったまま収める（レターボックス表示）
        if (slideImage != null)
            slideImage.preserveAspect = true;

        if (slideGroup != null) slideGroup.alpha = 0f;
        StartCoroutine(PlaySlideshow());
    }

    private IEnumerator PlaySlideshow()
    {
        if (slides != null)
        {
            for (int i = 0; i < slides.Length; i++)
            {
                var s = slides[i];
                if (s == null) continue;

                // スライド内容をセット
                if (slideImage != null)
                {
                    slideImage.sprite = s.image;
                    slideImage.enabled = (s.image != null);
                }
                if (creditText != null)
                    creditText.text = s.creditText ?? "";

                // フェードイン → 表示 → フェードアウト
                yield return Fade(0f, 1f);
                float dur = (s.durationOverride > 0f) ? s.durationOverride : defaultSlideDuration;
                yield return new WaitForSeconds(dur);
                yield return Fade(1f, 0f);
            }
        }

        yield return new WaitForSeconds(endWaitSeconds);
        Finish();
    }

    private IEnumerator Fade(float from, float to)
    {
        if (slideGroup == null || fadeDuration <= 0f) yield break;

        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            slideGroup.alpha = Mathf.Lerp(from, to, t / fadeDuration);
            yield return null;
        }
        slideGroup.alpha = to;
    }

    /// <summary>
    /// スタッフロール終了処理。
    /// 閲覧モード: 戻り先シーンへ。エンディングモード: エピローグ会話へ。
    /// </summary>
    private void Finish()
    {
        if (finished) return;
        finished = true;

        StopAllCoroutines();

        if (isReplayMode)
        {
            SceneManager.LoadScene(replayReturnScene);
            return;
        }

        EndingManager.HandleStaffRollFinished();
    }
}