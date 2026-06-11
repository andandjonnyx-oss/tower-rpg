using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections;


public class TalkRunner : MonoBehaviour
{
    [SerializeField] private TalkEventDatabase database;
    [SerializeField] private string towerSceneName = "Tower";

    [Header("UI")]
    [SerializeField] private TMP_Text speakerText;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private Image portraitImage;

    // =========================================================
    // 背景画像 UI
    // =========================================================

    [Header("UI - Background")]
    [Tooltip("背景画像を表示する Image。Canvas 最背面に配置する。\n"
           + "null 未設定の場合、背景切替機能は無効になる。")]
    [SerializeField] private Image backgroundImage;

    // =========================================================
    // 名前入力ポップアップ UI（追加）
    // =========================================================
    //
    // TalkLine.requestNameInput == true の台詞を表示する直前に
    // ポップアップを出し、確定までタップ進行をブロックする。
    // 入力結果は GameState.playerName に保存され、{name} 置換に使われる。
    // =========================================================

    [Header("UI - Name Input")]
    [Tooltip("名前入力ポップアップのルートオブジェクト。初期状態は非表示にしておく。\n"
           + "null の場合、requestNameInput は無視される（{name} は既定名で置換）。")]
    [SerializeField] private GameObject nameInputPanel;

    [Tooltip("名前入力フィールド（TMP_InputField）。Character Limit の設定推奨（例: 8）。")]
    [SerializeField] private TMP_InputField nameInputField;

    [Tooltip("名前確定ボタン")]
    [SerializeField] private Button nameInputConfirmButton;

    [Tooltip("未入力で確定した場合・{name} 置換時に名前が無い場合の既定名")]
    [SerializeField] private string defaultPlayerName = "あなた";

    /// <summary>名前入力待機中はタップ進行を無効化する。</summary>
    private bool isWaitingNameInput;

    // =========================================================
    // タイトル表示 UI（追加）
    // =========================================================
    //
    // 会話開始前に2秒間、背景とタイトルを表示する。
    // titleText: 画面中央に配置する TMP_Text（Inspector でアサイン）
    // titlePanel: タイトル表示中に有効化するパネル（背景の上に重ねる半透明パネル等。任意）
    // talkPanel: 会話UI全体の親オブジェクト（タイトル表示中は非表示にする）
    // titleDisplayDuration: タイトル表示時間（秒）
    // =========================================================

    [Header("UI - Title Display")]
    [Tooltip("会話開始前に表示するタイトル用テキスト。画面中央に配置する。\n"
           + "null の場合、タイトル表示をスキップして即座に会話を開始する。")]
    [SerializeField] private TMP_Text titleText;

    [Tooltip("タイトル表示中に有効化するパネル（半透明オーバーレイ等）。任意。")]
    [SerializeField] private GameObject titlePanel;

    [Tooltip("会話UIの親オブジェクト。タイトル表示中は非表示にする。")]
    [SerializeField] private GameObject talkPanel;

    [Tooltip("タイトルの表示時間（秒）")]
    [SerializeField] private float titleDisplayDuration = 2f;

    private TalkEvent current;
    private int index;

    /// <summary>タイトル表示待機中はタップを無効化する。</summary>
    private bool isWaitingTitle;

    //シーンに入ると実行
    private void Start()
    {
        //ゲームデータ及びデータベースが正常か確認
        var gs = GameState.I;
        if (gs == null || database == null)
        {
            Debug.LogError("GameState or Database missing.");
            SceneManager.LoadScene(towerSceneName);
            return;
        }

        // 名前入力ポップアップ初期化
        if (nameInputPanel != null)
            nameInputPanel.SetActive(false);
        if (nameInputConfirmButton != null)
            nameInputConfirmButton.onClick.AddListener(OnNameInputConfirm);

        //待機中IDを基にイベントを取得
        current = database.FindById(gs.pendingEventId);
        if (current == null || current.lines == null || current.lines.Count == 0)
        {
            Debug.LogWarning("No pending talk event / empty event. Back to Tower.");
            gs.pendingEventId = null;
            ReturnToPreviousScene();
            return;
        }

        // イベントのデフォルト背景を適用
        ApplyBackground(current.backgroundImage);

        // タイトル表示が可能ならコルーチンで待機、不可能なら即座に会話開始
        if (titleText != null && !string.IsNullOrEmpty(GetDisplayTitle()))
        {
            StartCoroutine(ShowTitleThenStart());
        }
        else
        {
            // タイトル表示なし: 従来どおり即座に開始
            SetTitleUIVisible(false);
            SetTalkUIVisible(true);
            index = 0;
            Render();
        }
    }

