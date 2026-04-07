using System;
using System.Collections.Generic;
using UnityEngine;

public class ItemBoxManager : MonoBehaviour
{
    public static ItemBoxManager Instance { get; private set; }

    [Header("Capacity")]
    [SerializeField] private int capacity = 10;

    [Header("Item Database (セーブ復元用)")]
    [Tooltip("セーブデータから itemId でアイテムを復元するために必要")]
    [SerializeField] private ItemDatabase itemDatabase;

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
        SaveManager.Save(); // 即時セーブ

        // MaxHpBonus / DefenseBonus を持つアイテムの追加に備えて maxHp を再計算
        if (GameState.I != null) GameState.I.RecalcMaxHp();

        return true;
    }

    public bool RemoveItem(InventoryItem invItem)
    {
        if (invItem == null) return false;

        // 装備中なら解除
        if (GameState.I != null && GameState.I.equippedWeaponUid == invItem.uid)
            GameState.I.equippedWeaponUid = "";

        bool removed = items.Remove(invItem);
        if (removed)
        {
            SortItems();
            SaveManager.Save(); // 即時セーブ

            // MaxHpBonus を持つアイテムの破棄で maxHp が下がる場合に備えて再計算
            // RecalcMaxHp 内で currentHp > maxHp ならクランプされる
            if (GameState.I != null) GameState.I.RecalcMaxHp();
        }
        return removed;
    }

    public void EquipItem(InventoryItem invItem)
    {
        if (invItem == null || invItem.data == null) return;
        if (invItem.data.category != ItemCategory.Weapon) return;
        if (GameState.I != null)
            GameState.I.equippedWeaponUid = invItem.uid;
        Debug.Log($"[ItemBoxManager] Equip: {invItem.data.itemName} uid={invItem.uid}");

        // 装備の equipMaxHp / equipMaxMp を即座に反映するため再計算
        if (GameState.I != null)
        {
            GameState.I.RecalcMaxHp();
            GameState.I.RecalcMaxMp();
        }

        SaveManager.Save(); // 即時セーブ
    }

    public void UnequipItem(InventoryItem invItem)
    {
        if (invItem == null || GameState.I == null) return;
        if (GameState.I.equippedWeaponUid == invItem.uid)
        {
            GameState.I.equippedWeaponUid = "";

            // 装備解除で equipMaxHp / equipMaxMp が無くなるため再計算
            // RecalcMaxHp 内で currentHp > 新maxHp ならクランプされる
            GameState.I.RecalcMaxHp();
            GameState.I.RecalcMaxMp();

            SaveManager.Save(); // 即時セーブ
        }
    }

    //removeは内部的にインベントリから削除する操作
    //discardはプレイヤーがUIから削除する操作　そのため今後を考慮して設計上分けている
    public bool DiscardItem(InventoryItem invItem) => RemoveItem(invItem);

    public void ClearAll()
    {
        if (GameState.I != null) GameState.I.equippedWeaponUid = "";
        items.Clear();
    }

    // =========================================================
    // セーブデータからの復元
    // =========================================================

    /// <summary>
    /// セーブデータから所持品リストを復元する。
    /// itemId を使って ItemDatabase からマスターデータを検索し、
    /// uid を引き継いで InventoryItem を再構築する。
    /// </summary>
    public void RestoreFromSave(List<SavedItem> savedItems)
    {
        items.Clear();

        if (savedItems == null || itemDatabase == null)
        {
            Debug.LogWarning("[ItemBoxManager] 復元データまたは ItemDatabase が null");
            return;
        }

        foreach (var saved in savedItems)
        {
            if (string.IsNullOrEmpty(saved.itemId)) continue;

            // ItemDatabase からマスターデータを検索
            ItemData data = FindItemDataById(saved.itemId);
            if (data == null)
            {
                Debug.LogWarning($"[ItemBoxManager] 復元失敗: itemId={saved.itemId} が ItemDatabase に見つかりません");
                continue;
            }

            // InventoryItem を再構築（uid を引き継ぐ）
            var invItem = new InventoryItem(data);
            invItem.uid = saved.uid; // セーブ時の uid を上書きで復元
            items.Add(invItem);
        }

        SortItems();
        Debug.Log($"[ItemBoxManager] 復元完了: {items.Count} 個のアイテム");

        // 復元後に maxHp を再計算（MaxHpBonus を持つアイテムの復元に対応）
        if (GameState.I != null) GameState.I.RecalcMaxHp();
    }

    // =========================================================
    // ボス戦コンティニュー用スナップショット（追加）
    // =========================================================

    /// <summary>
    /// 現在の所持品リストのスナップショットを作成する。
    /// 各アイテムの uid と itemId のペアを記録する。
    /// ボス戦開始時に BattleContext.ItemSnapshot に保存する。
    /// </summary>
    public List<ItemSnapshotEntry> CreateSnapshot()
    {
        var snapshot = new List<ItemSnapshotEntry>();
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] == null || items[i].data == null) continue;
            snapshot.Add(new ItemSnapshotEntry(items[i].uid, items[i].data.itemId));
        }
        Debug.Log($"[ItemBoxManager] スナップショット作成: {snapshot.Count} 個");
        return snapshot;
    }

    /// <summary>
    /// スナップショットから所持品リストを復元する。
    /// 戦闘中に消費されたアイテムを元に戻す。
    /// uid を引き継ぐので装備状態も維持される。
    /// </summary>
    public void RestoreFromSnapshot(List<ItemSnapshotEntry> snapshot)
    {
        if (snapshot == null || itemDatabase == null)
        {
            Debug.LogWarning("[ItemBoxManager] スナップショットまたは ItemDatabase が null");
            return;
        }

        items.Clear();

        foreach (var entry in snapshot)
        {
            if (string.IsNullOrEmpty(entry.itemId)) continue;

            ItemData data = FindItemDataById(entry.itemId);
            if (data == null)
            {
                Debug.LogWarning($"[ItemBoxManager] スナップショット復元失敗: itemId={entry.itemId} が ItemDatabase に見つかりません");
                continue;
            }

            var invItem = new InventoryItem(data);
            invItem.uid = entry.uid; // 元の uid を復元（装備状態を維持）
            items.Add(invItem);
        }

        SortItems();
        Debug.Log($"[ItemBoxManager] スナップショット復元完了: {items.Count} 個のアイテム");

        // 復元後に maxHp を再計算
        if (GameState.I != null) GameState.I.RecalcMaxHp();
    }

    /// <summary>
    /// ItemDatabase.items から itemId で検索してマスターデータを返す。
    /// </summary>
    private ItemData FindItemDataById(string itemId)
    {
        if (itemDatabase == null) return null;

        foreach (var item in itemDatabase.items)
        {
            if (item != null && item.itemId == itemId)
                return item;
        }
        return null;
    }

    // =========================================================
    // ソート
    // =========================================================

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