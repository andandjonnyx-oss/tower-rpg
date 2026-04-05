using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// TMP_Dropdown の代替となる自作ドロップダウン UI コンポーネント。
/// バトルシーン・塔シーンの魔法選択で共通して使用する。
///
/// 階層構造:
///   MagicSelector (このスクリプト)
///     └─ selectedButton  … 選択中の項目を表示。押すとリストが開閉する。
///          ├─ Text (TMP)
///          └─ listPanel   … ScrollView。selectedButton の子として配置する。
///               └─ Content (listContent)
///                    └─ (itemPrefab が動的生成される)
///
/// ■ Inspector 設定（推奨）
///
///   selectedButton:
///     任意の Anchor / Pivot で配置してよい。
///
///   listPanel (ScrollView):
///     Anchor: stretch-top  → AnchorMin(0, 1) / AnchorMax(1, 1)
///     Pivot: (0.5, 1)
///     Left: 0 / Right: 0 / Height: 0（コードが上書き）
///
///   content (listPanel の子):
///     VerticalLayoutGroup をアタッチ。
///       Child Alignment: Upper Left
///       Child Control Width: true  ← 重要
///       Child Force Expand Width: true
///       Child Control Height: false
///       Child Force Expand Height: false
///     ContentSizeFitter は不要（コードで高さ設定する）。
///
///   itemPrefab:
///     Anchor / Pivot は何でもよい（VerticalLayoutGroup が制御する）。
///     Width / Height は 500 / 40 など適当でよい。
///     ★ Prefab を変更する必要はない。
///
/// 見切れ対策:
///   リストパネルは selectedButton の真下に展開するが、
///   画面下端を超える場合はボタンの真上に展開する。
/// </summary>
public class MagicSelector : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("選択中の項目名を表示するボタン。押すとリストが開閉する。")]
    [SerializeField] private Button selectedButton;

    [Tooltip("選択肢一覧の ScrollView ルート。selectedButton の子として配置する。\n"
           + "Anchor: stretch-top / Pivot: (0.5, 1) を推奨。\n"
           + "初期状態は非表示にする。")]
    [SerializeField] private GameObject listPanel;

    [Tooltip("ScrollView の Content RectTransform。\n"
           + "VerticalLayoutGroup（Child Control Width = true）をアタッチすること。")]
    [SerializeField] private RectTransform listContent;

    [Tooltip("選択肢1行分のボタン Prefab。子に TMP_Text を持つこと。\n"
           + "Anchor / Pivot の設定は自由（LayoutGroup が制御する）。")]
    [SerializeField] private GameObject itemPrefab;

    [Header("Settings")]
    [Tooltip("1行あたりの高さ（px）")]
    [SerializeField] private float itemHeight = 40f;

    [Tooltip("リストに表示する最大行数。これを超えるとスクロールになる。")]
    [SerializeField] private int maxVisibleItems = 20;

    /// <summary>現在選択中のインデックス。</summary>
    private int selectedIndex = 0;

    /// <summary>現在のオプション文字列リスト。</summary>
    private List<string> options = new List<string>();

    /// <summary>動的生成されたボタンのキャッシュ。</summary>
    private List<GameObject> itemInstances = new List<GameObject>();

    /// <summary>リストが開いているかどうか。</summary>
    private bool isOpen = false;

    /// <summary>選択中のインデックスを取得する。</summary>
    public int Value => selectedIndex;

    /// <summary>選択中のインデックスを設定する。</summary>
    public void SetValue(int index)
    {
        if (options.Count == 0) return;
        selectedIndex = Mathf.Clamp(index, 0, options.Count - 1);
        RefreshSelectedLabel();
    }

    private void Awake()
    {
        if (selectedButton != null)
            selectedButton.onClick.AddListener(ToggleList);

        if (listPanel != null)
            listPanel.SetActive(false);
    }

    /// <summary>
    /// オプションを設定する。既存のリストはクリアされる。
    /// 設定後は selectedIndex = 0 にリセットされる。
    /// </summary>
    public void SetOptions(List<string> newOptions)
    {
        options = newOptions ?? new List<string>();
        selectedIndex = 0;
        RebuildListItems();
        RefreshSelectedLabel();
    }

    /// <summary>
    /// オプションを全クリアする。
    /// </summary>
    public void ClearOptions()
    {
        options.Clear();
        selectedIndex = 0;
        ClearListItems();
        RefreshSelectedLabel();
    }

    /// <summary>
    /// オプション数を返す。
    /// </summary>
    public int OptionCount => options.Count;

    /// <summary>
    /// ゲームオブジェクトの表示/非表示を一括制御する。
    /// </summary>
    public void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
    }

    // =========================================================
    // リスト開閉
    // =========================================================

    private void ToggleList()
    {
        if (isOpen) CloseList();
        else OpenList();
    }

    private void OpenList()
    {
        if (listPanel == null) return;
        if (options.Count == 0) return;

        RectTransform panelRect = listPanel.GetComponent<RectTransform>();
        if (panelRect == null) return;

        // リストパネルの高さを計算
        int visibleCount = Mathf.Min(options.Count, maxVisibleItems);
        float listHeight = visibleCount * itemHeight;

        // Content の高さ設定（全アイテム分）
        if (listContent != null)
        {
            Vector2 contentSize = listContent.sizeDelta;
            contentSize.y = options.Count * itemHeight;
            listContent.sizeDelta = contentSize;
        }

        // パネルの高さ設定（表示分のみ）
        Vector2 panelSize = panelRect.sizeDelta;
        panelSize.y = listHeight;
        panelRect.sizeDelta = panelSize;

        // selectedButton 基準でリストパネルの Y 位置を計算
        PositionListPanel(panelRect, listHeight);

        listPanel.SetActive(true);
        isOpen = true;
    }

    /// <summary>
    /// リストパネルの Y 位置を設定する。
    ///
    /// listPanel は selectedButton の子で、Anchor = stretch-top を想定。
    /// X は 0 固定（stretch で自動フィット）。Y のみ計算する。
    /// </summary>
    private void PositionListPanel(RectTransform panelRect, float listHeight)
    {
        if (selectedButton == null) return;
        RectTransform buttonRect = selectedButton.GetComponent<RectTransform>();
        if (buttonRect == null) return;

        float bh = buttonRect.rect.height;
        float bpy = buttonRect.pivot.y;

        // ボタンのローカル座標での端
        float buttonBottomY = -bh * bpy;
        float buttonTopY = bh * (1f - bpy);

        // パネルの Pivot Y
        float ppy = panelRect.pivot.y;

        // listPanel の Anchor 中央 Y（親=selectedButton 基準）
        float anchorCenterY = (panelRect.anchorMin.y + panelRect.anchorMax.y) * 0.5f;
        float anchorLocalY = buttonBottomY + bh * anchorCenterY;

        // =========================================================
        // 見切れ判定
        // =========================================================
        bool expandDown = true;
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            Vector3[] corners = new Vector3[4];
            buttonRect.GetWorldCorners(corners);
            Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null : canvas.worldCamera;
            Vector2 screenBottom = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);

            float canvasScale = 1f;
            if (canvas.transform.localScale.x != 0f)
                canvasScale = canvas.transform.localScale.x;

            expandDown = (screenBottom.y - listHeight * canvasScale) >= 0f;
        }

        float posY;

        if (expandDown)
        {
            // 真下展開: パネル上端 = ボタン下端
            posY = buttonBottomY - listHeight * (1f - ppy) - anchorLocalY;
        }
        else
        {
            // 真上展開: パネル下端 = ボタン上端
            posY = buttonTopY + listHeight * ppy - anchorLocalY;
        }

        // X=0 固定、sizeDelta.x=0（stretch なので Left+Right=0 の意味）
        Vector2 sd = panelRect.sizeDelta;
        sd.x = 0f;
        panelRect.sizeDelta = sd;

        panelRect.anchoredPosition = new Vector2(0f, posY);
    }

    private void CloseList()
    {
        if (listPanel != null) listPanel.SetActive(false);
        isOpen = false;
    }

    // =========================================================
    // リストアイテム管理
    // =========================================================

    private void ClearListItems()
    {
        for (int i = 0; i < itemInstances.Count; i++)
        {
            if (itemInstances[i] != null)
                Destroy(itemInstances[i]);
        }
        itemInstances.Clear();
    }

    private void RebuildListItems()
    {
        ClearListItems();

        if (listContent == null || itemPrefab == null) return;

        for (int i = 0; i < options.Count; i++)
        {
            GameObject item = Instantiate(itemPrefab, listContent);
            item.SetActive(true);

            // =========================================================
            // 生成した項目の高さのみ設定する。
            // 幅は VerticalLayoutGroup の Child Control Width = true で
            // Content 幅に自動フィットするため、コードでの設定は不要。
            // ★ itemPrefab の Anchor / Pivot は変更しない。
            // =========================================================
            RectTransform itemRect = item.GetComponent<RectTransform>();
            if (itemRect != null)
            {
                Vector2 sd = itemRect.sizeDelta;
                sd.y = itemHeight;
                itemRect.sizeDelta = sd;
            }

            // テキスト設定
            TMP_Text label = item.GetComponentInChildren<TMP_Text>();
            if (label != null)
                label.text = options[i];

            // ボタンクリック時の選択処理
            int capturedIndex = i;
            Button btn = item.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.AddListener(() => OnItemSelected(capturedIndex));
            }

            itemInstances.Add(item);
        }

        // Content の高さを設定
        if (listContent != null)
        {
            Vector2 size = listContent.sizeDelta;
            size.y = options.Count * itemHeight;
            listContent.sizeDelta = size;
        }
    }

    private void OnItemSelected(int index)
    {
        selectedIndex = index;
        RefreshSelectedLabel();
        CloseList();
    }

    private void RefreshSelectedLabel()
    {
        if (selectedButton == null) return;

        TMP_Text label = selectedButton.GetComponentInChildren<TMP_Text>();
        if (label == null) return;

        if (options.Count > 0 && selectedIndex >= 0 && selectedIndex < options.Count)
            label.text = options[selectedIndex];
        else
            label.text = "---";
    }

    /// <summary>
    /// 外部クリック等でリストを閉じたい場合に呼ぶ。
    /// </summary>
    public void ForceClose()
    {
        CloseList();
    }
}