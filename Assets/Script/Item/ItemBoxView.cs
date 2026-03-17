using System.Collections.Generic;
using UnityEngine;

public class ItemBoxView : MonoBehaviour
{
    [Header("Slots")]
    [SerializeField] private ItemSlotView[] slots;

    [Header("Detail Panel")]
    [SerializeField] private ItemDetailPanel detailPanel;

    private void Start()
    {
        RefreshView();
    }

    [ContextMenu("Refresh View")]
    public void RefreshView()
    {
        if (slots == null || slots.Length == 0) return;

        IReadOnlyList<InventoryItem> items = ItemBoxManager.Instance != null
            ? ItemBoxManager.Instance.GetItems()
            : null;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null) continue;
            slots[i].Setup(this);
            InventoryItem invItem = (items != null && i < items.Count) ? items[i] : null;
            slots[i].SetItem(invItem);
        }
        // RefreshView はスロット再描画のみ。パネルの開閉はここで行わない。
    }

    public void OnClickSlot(ItemSlotView slot, InventoryItem invItem)
    {
        if (detailPanel == null) return;

        if (invItem == null)
        {
            detailPanel.HideImmediate();
            return;
        }

        detailPanel.Show(invItem, this);
    }
}