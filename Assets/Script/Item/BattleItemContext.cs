using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 戦闘中アイテム使用シーン用コントローラー。
/// 消耗品のみ「使う」が可能。装備変更・捨てるは不可。
/// </summary>
public class BattleItemContext : MonoBehaviour, IItemContext
{
    [Header("Slots")]
    [SerializeField] private ItemSlotView[] slots;

    [Header("Detail Panel")]
    [SerializeField] private ItemDetailPanel detailPanel;

    /// <summary>
    /// 戦闘シーンから呼ばれるコールバック。使用したアイテムを返す。
    /// null = キャンセル。
    /// </summary>
    public System.Action<InventoryItem> onItemUsed;

    /// <summary>
    /// キャンセル時のコールバック。
    /// </summary>
    public System.Action onCancelled;

    private void Start()
    {
        if (slots != null)
            foreach (var s in slots)
                if (s != null) s.onClicked = OnSlotClicked;

        if (detailPanel != null) detailPanel.Hide();
        RefreshSlots();
    }

    private void OnSlotClicked(ItemSlotView slot, InventoryItem invItem)
    {
        if (invItem == null) { detailPanel?.Hide(); return; }
        detailPanel?.Show(invItem, this, fromInventory: true);
    }

    // =========================================================
    // IItemContext
    // =========================================================
    public List<DetailButtonDef> GetButtons(InventoryItem invItem, bool fromInventory)
    {
        var list = new List<DetailButtonDef>();
        if (invItem?.data == null) return list;

        // 戦闘中は消耗品のみ使用可能
        if (invItem.data.category == ItemCategory.Consumable)
        {
            list.Add(new DetailButtonDef("使う", () => UseInBattle(invItem)));
        }

        list.Add(new DetailButtonDef("やめる", () => Cancel()));

        return list;
    }

    public void RefreshSlots()
    {
        if (slots == null) return;
        IReadOnlyList<InventoryItem> items = ItemBoxManager.Instance?.GetItems();
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null) continue;
            InventoryItem invItem = (items != null && i < items.Count) ? items[i] : null;
            slots[i].SetItem(invItem);
        }
    }

    // =========================================================
    // Operations
    // =========================================================
    private void UseInBattle(InventoryItem invItem)
    {
        // TODO: 回復効果はここまたは呼び出し元で処理
        ItemBoxManager.Instance?.RemoveItem(invItem);
        detailPanel?.Hide();
        RefreshSlots();
        onItemUsed?.Invoke(invItem);
    }

    private void Cancel()
    {
        detailPanel?.Hide();
        onCancelled?.Invoke();
    }
}