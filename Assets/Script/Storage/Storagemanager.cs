using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// アイテム倉庫を管理するシングルトン。
/// ItemBoxManager（所持品）と同じパターンで、DontDestroyOnLoad で永続化する。
/// </summary>
public class StorageManager : MonoBehaviour
{
    public static StorageManager Instance { get; private set; }

    [Header("Capacity")]
    [SerializeField] private int capacity = 100;

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

    /// 倉庫にアイテムを追加する（預ける時に使う）
    public bool AddItem(ItemData data)
    {
        if (!CanAddItem(data)) return false;
        items.Add(new InventoryItem(data));
        SortItems();
        Debug.Log($"[StorageManager] AddItem: {data.itemName} (Count={items.Count})");
        return true;
    }

    /// InventoryItem をそのまま倉庫に移す（uid を維持したい場合）
    public bool AddInventoryItem(InventoryItem invItem)
    {
        if (invItem == null || invItem.data == null) return false;
        if (items.Count >= capacity) return false;
        items.Add(invItem);
        SortItems();
        Debug.Log($"[StorageManager] AddInventoryItem: {invItem.data.itemName} (Count={items.Count})");
        return true;
    }

    /// 倉庫からアイテムを削除する（引き出す時に使う）
    public bool RemoveItem(InventoryItem invItem)
    {
        if (invItem == null) return false;
        bool removed = items.Remove(invItem);
        if (removed) SortItems();
        return removed;
    }

    public void ClearAll()
    {
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