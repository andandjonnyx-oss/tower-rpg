using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
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
        WireListNav();

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

        CenterOn(target);
    }

    /// <summary>
    /// 指定セルをビューポート中央に来るようスクロールする（上端・下端ではクランプ）。
    /// コントローラーで選択が移るたびに呼び、カーソルを画面中央付近に固定して
    /// リストの側をスクロールさせる（2026-09-15 要望の「5段目＝中央」挙動）。
    /// </summary>
    private void CenterOn(RectTransform target)
    {
        if (scrollRect == null || scrollRect.content == null || target == null) return;

        RectTransform content = scrollRect.content;
        RectTransform viewport = scrollRect.viewport != null
            ? scrollRect.viewport : scrollRect.GetComponent<RectTransform>();

        float contentHeight = content.rect.height;
        float viewportHeight = viewport.rect.height;
        if (contentHeight <= viewportHeight)
        {
            scrollRect.verticalNormalizedPosition = 1f;
            return;
        }

        float targetCenterFromTop = -target.anchoredPosition.y;
        float desired = targetCenterFromTop - viewportHeight * 0.5f;
        float maxScroll = contentHeight - viewportHeight;
        desired = Mathf.Clamp(desired, 0f, maxScroll);
        scrollRect.verticalNormalizedPosition = Mathf.Clamp01(1f - desired / maxScroll);
    }

    // =========================================================
    // コントローラー/キーボード（2026-09-15）
    // =========================================================

    /// <summary>直近フレームで選択していたオブジェクト（中央スクロールの発火判定）。</summary>
    private GameObject lastSelected;

    private void Update()
    {
        // キャンセルキー（Esc / パッドB）で戻る
        var kb = Keyboard.current;
        var pad = Gamepad.current;
        if ((kb != null && kb.escapeKey.wasPressedThisFrame)
            || (pad != null && pad.buttonEast.wasPressedThisFrame))
        {
            OnBackClicked();
            return;
        }

        // 選択セルが変わったら、そのセルを中央へスクロール（カーソルは中央固定）
        var es = EventSystem.current;
        if (es == null) return;
        var sel = es.currentSelectedGameObject;
        if (sel != lastSelected)
        {
            lastSelected = sel;
            if (sel != null && sel.GetComponent<TalkZukanCell>() != null)
                CenterOn(sel.transform as RectTransform);
        }
    }

    /// <summary>
    /// 既読セル（操作可能なもの）を縦に明示配線する。
    /// 先頭の上／末尾の下は戻るボタンへ。上下移動のたびに Update が中央スクロールする。
    /// </summary>
    private void WireListNav()
    {
        var nav = new List<Selectable>();
        foreach (var c in cells)
        {
            if (c == null || !c.gameObject.activeInHierarchy) continue;
            var sel = c.GetComponent<Selectable>();
            if (sel != null && sel.interactable) nav.Add(sel);
        }

        Selectable back = (backButton != null && backButton.isActiveAndEnabled) ? backButton : null;

        for (int i = 0; i < nav.Count; i++)
        {
            Selectable up = (i > 0) ? nav[i - 1] : back;              // 先頭の上 → 戻る
            Selectable down = (i < nav.Count - 1) ? nav[i + 1] : back; // 末尾の下 → 戻る
            ControllerNav.SetExplicit(nav[i], up, down, null, null);
        }

        if (back != null)
        {
            Selectable first = nav.Count > 0 ? nav[0] : null;
            Selectable last = nav.Count > 0 ? nav[nav.Count - 1] : null;
            ControllerNav.SetExplicit(back, last, first, null, null); // ↑末尾 / ↓先頭
        }

        SelectionHighlighter.PreferredFallback = nav.Count > 0 ? nav[0] : back;
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