    // =========================================================
    // タイトル表示コルーチン（追加）
    // =========================================================

    /// <summary>
    /// 背景 + タイトルを表示し、titleDisplayDuration 秒待機後に会話を開始する。
    /// 待機中はタップ（OnClickNext）を無効化する。
    /// </summary>
    private IEnumerator ShowTitleThenStart()
    {
        isWaitingTitle = true;

        // 会話UIを非表示、タイトルUIを表示
        SetTalkUIVisible(false);
        SetTitleUIVisible(true);

        // タイトルテキスト設定
        titleText.text = GetDisplayTitle();

        // 待機
        yield return new WaitForSeconds(titleDisplayDuration);

        // タイトルUIを非表示、会話UIを表示
        SetTitleUIVisible(false);
        SetTalkUIVisible(true);

        // 会話開始
        index = 0;
        Render();

        isWaitingTitle = false;
    }

    /// <summary>
    /// 表示用タイトルを取得する。
    /// zukanTitle が設定されていればそちらを使い、なければ id をフォールバック。
    /// </summary>
    private string GetDisplayTitle()
    {
        if (current == null) return "";
        if (!string.IsNullOrEmpty(current.zukanTitle))
            return current.zukanTitle;
        return current.id ?? "";
    }

    /// <summary>タイトルUIの表示/非表示を切り替える。</summary>
    private void SetTitleUIVisible(bool visible)
    {
        if (titleText != null)
            titleText.gameObject.SetActive(visible);
        if (titlePanel != null)
            titlePanel.SetActive(visible);
    }

    /// <summary>会話UIの表示/非表示を切り替える。</summary>
    private void SetTalkUIVisible(bool visible)
    {
        if (talkPanel != null)
            talkPanel.SetActive(visible);
    }

    // クリック時に次の台詞を表示させる。無ければ終了
    public void OnClickNext()
    {
        // タイトル表示中・名前入力中はタップを無視
        if (isWaitingTitle || isWaitingNameInput) return;

        if (current == null) return;

        index++;
        if (index >= current.lines.Count)
        {
            Finish();
            return;
        }
        Render();
    }



    //画面表示
    //対応したキャラ名と台詞、立ち絵を表示させる
    private void Render()
    {
        var line = current.lines[index];

        // =========================================================
        // 名前入力リクエスト（追加）
        // この台詞を表示する前にポップアップを出し、確定を待つ。
        // 図鑑リプレイ時はスキップ（保存済みの名前をそのまま使う）。
        // =========================================================
        if (line.requestNameInput && nameInputPanel != null && !GameState.I.isZukanReplay)
        {
            ShowNameInputPopup();
            return; // 確定後に OnNameInputConfirm() → RenderLine() が呼ばれる
        }

        RenderLine(line);
    }

    /// <summary>
    /// 台詞1行分の実描画。{name} 置換を適用する。
    /// </summary>
    private void RenderLine(TalkEvent.TalkLine line)
    {
        if (speakerText) speakerText.text = ReplacePlaceholders(line.speaker ?? "");
        if (bodyText) bodyText.text = ReplacePlaceholders(line.text ?? "");

        if (portraitImage)
        {
            portraitImage.sprite = line.portrait;
            portraitImage.enabled = (line.portrait != null);
        }

        // 背景の切替: 台詞にオーバーライドがあればそちら、なければイベントのデフォルト
        Sprite bg = line.backgroundOverride != null
                  ? line.backgroundOverride
                  : current.backgroundImage;
        ApplyBackground(bg);
    }

    // =========================================================
    // 名前入力ポップアップ（追加）
    // =========================================================

