using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 会話図鑑の1行セル。
/// TalkZukanView の VerticalLayoutGroup 配下に Prefab から動的生成される。
///
/// 構造:
///   TalkZukanCell (Button)
///     └ titleText (TMP_Text) … イベントのタイトル or 「先に進もう！」
///
/// 既読時: タイトル表示、ボタン有効、タップで会話再生
/// 未読時: 「先に進もう！」表示、ボタン無効
/// </summary>
public class TalkZukanCell : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("イベントタイトル表示用 TMP_Text")]
    [SerializeField] private TMP_Text titleText;

    [Tooltip("セル全体の Button コンポーネント")]
    [SerializeField] private Button cellButton;

    // 内部状態
    private TalkEvent talkEvent;
    private Action<TalkEvent> onClickCallback;

    /// <summary>
    /// セルを初期化する。
    /// </summary>
    /// <param name="ev">会話イベントデータ</param>
    /// <param name="played">既読かどうか</param>
    /// <param name="onClick">タップ時コールバック（既読のみ発火）</param>
    public void Setup(TalkEvent ev, bool played, Action<TalkEvent> onClick)
    {
        talkEvent = ev;
        onClickCallback = onClick;

        if (played)
        {
            // 既読: タイトル表示、ボタン有効
            if (titleText != null)
            {
                // zukanTitle が設定されていればそれを使う、なければ id をフォールバック
                string title = !string.IsNullOrEmpty(ev.zukanTitle) ? ev.zukanTitle : ev.id;
                titleText.text = title;
            }
            if (cellButton != null)
            {
                cellButton.interactable = true;
                cellButton.onClick.RemoveAllListeners();
                cellButton.onClick.AddListener(() => onClickCallback?.Invoke(talkEvent));
            }
        }
        else
        {
            // 未読: 「先に進もう！」表示、ボタン無効
            if (titleText != null) titleText.text = "先に進もう！";
            if (cellButton != null) cellButton.interactable = false;
        }
    }
}