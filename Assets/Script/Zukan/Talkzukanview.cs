using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// ZukanT シーン（会話図鑑）のコントローラー。
/// TalkEventDatabase の全イベントを登録順にスクロール表示する。
/// 既読イベントはタイトル付きボタンでタップ可能。
/// 未読イベントは「先に進もう！」表示でタップ不可。
///
/// 図鑑から会話を再生する場合:
///   - pendingEventId にイベントIDをセット
///   - talkReturnScene に "ZukanT" をセット（Talk終了後にこのシーンに戻る）
///   - isZukanReplay = true をセット（報酬二重付与防止）
///   - Talk シーンへ遷移
///
/// レイアウト:
///   ScrollView > Viewport > Content (VerticalLayoutGroup)
///     └ [動的生成] TalkZukanCell × N（横長ボタン）
/// </summary>
public class TalkZukanView : MonoBehaviour
{
    // =========================================================
    // Inspector 参照
    // =========================================================

    [Header("Data")]
    [Tooltip("会話イベントデータベース（SOアセットをアサイン）")]
    [SerializeField] private TalkEventDatabase talkDatabase;

    [Header("Grid")]
    [Tooltip("会話セルの Prefab（TalkZukanCell）")]
    [SerializeField] private TalkZukanCell cellPrefab;

    [Tooltip("VerticalLayoutGroup がアタッチされた Content Transform")]
    [SerializeField] private Transform listContent;

    [Header("Buttons")]
    [Tooltip("戻るボタン（Zukan シーンへ）")]
    [SerializeField] private Button backButton;

    [Header("Scene Names")]
    [SerializeField] private string zukanSceneName = "Zukan";
    [SerializeField] private string talkSceneName = "Talk";

    // =========================================================
    // 内部状態
    // =========================================================
    private List<TalkZukanCell> cells = new List<TalkZukanCell>();

    // =========================================================
    // 初期化
    // =========================================================

    private void Start()
    {
        if (backButton != null) backButton.onClick.AddListener(OnBackClicked);

        BuildList();
    }

    // =========================================================
    // リスト構築
    // =========================================================

    /// <summary>
    /// TalkEventDatabase.events を順番にセルとして生成する。
    /// データベースの登録順 = 表示順（ストーリー順序に合わせて登録する前提）。
    /// </summary>
    private void BuildList()
    {
        // 既存セルを破棄
        foreach (var cell in cells)
        {
            if (cell != null) Destroy(cell.gameObject);
        }
        cells.Clear();

        if (talkDatabase == null || talkDatabase.events == null) return;
        if (cellPrefab == null || listContent == null) return;

        foreach (var talkEvent in talkDatabase.events)
        {
            if (talkEvent == null) continue;

            TalkZukanCell cell = Instantiate(cellPrefab, listContent);
            bool played = GameState.I != null && GameState.I.IsPlayed(talkEvent.id);
            cell.Setup(talkEvent, played, OnCellClicked);
            cells.Add(cell);
        }

        Debug.Log($"[TalkZukan] イベント数: {cells.Count}");
    }

    // =========================================================
    // セルタップコールバック
    // =========================================================

    /// <summary>
    /// 既読イベントのセルをタップした時のコールバック。
    /// Talk シーンへ遷移して会話を再生する。
    /// 報酬は二重付与しない（isZukanReplay フラグ）。
    /// Talk 終了後はこのシーン（ZukanT）に戻る。
    /// </summary>
    private void OnCellClicked(TalkEvent talkEvent)
    {
        if (talkEvent == null) return;
        if (GameState.I == null) return;

        GameState.I.pendingEventId = talkEvent.id;
        GameState.I.talkReturnScene = "ZukanT";        // Talk終了後にこのシーンに戻る
        GameState.I.isZukanReplay = true;               // 報酬二重付与防止フラグ

        SceneManager.LoadScene(talkSceneName);
    }

    // =========================================================
    // ボタンハンドラ
    // =========================================================

    private void OnBackClicked()
    {
        SceneManager.LoadScene(zukanSceneName);
    }
}