    /// <summary>
    /// 名前入力ポップアップを表示し、タップ進行をブロックする。
    /// 入力欄には保存済みの名前をプリフィルする（中断再開時の再入力を楽にする）。
    /// </summary>
    private void ShowNameInputPopup()
    {
        isWaitingNameInput = true;

        if (nameInputField != null)
            nameInputField.text = GameState.I.playerName ?? "";

        if (AudioManager.I != null) AudioManager.I.PlayPopupSe();
        nameInputPanel.SetActive(true);
    }

    /// <summary>
    /// 名前確定ボタン。入力を GameState.playerName に保存して台詞表示を再開する。
    /// 空入力（スペースのみ含む）の場合は既定名を使う。
    /// </summary>
    private void OnNameInputConfirm()
    {
        string input = nameInputField != null ? nameInputField.text.Trim() : "";
        if (string.IsNullOrEmpty(input)) input = defaultPlayerName;

        GameState.I.playerName = input;
        SaveManager.Save(); // 即時セーブ

        nameInputPanel.SetActive(false);
        isWaitingNameInput = false;

        Debug.Log($"[TalkRunner] プレイヤー名を設定: {input}");

        // 入力リクエスト元の台詞を表示（この台詞自身も {name} 置換される）
        RenderLine(current.lines[index]);
    }

    /// <summary>
    /// テキスト内のプレースホルダーを置換する。
    /// {name} → プレイヤー名（未設定なら defaultPlayerName）
    /// </summary>
    private string ReplacePlaceholders(string text)
    {
        if (string.IsNullOrEmpty(text)) return text ?? "";
        if (!text.Contains("{name}")) return text;
        return text.Replace("{name}", GameState.I.GetDisplayName(defaultPlayerName));
    }

    // =========================================================
    // 背景画像の適用
    // =========================================================

    /// <summary>
    /// 背景Image に Sprite を適用する。
    /// sprite が null の場合は背景 Image を非表示にする（シーンのデフォルト背景が見える）。
    /// </summary>
    private void ApplyBackground(Sprite sprite)
    {
        if (backgroundImage == null) return;

        if (sprite != null)
        {
            backgroundImage.sprite = sprite;
            backgroundImage.enabled = true;
        }
        else
        {
            backgroundImage.enabled = false;
        }
    }

    //イベント終了時、再生済イベントとして登録し、待機イベントを空に
    private void Finish()
    {
        var gs = GameState.I;
        gs.MarkPlayed(current.id);
        gs.pendingEventId = null;

        // =========================================================
        // 報酬アイテム付与
        // =========================================================
        // 図鑑リプレイ時は報酬を付与しない（二重付与防止）
        if (current.rewardItem != null && !gs.isZukanReplay)
        {
            gs.pendingItemData = current.rewardItem;
            gs.isRewardItem = true;
            Debug.Log($"[TalkRunner] 報酬アイテムを pendingItemData にセット: {current.rewardItem.itemName}");
        }

        // 図鑑リプレイフラグをクリア（判定用に値を控えてからクリア）
        bool wasZukanReplay = gs.isZukanReplay;
        gs.isZukanReplay = false;

        // =========================================================
        // エンディング進行フック（追加）
        // ED会話/エピローグの終了時は EndingManager が次のシーンへ遷移する。
        // 図鑑リプレイでの再生時は通常の戻り処理を使う（エンディングを誤発火させない）。
        // =========================================================
        if (!wasZukanReplay && EndingManager.HandleTalkFinished(current.id))
            return;

        ReturnToPreviousScene();
    }

    // =========================================================
    // 戻り先シーンの決定
    // =========================================================

    /// <summary>
    /// Talk 終了後の戻り先シーンをロードする。
    /// GameState.talkReturnScene が設定されていればそちらへ遷移し、
    /// 設定されていなければ towerSceneName（デフォルト: "Tower"）へ遷移する。
    /// </summary>
    private void ReturnToPreviousScene()
    {
        var gs = GameState.I;
        string returnScene = towerSceneName;

        if (gs != null && !string.IsNullOrEmpty(gs.talkReturnScene))
        {
            returnScene = gs.talkReturnScene;
            gs.talkReturnScene = null; // 使用後クリア
        }

        SceneManager.LoadScene(returnScene);
    }
}