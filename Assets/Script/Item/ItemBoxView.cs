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
        if (detailPanel != null) detailPanel.HideImmediate();
        RefreshView();
    }

    [ContextMenu("Refresh View")]
    public void RefreshView()
    {
        if (slots == null || slots.Length == 0) return;

        IReadOnlyList<ItemData> items = ItemBoxManager.Instance != null
            ? ItemBoxManager.Instance.GetItems()
            : null;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null) continue;
            slots[i].Setup(this);
            ItemData item = (items != null && i < items.Count) ? items[i] : null;
            slots[i].SetItem(item, i); // SetItem 内で RefreshEquipColor も呼ばれる
        }

        if (detailPanel != null) detailPanel.HideImmediate();
    }

    public void OnClickSlot(ItemSlotView slot, ItemData item)
    {
        if (detailPanel == null) return;

        if (item == null)
        {
            detailPanel.HideImmediate();
            return;
        }

        // スロットのインスタンスIDを一緒に渡す
        detailPanel.Show(item, slot.SlotIndex, this);
    }
}