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
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public IReadOnlyList<ItemData> GetItems() => items;

    public bool CanAddItem(ItemData item) => item != null && items.Count < capacity;

    public bool AddItem(ItemData item)
    {
        if (!CanAddItem(item)) return false;
        items.Add(item);
        SortItems();
        Debug.Log($"[ItemBoxManager] AddItem: {item.itemName} (Count={items.Count})");
        return true;
    }

    public bool RemoveItem(ItemData item)
    {
        if (item == null) return false;
        // 削除前にインデックスを確認し、装備中なら解除する
        int index = items.IndexOf(item);
        if (index >= 0 && GameState.I != null)
        {
            string key = $"{item.itemId}:{index}";
            if (GameState.I.equippedSlotKey == key)
                GameState.I.equippedSlotKey = "";
        }
        bool removed = items.Remove(item);
        if (removed) SortItems();
        return removed;
    }

    // -----------------------------------------------------------------
    // 装備 / 解除 / 廃棄
    // -----------------------------------------------------------------

    /// <summary>
    /// スロット単位のインスタンスIDで装備を記録する。
    /// 同名武器を複数持っていても1つだけ光らせるために
    /// item参照ではなくインスタンスIDを使う。
    /// </summary>
    public void EquipItem(ItemData item, int index)
    {
        if (item == null || item.category != ItemCategory.Weapon) return;
        if (GameState.I != null)
            GameState.I.equippedSlotKey = $"{item.itemId}:{index}";
        Debug.Log($"[ItemBoxManager] Equip: {item.itemName} key={GameState.I?.equippedSlotKey}");
    }

    public void UnequipItem(ItemData item, int index)
    {
        if (GameState.I == null) return;
        string key = $"{item.itemId}:{index}";
        if (GameState.I.equippedSlotKey == key)
            GameState.I.equippedSlotKey = "";
    }

    // DiscardItem: RemoveItem の中で装備解除も行う。引数を item のみに変更。
    public bool DiscardItem(ItemData item)
    {
        return RemoveItem(item);
    }

    public void ClearAll() => items.Clear();

    // -----------------------------------------------------------------
    private void SortItems() => items.Sort(CompareItems);

    private int CompareItems(ItemData a, ItemData b)
    {
        if (a == null && b == null) return 0;
        if (a == null) return 1;
        if (b == null) return -1;
        int c = GetCategoryPriority(a.category).CompareTo(GetCategoryPriority(b.category));
        if (c != 0) return c;
        int s = a.sortOrder.CompareTo(b.sortOrder);
        if (s != 0) return s;
        return string.Compare(a.itemId, b.itemId, StringComparison.Ordinal);
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