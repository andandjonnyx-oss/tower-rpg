using System.Collections.Generic;
using UnityEngine;

public class ItemBoxView : MonoBehaviour
{
    [Header("Slots")]
    [SerializeField] private ItemSlotView[] slots;

    [Header("Detail Panel")]
    [SerializeField] private ItemDetailPanel detailPanel;

    private ItemSlotView selectedSlot;
    private ItemData selectedItem;

    private void Start()
    {
        if (detailPanel != null)
            detailPanel.HideImmediate();

        RefreshView();
    }

    [ContextMenu("Refresh View")]
    public void RefreshView()
    {
        if (slots == null || slots.Length == 0)
            return;

        IReadOnlyList<ItemData> items = ItemBoxManager.Instance != null
            ? ItemBoxManager.Instance.GetItems()
            : null;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null) continue;

            slots[i].Setup(this);

            ItemData item = null;
            if (items != null && i < items.Count)
                item = items[i];

            slots[i].SetItem(item);
        }

        // ˆê——XVŽž‚Í‘I‘ð‰ðœ
        selectedSlot = null;
        selectedItem = null;

        if (detailPanel != null)
            detailPanel.HideImmediate();
    }

    public void OnClickSlot(ItemSlotView slot, ItemData item)
    {
        selectedSlot = slot;
        selectedItem = item;

        if (detailPanel == null)
            return;

        if (item == null)
        {
            detailPanel.HideImmediate();
            return;
        }

        detailPanel.Show(item);
    }
}