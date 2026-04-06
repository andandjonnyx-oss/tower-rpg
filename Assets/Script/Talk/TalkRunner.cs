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
            SceneManager.LoadScene(towerSceneName);
            return;
        }

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

        SceneManager.LoadScene(towerSceneName);
    }
}