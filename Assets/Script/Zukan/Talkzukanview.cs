using System.Collections;
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
/// 【スクロール位置の復元】
///   会話を見た後に図鑑へ戻った際、直前に開いた会話セルの位置へ復元する。
///   - セルタップ時: ZukanContext.TalkReturningFromDetail = true,
///                   TalkReturnTargetId = そのイベント id をセットして Talk へ。
///   - Talk から戻った Start(): フラグが立っていればそのセルへスクロール復元。
///   - 図鑑トップ(Zukan)から入った場合: フラグが false なので先頭表示。
///   モンスター図鑑(Mstatus/ZukanM)の ReturningFromDetail と同じパターン。
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

    [Header("Scroll")]
    [Tooltip("スクロール位置復元に使う ScrollRect")]
    [SerializeField] private ScrollRect scrollRect;

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

    // 復元用: イベント id → そのセルの RectTransform を引けるようにしておく
    private Dictionary<string, RectTransform> cellRectById = new Dictionary<string, RectTransform>();

    // =========================================================
    // 初期化
    // =========================================================

    private void Start()
    {
        if (backButton != null) backButton.onClick.AddListener(OnBackClicked);

        BuildList();

        // Talk から戻ってきた場合のみスクロール位置を復元する。
        // 図鑑トップ(Zukan)から入った場合はフラグが false なので先頭のまま。
        if (ZukanContext.TalkReturningFromDetail)
        {
            string targetId = ZukanContext.TalkReturnTargetId;

            // フラグは一度使ったらクリア（次回トップから入った時に先頭表示させる）
            ZukanContext.TalkReturningFromDetail = false;
            ZukanContext.TalkReturnTargetId = null;

            if (!string.IsNullOrEmpty(targetId))
            {
                StartCoroutine(ScrollToTargetNextFrame(targetId));
            }
        }
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
        cellRectById.Clear();

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
            bool played = GameState.I != null
                       && (GameState.I.zukanAllUnlocked || GameState.I.IsPlayed(talkEvent.id));
            cell.Setup(talkEvent, played, OnCellClicked);
            cells.Add(cell);

            // 復元用に id → RectTransform を登録（id 重複時は先勝ち）
            if (!string.IsNullOrEmpty(talkEvent.id) && !cellRectById.ContainsKey(talkEvent.id))
            {
                cellRectById[talkEvent.id] = cell.transform as RectTransform;
            }
        }

        Debug.Log($"[TalkZukan] イベント数: {cells.Count}");
    }

    // =========================================================
    // スクロール位置の復元
    // =========================================================

    /// <summary>
    /// 指定 id のセルが画面内に収まるよう、1フレーム待ってからスクロールする。
    /// 生成直後は VerticalLayoutGroup / ContentSizeFitter のレイアウトが未確定なので
    /// 1フレーム待ってから位置を計算する（モンスター図鑑と同じ手法）。
    /// </summary>
    private IEnumerator ScrollToTargetNextFrame(string targetId)
    {
        // レイアウト確定を待つ
        yield return null;

        if (scrollRect == null || scrollRect.content == null) yield break;
        if (!cellRectById.TryGetValue(targetId, out RectTransform target) || target == null) yield break;

        // レイアウトを即時確定させてからサイズを読む
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.content);

        RectTransform content = scrollRect.content;
        RectTransform viewport = scrollRect.viewport != null ? scrollRect.viewport : scrollRect.GetComponent<RectTransform>();

        float contentHeight = content.rect.height;
        float viewportHeight = viewport.rect.height;

        // スクロール不要（全部見えている）なら何もしない
        if (contentHeight <= viewportHeight)
        {
            scrollRect.verticalNormalizedPosition = 1f;
            yield break;
        }

        // ターゲットセルの content 内ローカルY位置（上端基準の距離）を求める。
        // VerticalLayoutGroup は上から下へ並ぶので、anchoredPosition.y は負方向に増える。
        float targetCenterFromTop = -target.anchoredPosition.y; // content上端からセル中心までの距離

        // セル中心をビューポート中央に置きたい場合のスクロール量
        float desired = targetCenterFromTop - viewportHeight * 0.5f;

        // クランプ
        float maxScroll = contentHeight - viewportHeight;
        desired = Mathf.Clamp(desired, 0f, maxScroll);

        // verticalNormalizedPosition: 1=上端, 0=下端
        float normalized = 1f - (desired / maxScroll);
        scrollRect.verticalNormalizedPosition = Mathf.Clamp01(normalized);
    }

    // =========================================================
    // セルタップコールバック
    // =========================================================

    /// <summary>
    /// 既読イベントのセルをタップした時のコールバック。
    /// Talk シーンへ遷移して会話を再生する。
    /// 報酬は二重付与しない（isZukanReplay フラグ）。
    /// Talk 終了後はこのシーン（ZukanT）に戻り、このセルの位置へスクロール復元する。
    /// </summary>
    private void OnCellClicked(TalkEvent talkEvent)
    {
        if (talkEvent == null) return;
        if (GameState.I == null) return;

        // スクロール復元用: このイベントを「戻り先ターゲット」として記録
        ZukanContext.TalkReturningFromDetail = true;
        ZukanContext.TalkReturnTargetId = talkEvent.id;

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
        // トップ(Zukan)へ戻る時は復元フラグをクリアしておく
        // （次に図鑑トップから入り直した時、先頭表示にするため）
        ZukanContext.TalkReturningFromDetail = false;
        ZukanContext.TalkReturnTargetId = null;

        SceneManager.LoadScene(zukanSceneName);
    }
}