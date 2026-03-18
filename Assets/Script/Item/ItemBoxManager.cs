using System;
using System.Collections.Generic;
using UnityEngine;

public class ItemBoxManager : MonoBehaviour
{
    public static ItemBoxManager Instance { get; private set; }

    [Header("Capacity")]
    [SerializeField] private int capacity = 10;

    // 所持品リスト。ItemData ではなく InventoryItem で管理する。
    [SerializeField] private List<InventoryItem> items = new();

    public int Capacity => capacity;
    public int Count => items.Count;
    public bool IsFull => items.Count >= capacity;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public IReadOnlyList<InventoryItem> GetItems() => items;

    public bool CanAddItem(ItemData data) => data != null && items.Count < capacity;

    public bool AddItem(ItemData data)
    {
        if (!CanAddItem(data)) return false;
        items.Add(new InventoryItem(data));
        SortItems();
        Debug.Log($"[ItemBoxManager] AddItem: {data.itemName} (Count={items.Count})");
        return true;
    }

    public bool RemoveItem(InventoryItem invItem)
    {
        if (invItem == null) return false;

        // 装備中なら解除
        if (GameState.I != null && GameState.I.equippedWeaponUid == invItem.uid)
            GameState.I.equippedWeaponUid = "";

        bool removed = items.Remove(invItem);
        if (removed) SortItems();
        return removed;
    }

    public void EquipItem(InventoryItem invItem)
    {
        if (invItem == null || invItem.data == null) return;
        if (invItem.data.category != ItemCategory.Weapon) return;
        if (GameState.I != null)
            GameState.I.equippedWeaponUid = invItem.uid;
        Debug.Log($"[ItemBoxManager] Equip: {invItem.data.itemName} uid={invItem.uid}");
    }

    public void UnequipItem(InventoryItem invItem)
    {
        if (invItem == null || GameState.I == null) return;
        if (GameState.I.equippedWeaponUid == invItem.uid)
            GameState.I.equippedWeaponUid = "";
    }

    //removeは内部的にインベントリから削除する操作
    //discardはプレイヤーがUIから削除する操作　そのため今後を考慮して設計上分けている
    public bool DiscardItem(InventoryItem invItem) => RemoveItem(invItem);

    public void ClearAll()
    {
        if (GameState.I != null) GameState.I.equippedWeaponUid = "";
        items.Clear();
    }

    private void SortItems() => items.Sort(CompareItems);

    private int CompareItems(InventoryItem a, InventoryItem b)
    {
        if (a?.data == null && b?.data == null) return 0;
        if (a?.data == null) return 1;
        if (b?.data == null) return -1;
        int c = GetCategoryPriority(a.data.category).CompareTo(GetCategoryPriority(b.data.category));
        if (c != 0) return c;
        int s = a.data.sortOrder.CompareTo(b.data.sortOrder);
        if (s != 0) return s;
        return string.Compare(a.data.itemId, b.data.itemId, StringComparison.Ordinal);
    }

    private int GetCategoryPriority(ItemCategory cat)
    {
        switch (cat)
        {
            case ItemCategory.Consumable: return 0;
            case ItemCategory.Weapon: return 1;
            case ItemCategory.Magic: return 2;
        }
        return 999;
    }
}