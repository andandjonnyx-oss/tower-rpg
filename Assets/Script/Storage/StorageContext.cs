using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Itemsouko（倉庫）シーン用コントローラー。
/// 所持品と倉庫の2列を表示し、使う/装備/捨てる/預ける/引き出すの操作を提供する。
/// </summary>
public class StorageContext : MonoBehaviour, IItemContext
{
    [Header("Inventory Slots (Left)")]
    [SerializeField] private ItemSlotView[] inventorySlots;

    [Header("Storage Slots (Right)")]
    [SerializeField] private ItemSlotView[] storageSlots;

    [Header("Detail Panel")]
    [SerializeField] private ItemDetailPanel detailPanel;

    [Header("Navigation")]
    [SerializeField] private Button backButton;
    [SerializeField] private string mainSceneName = "Main";

    private InventoryItem selectedItem;
    private bool selectedFromInventory;

    private void Start()
    {
        // 所持品スロットにコールバック登録
        if (inventorySlots != null)
            foreach (var s in inventorySlots)
                if (s != null) s.onClicked = OnInventorySlotClicked;

        // 倉庫スロットにコールバック登録
        if (storageSlots != null)
            foreach (var s in storageSlots)
                if (s != null) s.onClicked = OnStorageSlotClicked;

        if (backButton != null)
            backButton.onClick.AddListener(() => SceneManager.LoadScene(mainSceneName));

        if (detailPanel != null) detailPanel.Hide();
        RefreshSlots();
    }

    private void OnInventorySlotClicked(ItemSlotView slot, InventoryItem invItem)
    {
        if (invItem == null) { detailPanel?.Hide(); return; }
        selectedItem = invItem;
        selectedFromInventory = true;
        detailPanel?.Show(invItem, this, fromInventory: true);
    }

    private void OnStorageSlotClicked(ItemSlotView slot, InventoryItem invItem)
    {
        if (invItem == null) { detailPanel?.Hide(); return; }
        selectedItem = invItem;
        selectedFromInventory = false;
        detailPanel?.Show(invItem, this, fromInventory: false);
    }

    // =========================================================
    // IItemContext
    // =========================================================
    public List<DetailButtonDef> GetButtons(InventoryItem invItem, bool fromInventory)
    {
        var list = new List<DetailButtonDef>();
        if (invItem?.data == null) return list;

        if (fromInventory)
            BuildInventoryButtons(invItem, list);
        else
            BuildStorageButtons(invItem, list);

        return list;
    }

    private void BuildInventoryButtons(InventoryItem invItem, List<DetailButtonDef> list)
    {
        switch (invItem.data.category)
        {
            case ItemCategory.Consumable:
                list.Add(new DetailButtonDef("使う", () => UseConsumableFromInventory(invItem)));
                break;
            case ItemCategory.Weapon:
                bool equipped = GameState.I != null
                    && GameState.I.equippedWeaponUid == invItem.uid;
                if (equipped)
                    list.Add(new DetailButtonDef("外す", () => UnequipWeapon(invItem)));
                else
                    list.Add(new DetailButtonDef("装備", () => EquipWeapon(invItem)));
                break;
            case ItemCategory.Magic:
                break;
        }

        list.Add(new DetailButtonDef("捨てる", () => DiscardFromInventory(invItem)));

        bool canDeposit = StorageManager.Instance != null && !StorageManager.Instance.IsFull;
        list.Add(new DetailButtonDef("預ける", () => DepositItem(invItem), canDeposit));
    }

    private void BuildStorageButtons(InventoryItem invItem, List<DetailButtonDef> list)
    {
        if (invItem.data.category == ItemCategory.Consumable)
            list.Add(new DetailButtonDef("使う", () => UseConsumableFromStorage(invItem)));

        list.Add(new DetailButtonDef("捨てる", () => DiscardFromStorage(invItem)));

        bool canWithdraw = ItemBoxManager.Instance != null && !ItemBoxManager.Instance.IsFull;
        list.Add(new DetailButtonDef("引き出す", () => WithdrawItem(invItem), canWithdraw));
    }

    public void RefreshSlots()
    {
        RefreshInventorySide();
        RefreshStorageSide();
    }

    private void RefreshInventorySide()
    {
        if (inventorySlots == null) return;
        IReadOnlyList<InventoryItem> items = ItemBoxManager.Instance?.GetItems();
        for (int i = 0; i < inventorySlots.Length; i++)
        {
            if (inventorySlots[i] == null) continue;
            InventoryItem invItem = (items != null && i < items.Count) ? items[i] : null;
            inventorySlots[i].SetItem(invItem);
        }
    }

    private void RefreshStorageSide()
    {
        if (storageSlots == null) return;
        IReadOnlyList<InventoryItem> items = StorageManager.Instance?.GetItems();
        for (int i = 0; i < storageSlots.Length; i++)
        {
            if (storageSlots[i] == null) continue;
            InventoryItem invItem = (items != null && i < items.Count) ? items[i] : null;
            storageSlots[i].SetItem(invItem);
        }
    }

    // =========================================================
    // Operations
    // =========================================================
    private void UseConsumableFromInventory(InventoryItem invItem)
    {
        ItemBoxManager.Instance?.RemoveItem(invItem);
        AfterAction();
    }

    private void UseConsumableFromStorage(InventoryItem invItem)
    {
        StorageManager.Instance?.RemoveItem(invItem);
        AfterAction();
    }

    private void EquipWeapon(InventoryItem invItem)
    {
        ItemBoxManager.Instance?.EquipItem(invItem);
        AfterAction();
    }

    private void UnequipWeapon(InventoryItem invItem)
    {
        ItemBoxManager.Instance?.UnequipItem(invItem);
        AfterAction();
    }

    private void DiscardFromInventory(InventoryItem invItem)
    {
        ItemBoxManager.Instance?.DiscardItem(invItem);
        AfterAction();
    }

    private void DiscardFromStorage(InventoryItem invItem)
    {
        StorageManager.Instance?.RemoveItem(invItem);
        AfterAction();
    }

    private void DepositItem(InventoryItem invItem)
    {
        if (StorageManager.Instance == null || ItemBoxManager.Instance == null) return;
        if (StorageManager.Instance.IsFull) return;

        if (GameState.I != null && GameState.I.equippedWeaponUid == invItem.uid)
            GameState.I.equippedWeaponUid = "";

        ItemBoxManager.Instance.RemoveItem(invItem);
        StorageManager.Instance.AddInventoryItem(invItem);
        AfterAction();
    }

    private void WithdrawItem(InventoryItem invItem)
    {
        if (StorageManager.Instance == null || ItemBoxManager.Instance == null) return;
        if (ItemBoxManager.Instance.IsFull) return;

        StorageManager.Instance.RemoveItem(invItem);
        ItemBoxManager.Instance.AddItem(invItem.data);
        AfterAction();
    }

    private void AfterAction()
    {
        detailPanel?.Hide();
        selectedItem = null;
        RefreshSlots();
    }
}