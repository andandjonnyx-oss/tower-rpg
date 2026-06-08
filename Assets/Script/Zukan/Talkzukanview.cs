using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// ZukanT シーン（会話図鑑）のコントローラー。
/// TalkEventDatabase の全イベントをストーリー順にソートしてスクロール表示する。
/// 既読イベントはタイトル付きボタンでタップ可能。
/// 未読イベントは「先に進もう！」表示でタップ不可。
///
/// 【表示順（ソート規約）】
///   floor / step を主キーにソートする。手入力した floor/step が表示順を決める。
///
///     オープニング   : floor = 0          （最小なので先頭）
///     通常イベント   : floor = 該当階, step = 該当ステップ
///     ボス勝利会話   : floor = ボス階, step = 9999  （その階の最後に来る）
///     エンディング   : floor = 9999       （最大なので末尾）
///
///   ソートキーは (floor, step, id)。
///   第3キーに id を入れることで、同じ floor/step のイベントが複数あっても
///   順序が安定する（確率分岐グループなど）。
///   OrderBy 系の LINQ は安定ソートなので、同一キーは元の登録順を保持する。
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
    /// TalkEventDatabase.events を (floor, step, id) でソートしてセルを生成する。
    /// 手入力した floor/step がストーリー順序を決める。
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

        // null を除外しつつ、(floor, step, id) の安定ソート。
        // OrderBy/ThenBy は安定ソートなので、同一 floor/step のイベントは
        // 元の登録順（確率分岐グループなど）を保持する。
        var sorted = talkDatabase.events
            .Where(e => e != null)
            .OrderBy(e => e.floor)
            .ThenBy(e => e.step)
            .ThenBy(e => e.id, System.StringComparer.Ordinal)
            .ToList();

        foreach (var talkEvent in sorted)
        {
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