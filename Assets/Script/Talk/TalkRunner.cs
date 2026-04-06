using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System;


public class TalkRunner : MonoBehaviour
{
    [SerializeField] private TalkEventDatabase database;
    [SerializeField] private string towerSceneName = "Tower";

    [Header("UI")]
    [SerializeField] private TMP_Text speakerText;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private Image portraitImage;

    // =========================================================
    // 背景画像 UI（追加）
    // =========================================================
    //
    // Talk シーンの Canvas 最背面に配置する Image。
    // TalkEvent.backgroundImage または TalkLine.backgroundOverride で
    // 背景を差し替える。
    // 設定がない場合（null）はシーンのデフォルト背景がそのまま表示される。
    // =========================================================

    [Header("UI - Background")]
    [Tooltip("背景画像を表示する Image。Canvas 最背面に配置する。\n"
           + "null 未設定の場合、背景切替機能は無効になる。")]
    [SerializeField] private Image backgroundImage;

    // =========================================================
    // 戻り先シーン（追加）
    // =========================================================
    //
    // オープニングイベント等、Tower 以外から呼ばれた場合の戻り先を
    // GameState.talkReturnScene に保持する。
    // null / 空文字の場合は従来どおり towerSceneName へ戻る。
    // =========================================================

    private TalkEvent current;
    private int index;


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

        //1つ目のセリフを表示
        index = 0;
        Render();
    }

    // クリック時に次の台詞を表示させる。無ければ終了
    public void OnClickNext()
    {
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

        if (speakerText) speakerText.text = line.speaker ?? "";
        if (bodyText) bodyText.text = line.text ?? "";

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
    // 背景画像の適用（追加）
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
        // 報酬アイテム付与（追加）
        // =========================================================
        //
        // TalkEvent.rewardItem が設定されている場合、
        // GameState.pendingItemData にセットし、isRewardItem フラグを立てて
        // Tower シーンへ戻す。
        //
        // Tower シーンの TowerItemTrigger.Start() → CheckPendingExchange() が
        // isRewardItem フラグを見て、通常の入手ポップアップを表示する。
        // （「整理が完了しました」メッセージは表示されない）
        // =========================================================
        if (current.rewardItem != null)
        {
            gs.pendingItemData = current.rewardItem;
            gs.isRewardItem = true;
            Debug.Log($"[TalkRunner] 報酬アイテムを pendingItemData にセット: {current.rewardItem.itemName}");
        }

        ReturnToPreviousScene();
    }

    // =========================================================
    // 戻り先シーンの決定（追加）
    // =========================================================
    //
    // talkReturnScene が設定されていればそちらへ、なければ Tower へ戻る。
    // 使用後は talkReturnScene をクリアする。
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