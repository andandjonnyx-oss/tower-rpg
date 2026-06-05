using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// アイテム図鑑（ZukanI シーン）のメインビュー。
/// 大ジャンル4ボタンで切替、選択中ジャンルの小ジャンルを
/// 「見出し帯 → アイテム5列グリッド」の順に VerticalLayoutGroup へ動的生成する。
///
/// シーン構成（想定）:
///   Canvas
///     ├ GoZukan(消費)/weapon(武器)/magic(魔導書)/hojo(パッシブ) … 大ジャンルボタン
///     ├ Scroll View
///     │   └ Viewport
///     │       └ Content (VerticalLayoutGroup + ContentSizeFitter)
///     │           └ (見出し帯／グリッドブロックを動的生成)
///     └ 戻る Button
///
/// 発見状態は GameState.IsItemDiscovered で判定。未発見は「？」表示。
/// </summary>
public class ItemZukanView : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private ItemZukanCategoryDatabase database;

    [Header("Major Category Buttons")]
    [Tooltip("大ジャンルボタン。database.majorCategories と同じ並び順で4つアサインする。\n"
           + "[0]消費 [1]武器 [2]魔導書 [3]パッシブ")]
    [SerializeField] private Button[] majorButtons;

    [Header("Scroll")]
    [Tooltip("一覧の ScrollRect（詳細から戻った際のスクロール位置復元に使用）")]
    [SerializeField] private ScrollRect scrollRect;

    [Tooltip("VerticalLayoutGroup を持つ Content。見出し帯とグリッドブロックの親。")]
    [SerializeField] private RectTransform content;

    [Header("Prefabs")]
    [Tooltip("見出し帯 Prefab（ItemSectionHeaderCell 付き）")]
    [SerializeField] private ItemSectionHeaderCell headerPrefab;

    [Tooltip("アイテムセル Prefab（ItemIconCell 付き）")]
    [SerializeField] private ItemIconCell itemCellPrefab;

    [Header("Grid Settings")]
    [Tooltip("各小ジャンルグリッドの列数")]
    [SerializeField] private int columns = 5;

    [Tooltip("セルサイズ（MonsterZukan に合わせる場合 250×300）")]
    [SerializeField] private Vector2 cellSize = new Vector2(250f, 300f);

    [Tooltip("セル間隔")]
    [SerializeField] private Vector2 spacing = new Vector2(40f, 40f);

    [Header("Back")]
    [SerializeField] private Button backButton;

    [Tooltip("戻る先シーン名（図鑑トップ）")]
    [SerializeField] private string zukanTopSceneName = "Zukan";

    // 内部状態
    private int currentMajorIndex = 0;
    private readonly List<GameObject> spawned = new List<GameObject>();

    private void Start()
    {
        // 大ジャンルボタンにリスナー登録
        if (majorButtons != null)
        {
            for (int i = 0; i < majorButtons.Length; i++)
            {
                int idx = i; // クロージャ対策
                if (majorButtons[i] != null)
                    majorButtons[i].onClick.AddListener(() => OnMajorClicked(idx));
            }
        }

        if (backButton != null)
            backButton.onClick.AddListener(OnBackClicked);

        // 詳細から戻ったかどうかで初期表示を分岐
        if (ItemZukanContext.ReturningFromDetail)
        {
            currentMajorIndex = ItemZukanContext.ReturnMajorIndex;
            ItemData target = ItemZukanContext.ReturnTargetItem;

            BuildCategory(currentMajorIndex);
            UpdateButtonVisual();

            if (target != null)
                StartCoroutine(ScrollToTargetNextFrame(target));

            // フラグは使い切り
            ItemZukanContext.ReturningFromDetail = false;
            ItemZukanContext.ReturnTargetItem = null;
        }
        else
        {
            // トップから来た場合: 先頭の大ジャンル・先頭表示
            currentMajorIndex = 0;
            BuildCategory(currentMajorIndex);
            UpdateButtonVisual();
        }
    }

    // =========================================================
    // 大ジャンル切替
    // =========================================================

    private void OnMajorClicked(int majorIndex)
    {
        currentMajorIndex = majorIndex;
        BuildCategory(majorIndex);
        UpdateButtonVisual();

        // タブ切替時は先頭へ
        if (scrollRect != null)
            scrollRect.verticalNormalizedPosition = 1f;
    }

    /// <summary>選択中ボタンの見た目を更新（簡易: interactable で表現）。</summary>
    private void UpdateButtonVisual()
    {
        if (majorButtons == null) return;
        for (int i = 0; i < majorButtons.Length; i++)
        {
            if (majorButtons[i] != null)
                majorButtons[i].interactable = (i != currentMajorIndex);
        }
    }

    // =========================================================
    // グリッド構築
    // =========================================================

    /// <summary>
    /// 指定大ジャンルの内容を Content に再構築する。
    /// 小ジャンルごとに「見出し帯 → アイテム5列グリッド」を縦に積む。
    /// </summary>
    private void BuildCategory(int majorIndex)
    {
        // 既存を破棄
        foreach (var go in spawned)
        {
            if (go != null) Destroy(go);
        }
        spawned.Clear();

        if (database == null || content == null) return;
        if (majorIndex < 0 || majorIndex >= database.majorCategories.Count) return;

        var major = database.majorCategories[majorIndex];
        if (major == null || major.subCategories == null) return;

        foreach (var sub in major.subCategories)
        {
            if (sub == null) continue;

            // --- 見出し帯 ---
            if (headerPrefab != null)
            {
                var header = Instantiate(headerPrefab, content);
                header.Setup(sub.headerText);
                spawned.Add(header.gameObject);
            }

            // --- アイテムグリッド（この小ジャンル専用の入れ物を動的生成） ---
            var gridGo = CreateGridBlock();
            spawned.Add(gridGo);

            if (sub.items != null)
            {
                foreach (var item in sub.items)
                {
                    if (item == null) continue;
                    var cell = Instantiate(itemCellPrefab, gridGo.transform);
                    bool discovered = GameState.I != null && GameState.I.IsItemDiscovered(item.itemId);
                    cell.Setup(item, discovered, OnItemClicked);
                }
            }
        }
    }

    /// <summary>
    /// 小ジャンル1つ分のアイテムを並べる、5列 GridLayoutGroup の入れ物を生成する。
    /// 高さはアイテム数に応じて ContentSizeFitter で自動調整。
    /// </summary>
    private GameObject CreateGridBlock()
    {
        var go = new GameObject("SubCategoryGrid",
            typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
        go.transform.SetParent(content, false);

        var grid = go.GetComponent<GridLayoutGroup>();
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = columns;
        grid.cellSize = cellSize;
        grid.spacing = spacing;
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.UpperLeft;

        // グリッド自身の高さを中身に合わせて伸ばす（縦積みで正しく確保するため）
        var fitter = go.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        return go;
    }

    // =========================================================
    // アイテムタップ → 詳細へ
    // =========================================================

    private void OnItemClicked(ItemData item)
    {
        if (item == null) return;

        // ↑↓移動用: 現在の大ジャンル内の発見済みアイテムを1列化
        var flat = database.GetFlatItems(currentMajorIndex);
        var discoveredList = new List<ItemData>();
        foreach (var it in flat)
        {
            if (it != null && GameState.I != null && GameState.I.IsItemDiscovered(it.itemId))
                discoveredList.Add(it);
        }

        int index = discoveredList.IndexOf(item);
        if (index < 0) index = 0;

        ItemZukanContext.SelectedItem = item;
        ItemZukanContext.DiscoveredList = discoveredList;
        ItemZukanContext.CurrentIndex = index;
        ItemZukanContext.CurrentMajorIndex = currentMajorIndex;

        SceneManager.LoadScene("Istatus");
    }

    // =========================================================
    // スクロール復元
    // =========================================================

    /// <summary>
    /// 1フレーム待ってレイアウト確定後、対象アイテムのセルが画面内に収まるよう
    /// スクロール位置を調整する。見出し帯やグリッドの高さを実測して対象セルのY位置を求める。
    /// </summary>
    private IEnumerator ScrollToTargetNextFrame(ItemData target)
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
        yield return null;

        if (scrollRect == null || scrollRect.content == null || scrollRect.viewport == null)
            yield break;

        // 対象セルの RectTransform を探す
        RectTransform targetCell = FindCellOf(target);
        if (targetCell == null)
        {
            scrollRect.verticalNormalizedPosition = 1f;
            yield break;
        }

        float contentHeight = scrollRect.content.rect.height;
        float viewportHeight = scrollRect.viewport.rect.height;
        if (contentHeight <= viewportHeight)
        {
            scrollRect.verticalNormalizedPosition = 1f;
            yield break;
        }

        // 対象セルの中心の、Content 上端からの距離を求める
        // （Content は Pivot Y=1 / 上詰めを想定）
        Vector3[] contentCorners = new Vector3[4];
        Vector3[] cellCorners = new Vector3[4];
        scrollRect.content.GetWorldCorners(contentCorners);
        targetCell.GetWorldCorners(cellCorners);

        float contentTopY = contentCorners[1].y; // 左上
        float cellCenterY = (cellCorners[1].y + cellCorners[0].y) * 0.5f; // 左上と左下の中点

        // ワールドY → Content 上端からの距離（ピクセル換算は Canvas scale を考慮）
        float canvasScale = scrollRect.content.lossyScale.y;
        if (Mathf.Approximately(canvasScale, 0f)) canvasScale = 1f;

        float distanceFromTop = (contentTopY - cellCenterY) / canvasScale;

        // セル中心をビューポート中央に置きたい
        float targetTop = distanceFromTop - viewportHeight * 0.5f;
        float maxScroll = contentHeight - viewportHeight;
        float normalizedFromTop = Mathf.Clamp01(targetTop / maxScroll);

        scrollRect.verticalNormalizedPosition = 1f - normalizedFromTop;
    }

    /// <summary>生成済みセルの中から、指定アイテムのセルの RectTransform を探す。</summary>
    private RectTransform FindCellOf(ItemData target)
    {
        foreach (var go in spawned)
        {
            if (go == null) continue;
            var cells = go.GetComponentsInChildren<ItemIconCell>();
            foreach (var c in cells)
            {
                if (c != null && c.RepresentsItem(target))
                    return c.transform as RectTransform;
            }
        }
        return null;
    }

    // =========================================================
    // 戻る
    // =========================================================

    private void OnBackClicked()
    {
        SceneManager.LoadScene(zukanTopSceneName);
    }
}