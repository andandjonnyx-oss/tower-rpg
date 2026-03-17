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
    public void EquipItem(ItemData item, int instanceId)
    {
        if (item == null || item.category != ItemCategory.Weapon) return;
        if (GameState.I != null)
            GameState.I.equippedWeaponInstanceId = instanceId;
        Debug.Log($"[ItemBoxManager] Equip: {item.itemName} (instanceId={instanceId})");
    }

    /// <summary>
    /// 指定インスタンスIDが装備中なら解除する。
    /// </summary>
    public void UnequipItem(int instanceId)
    {
        if (GameState.I == null) return;
        if (GameState.I.equippedWeaponInstanceId == instanceId)
        {
            GameState.I.equippedWeaponInstanceId = -1;
            Debug.Log($"[ItemBoxManager] Unequip: instanceId={instanceId}");
        }
    }

    /// <summary>
    /// アイテムを捨てる。装備中なら自動解除してから削除する。
    /// </summary>
    public bool DiscardItem(ItemData item, int instanceId)
    {
        if (item == null) return false;
        UnequipItem(instanceId);
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