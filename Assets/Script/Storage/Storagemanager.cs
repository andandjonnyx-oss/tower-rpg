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

    [Header("Item Database (セーブ復元用)")]
    [Tooltip("セーブデータから itemId でアイテムを復元するために必要")]
    [SerializeField] private ItemDatabase itemDatabase;

    [SerializeField] private List<InventoryItem> items = new();

    public int Capacity => capacity;
    public int Count => items.Count;
    public bool IsFull => items.Count >= capacity;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // シーンにシリアライズされた空エントリ（data == null）を起動時に除去する。
        // 過去に Title.unity の items に空要素が 100 個入っており、capacity(100) と
        // 同数のため新規プレイヤーの初回セッションだけ IsFull が true になり
        // 「預ける」がグレーアウトする不具合があった（セーブ後の再起動では
        // RestoreFromSave が items.Clear() するため再現しない）。
        // シーン側のデータ事故が再発しても無害化できるよう、ここで防御する。
        int removed = items.RemoveAll(x => x == null || x.data == null);
        if (removed > 0)
            Debug.LogWarning($"[StorageManager] 無効な倉庫エントリを {removed} 件除去しました");
    }

    /// <summary>
    /// 容量を外部から設定する。StorageContext のスロット自動生成時に呼ばれる。
    /// </summary>
    public void SetCapacity(int newCapacity)
    {
        capacity = Mathf.Max(newCapacity, items.Count);
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
        SaveManager.Save(); // 即時セーブ

        // 図鑑登録。倉庫側で「使う/食べる」した変化先アイテムはここにしか来ないため必須。
        // ItemBoxManager.AddItem と同じ方針（登録済みなら無害）。
        if (GameState.I != null) GameState.I.MarkItemDiscovered(data.itemId);
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
        SaveManager.Save(); // 即時セーブ
        return true;
    }

    /// 倉庫からアイテムを削除する（引き出す時に使う）
    public bool RemoveItem(InventoryItem invItem)
    {
        if (invItem == null) return false;
        bool removed = items.Remove(invItem);
        if (removed)
        {
            SortItems();
            SaveManager.Save(); // 即時セーブ
        }
        return removed;
    }

    public void ClearAll()
    {
        items.Clear();
    }

    // =========================================================
    // セーブデータからの復元
    // =========================================================

    /// <summary>
    /// セーブデータから倉庫アイテムリストを復元する。
    /// ItemBoxManager.RestoreFromSave と同じパターン。
    /// </summary>
    public void RestoreFromSave(List<SavedItem> savedItems)
    {
        items.Clear();

        if (savedItems == null || itemDatabase == null)
        {
            Debug.LogWarning("[StorageManager] 復元データまたは ItemDatabase が null");
            return;
        }

        foreach (var saved in savedItems)
        {
            if (string.IsNullOrEmpty(saved.itemId)) continue;

            ItemData data = FindItemDataById(saved.itemId);
            if (data == null)
            {
                Debug.LogWarning($"[StorageManager] 復元失敗: itemId={saved.itemId} が ItemDatabase に見つかりません");
                continue;
            }

            var invItem = new InventoryItem(data);
            invItem.uid = saved.uid;
            items.Add(invItem);
        }

        SortItems();
        Debug.Log($"[StorageManager] 復元完了: {items.Count} 個のアイテム");
    }

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