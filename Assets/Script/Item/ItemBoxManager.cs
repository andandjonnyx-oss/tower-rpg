using System;
using System.Collections.Generic;
using UnityEngine;

public class ItemBoxManager : MonoBehaviour
{
    public static ItemBoxManager Instance { get; private set; }

    [Header("Capacity")]
    [SerializeField] private int capacity = 10;

    [Header("Debug")]
    [SerializeField] private List<ItemData> items = new();

    public int Capacity => capacity;
    public int Count => items.Count;
    public bool IsFull => items.Count >= capacity;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public IReadOnlyList<ItemData> GetItems()
    {
        return items;
    }

    public bool CanAddItem(ItemData item)
    {
        if (item == null) return false;
        return items.Count < capacity;
    }

    public bool AddItem(ItemData item)
    {
        Debug.Log($"[ItemBoxManager] AddItem called: {(item != null ? item.itemName : "NULL")}");
        Debug.Log($"[ItemBoxManager] Before Count = {items.Count}, Capacity = {capacity}");

        if (!CanAddItem(item))
        {
            Debug.Log("[ItemBoxManager] AddItem failed.");
            return false;
        }

        items.Add(item);
        SortItems();

        Debug.Log($"[ItemBoxManager] AddItem success. After Count = {items.Count}");
        return true;
    }

    public bool RemoveItem(ItemData item)
    {
        if (item == null) return false;

        bool removed = items.Remove(item);

        if (removed)
        {
            SortItems();
            Debug.Log($"[ItemBoxManager] RemoveItem success: {item.itemName}");
        }
        else
        {
            Debug.LogWarning($"[ItemBoxManager] RemoveItem failed: {item.itemName}");
        }

        return removed;
    }

    public void ClearAll()
    {
        items.Clear();
    }

    private void SortItems()
    {
        items.Sort(CompareItems);
    }

    private int CompareItems(ItemData a, ItemData b)
    {
        if (a == null && b == null) return 0;
        if (a == null) return 1;
        if (b == null) return -1;

        // ‡@ ƒJƒeƒSƒŠ—Dæ“x
        int categoryCompare = GetCategoryPriority(a.category).CompareTo(GetCategoryPriority(b.category));
        if (categoryCompare != 0)
            return categoryCompare;

        // ‡A sortOrder
        int sortOrderCompare = a.sortOrder.CompareTo(b.sortOrder);
        if (sortOrderCompare != 0)
            return sortOrderCompare;

        // ‡B itemIdiÅŒã‚Ì•ÛŒ¯j
        return string.Compare(a.itemId, b.itemId, StringComparison.Ordinal);
    }

    private int GetCategoryPriority(ItemCategory category)
    {
        switch (category)
        {
            case ItemCategory.Consumable:
                return 0;

            case ItemCategory.Weapon:
                return 1;

            case ItemCategory.Magic:
                return 2;
        }

        return 999;
    }
}