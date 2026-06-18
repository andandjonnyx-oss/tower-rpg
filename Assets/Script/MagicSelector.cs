using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Magic selector UI. Shows the currently selected magic on a button (top-right);
/// tapping it opens a centered popup with an icon grid of owned magic.
/// Shared by the Battle scene and the Tower (field) scene.
///
/// Public API is kept stable so consumers (Battle/Tower) and MagicSelectionMemory
/// need no changes:
///   SetVisible / SetOptions / SetItems / SetValue / Value / onValueChanged /
///   ForceClose / OptionCount / ClearOptions
///
/// "index" always matches the consumer-side parallel list order
/// (magicSkillList / fieldMagicList).
/// </summary>
public class MagicSelector : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Button that shows the selected magic name. Tap to open the popup.")]
    [SerializeField] private Button selectedButton;

    [Tooltip("Centered popup window (MagicPopupPanel). Hidden by default.")]
    [SerializeField] private GameObject popupPanel;

    [Tooltip("Content RectTransform under the ScrollView (GridLayoutGroup + ContentSizeFitter).")]
    [SerializeField] private RectTransform gridContent;

    [Tooltip("ScrollRect of the popup. Reset to top when the popup opens.")]
    [SerializeField] private ScrollRect scrollRect;

    [Tooltip("Magic cell prefab (must have a MagicIconCell component on its root).")]
    [SerializeField] private GameObject magicCellPrefab;

    [Tooltip("Optional explicit close button on the popup. Outside-tap also closes.")]
    [SerializeField] private Button closeButton;

    // Currently selected index.
    private int selectedIndex = 0;

    // Current option label strings (used by the selected-label and Value machinery).
    private List<string> options = new List<string>();

    // Current skill list (used to build icon cells). Parallel to options.
    private List<SkillData> items = new List<SkillData>();

    // Spawned cell instances.
    private List<GameObject> itemInstances = new List<GameObject>();

    // Full-screen transparent blocker used to close the popup on outside tap.
    private GameObject blocker;

    private bool isOpen = false;

    /// <summary>Current selected index.</summary>
    public int Value => selectedIndex;

    /// <summary>Fired when the selection changes. Argument is the new index.</summary>
    public event Action<int> onValueChanged;

    /// <summary>Number of current options.</summary>
    public int OptionCount => options.Count;

    private void Awake()
    {
        if (selectedButton != null)
            selectedButton.onClick.AddListener(ToggleList);

        if (closeButton != null)
            closeButton.onClick.AddListener(CloseList);

        if (popupPanel != null)
            popupPanel.SetActive(false);
    }

    /// <summary>Set the selected index (used by MagicSelectionMemory.Restore).</summary>
    public void SetValue(int index)
    {
        if (options.Count == 0) return;
        selectedIndex = Mathf.Clamp(index, 0, options.Count - 1);
        RefreshSelectedLabel();
        UpdateSelectionHighlight();
    }

    /// <summary>
    /// Legacy string-only API (kept for compatibility). Prefer SetItems.
    /// Clears the skill list, so the popup grid will be empty until SetItems is used.
    /// </summary>
    public void SetOptions(List<string> newOptions)
    {
        options = newOptions ?? new List<string>();
        items = new List<SkillData>();
        selectedIndex = 0;
        RebuildListItems();
        RefreshSelectedLabel();
    }

    /// <summary>
    /// Primary API. Set the magic list. Builds icon cells and the selected label.
    /// "index" matches the order of this list (same as the consumer's parallel list).
    /// </summary>
    public void SetItems(List<SkillData> skills)
    {
        items = skills ?? new List<SkillData>();

        // Build label strings so RefreshSelectedLabel / Value keep working unchanged.
        options = new List<string>();
        for (int i = 0; i < items.Count; i++)
        {
            SkillData s = items[i];
            options.Add(s != null ? $"{s.skillName} (MP:{s.mpCost})" : "");
        }

        selectedIndex = 0;
        RebuildListItems();
        RefreshSelectedLabel();
    }

    /// <summary>Clear all options/items.</summary>
    public void ClearOptions()
    {
        options.Clear();
        items.Clear();
        selectedIndex = 0;
        ClearListItems();
        RefreshSelectedLabel();
    }

    /// <summary>Show/hide the whole selector. Hiding also closes the popup.</summary>
    public void SetVisible(bool visible)
    {
        if (!visible) ForceClose();
        gameObject.SetActive(visible);
    }

    /// <summary>Close the popup from outside (e.g. when another action starts).</summary>
    public void ForceClose()
    {
        CloseList();
    }

    // =========================================================
    // Open / close
    // =========================================================

    private void ToggleList()
    {
        if (isOpen) CloseList();
        else OpenList();
    }

    private void OpenList()
    {
        if (popupPanel == null) return;
        if (options.Count == 0) return;

        popupPanel.SetActive(true);
        isOpen = true;

        // Blocker covers everything; popup is brought above the blocker.
        CreateBlocker();
        popupPanel.transform.SetAsLastSibling();

        // Reset scroll to top.
        if (scrollRect != null)
            scrollRect.verticalNormalizedPosition = 1f;

        UpdateSelectionHighlight();
    }

    private void CloseList()
    {
        DestroyBlocker();
        if (popupPanel != null) popupPanel.SetActive(false);
        isOpen = false;
    }

    // =========================================================
    // Cell management
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

        if (gridContent == null || magicCellPrefab == null) return;

        for (int i = 0; i < items.Count; i++)
        {
            SkillData skill = items[i];

            GameObject cellObj = Instantiate(magicCellPrefab, gridContent);
            cellObj.SetActive(true);

            MagicIconCell cell = cellObj.GetComponent<MagicIconCell>();
            if (cell != null)
            {
                Sprite icon = (skill != null) ? PassiveCalculator.GetMagicIcon(skill) : null;
                cell.Setup(skill, icon, i, OnItemSelected);
            }

            itemInstances.Add(cellObj);
        }

        UpdateSelectionHighlight();
    }

    private void OnItemSelected(int index)
    {
        selectedIndex = index;
        RefreshSelectedLabel();
        UpdateSelectionHighlight();
        CloseList();
        onValueChanged?.Invoke(index);
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

    /// <summary>Highlight the selected cell (no-op if cells have no selectedFrame).</summary>
    private void UpdateSelectionHighlight()
    {
        for (int i = 0; i < itemInstances.Count; i++)
        {
            if (itemInstances[i] == null) continue;
            MagicIconCell cell = itemInstances[i].GetComponent<MagicIconCell>();
            if (cell != null) cell.SetSelected(i == selectedIndex);
        }
    }

    // =========================================================
    // Outside-tap blocker (closes popup without pressing anything behind it)
    // =========================================================

    private void CreateBlocker()
    {
        DestroyBlocker();

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;

        blocker = new GameObject("MagicSelectorBlocker");
        blocker.transform.SetParent(canvas.transform, false);

        RectTransform blockerRect = blocker.AddComponent<RectTransform>();
        blockerRect.anchorMin = Vector2.zero;
        blockerRect.anchorMax = Vector2.one;
        blockerRect.sizeDelta = Vector2.zero;

        Image blockerImage = blocker.AddComponent<Image>();
        blockerImage.color = Color.clear; // transparent but still raycast target (same as original)

        Button blockerButton = blocker.AddComponent<Button>();
        blockerButton.onClick.AddListener(CloseList);

        // Put blocker on top of everything; OpenList then raises the popup above it.
        blocker.transform.SetAsLastSibling();
    }

    private void DestroyBlocker()
    {
        if (blocker != null)
        {
            Destroy(blocker);
            blocker = null;
        }
    }


}