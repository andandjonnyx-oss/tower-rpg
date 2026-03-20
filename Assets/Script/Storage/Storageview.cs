using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class StorageView : MonoBehaviour
{
    [Header("Inventory Side (Left)")]
    [SerializeField] private StorageSlotView[] inventorySlots;

    [Header("Storage Side (Right)")]
    [SerializeField] private StorageSlotView[] storageSlots;

    [Header("Detail Panel")]
    [SerializeField] private GameObject detailRoot;
    [SerializeField] private TMP_Text detailItemName;
    [SerializeField] private TMP_Text detailDescription;
    [SerializeField] private Image detailItemImage;

    [Header("Detail Buttons")]
    [SerializeField] private Button button1;
    [SerializeField] private TMP_Text button1Text;
    [SerializeField] private Button button2;
    [SerializeField] private TMP_Text button2Text;
    [SerializeField] private Button button3;
    [SerializeField] private TMP_Text button3Text;

    [Header("Navigation")]
    [SerializeField] private Button backButton;
    [SerializeField] private string mainSceneName = "Main";

    private InventoryItem selectedItem;
    private bool selectedFromInventory;

    private void Awake()
    {
        if (button1 != null) button1.onClick.AddListener(OnButton1Clicked);
        if (button2 != null) button2.onClick.AddListener(OnButton2Clicked);
        if (button3 != null) button3.onClick.AddListener(OnButton3Clicked);
        if (backButton != null) backButton.onClick.AddListener(OnBackClicked);
    }

    private void Start()
    {
        if (inventorySlots != null)
            foreach (var s in inventorySlots)
                if (s != null) s.Setup(this);

        if (storageSlots != null)
            foreach (var s in storageSlots)
                if (s != null) s.Setup(this);

        HideDetail();
        RefreshAll();
    }

    public void RefreshAll()
    {
        RefreshInventorySide();
        RefreshStorageSide();
    }

    private void RefreshInventorySide()
    {
        if (inventorySlots == null) return;
        IReadOnlyList<InventoryItem> items = ItemBoxManager.Instance != null
            ? ItemBoxManager.Instance.GetItems() : null;

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
        IReadOnlyList<InventoryItem> items = StorageManager.Instance != null
            ? StorageManager.Instance.GetItems() : null;

        for (int i = 0; i < storageSlots.Length; i++)
        {
            if (storageSlots[i] == null) continue;
            InventoryItem invItem = (items != null && i < items.Count) ? items[i] : null;
            storageSlots[i].SetItem(invItem);
        }
    }

    // =========================================================
    // スロットクリック
    // =========================================================
    public void OnInventorySlotClicked(InventoryItem invItem)
    {
        Debug.Log($"[StorageView] OnInventorySlotClicked: {(invItem != null ? invItem.data?.itemName : "NULL")}");

        if (invItem == null) { HideDetail(); return; }
        selectedItem = invItem;
        selectedFromInventory = true;
        ShowDetail();
    }

    public void OnStorageSlotClicked(InventoryItem invItem)
    {
        Debug.Log($"[StorageView] OnStorageSlotClicked: {(invItem != null ? invItem.data?.itemName : "NULL")}");

        if (invItem == null) { HideDetail(); return; }
        selectedItem = invItem;
        selectedFromInventory = false;
        ShowDetail();
    }

    // =========================================================
    // 詳細パネル表示
    // =========================================================
    private void ShowDetail()
    {
        var invItem = selectedItem;
        if (invItem?.data == null)
        {
            Debug.LogWarning("[StorageView] ShowDetail: invItem or data is null → HideDetail");
            HideDetail();
            return;
        }

        Debug.Log($"[StorageView] ShowDetail: {invItem.data.itemName}, detailRoot={(detailRoot != null ? detailRoot.name : "NULL")}");

        var data = invItem.data;

        if (detailItemName != null) detailItemName.text = data.itemName;
        if (detailDescription != null) detailDescription.text = data.description;
        if (detailItemImage != null)
        {
            detailItemImage.sprite = data.icon;
            detailItemImage.enabled = data.icon != null;
        }

        if (selectedFromInventory)
            ApplyInventoryButtons(invItem);
        else
            ApplyStorageButtons(invItem);

        if (detailRoot != null)
        {
            detailRoot.SetActive(true);
            Debug.Log($"[StorageView] detailRoot.SetActive(true) 完了。activeSelf={detailRoot.activeSelf}, activeInHierarchy={detailRoot.activeInHierarchy}");
        }
        else
        {
            Debug.LogError("[StorageView] detailRoot is NULL!");
        }
    }

    private void ApplyInventoryButtons(InventoryItem invItem)
    {
        if (button1 != null)
        {
            switch (invItem.data.category)
            {
                case ItemCategory.Consumable:
                    button1.gameObject.SetActive(true);
                    if (button1Text != null) button1Text.text = "使う";
                    break;
                case ItemCategory.Weapon:
                    button1.gameObject.SetActive(true);
                    bool isEquipped = GameState.I != null
                        && GameState.I.equippedWeaponUid == invItem.uid;
                    if (button1Text != null) button1Text.text = isEquipped ? "外す" : "装備";
                    break;
                case ItemCategory.Magic:
                    button1.gameObject.SetActive(false);
                    break;
            }
        }

        if (button2 != null)
        {
            button2.gameObject.SetActive(true);
            if (button2Text != null) button2Text.text = "捨てる";
        }

        if (button3 != null)
        {
            button3.gameObject.SetActive(true);
            if (button3Text != null) button3Text.text = "預ける";
            bool canDeposit = StorageManager.Instance != null && !StorageManager.Instance.IsFull;
            button3.interactable = canDeposit;
        }
    }

    private void ApplyStorageButtons(InventoryItem invItem)
    {
        if (button1 != null)
        {
            switch (invItem.data.category)
            {
                case ItemCategory.Consumable:
                    button1.gameObject.SetActive(true);
                    if (button1Text != null) button1Text.text = "使う";
                    break;
                case ItemCategory.Weapon:
                    button1.gameObject.SetActive(false);
                    break;
                case ItemCategory.Magic:
                    button1.gameObject.SetActive(false);
                    break;
            }
        }

        if (button2 != null)
        {
            button2.gameObject.SetActive(true);
            if (button2Text != null) button2Text.text = "捨てる";
        }

        if (button3 != null)
        {
            button3.gameObject.SetActive(true);
            if (button3Text != null) button3Text.text = "引き出す";
            bool canWithdraw = ItemBoxManager.Instance != null && !ItemBoxManager.Instance.IsFull;
            button3.interactable = canWithdraw;
        }
    }

    private void HideDetail()
    {
        Debug.Log("[StorageView] HideDetail 呼び出し");
        selectedItem = null;
        if (detailRoot != null) detailRoot.SetActive(false);
    }

    // =========================================================
    // ボタンハンドラ
    // =========================================================
    private void OnButton1Clicked()
    {
        if (selectedItem?.data == null) return;

        if (selectedFromInventory)
        {
            switch (selectedItem.data.category)
            {
                case ItemCategory.Consumable:
                    UseConsumableFromInventory();
                    break;
                case ItemCategory.Weapon:
                    bool isEquipped = GameState.I != null
                        && GameState.I.equippedWeaponUid == selectedItem.uid;
                    if (isEquipped) UnequipWeapon();
                    else EquipWeapon();
                    break;
            }
        }
        else
        {
            if (selectedItem.data.category == ItemCategory.Consumable)
                UseConsumableFromStorage();
        }
    }

    private void OnButton2Clicked()
    {
        if (selectedItem == null) return;

        if (selectedFromInventory)
            DiscardFromInventory();
        else
            DiscardFromStorage();
    }

    private void OnButton3Clicked()
    {
        if (selectedItem == null) return;

        if (selectedFromInventory)
            DepositItem(selectedItem);
        else
            WithdrawItem(selectedItem);
    }

    // =========================================================
    // アイテム操作
    // =========================================================
    private void UseConsumableFromInventory()
    {
        ItemBoxManager.Instance?.RemoveItem(selectedItem);
        Debug.Log($"[StorageView] 所持品から使用: {selectedItem.data.itemName}");
        HideDetail();
        RefreshAll();
    }

    private void EquipWeapon()
    {
        ItemBoxManager.Instance?.EquipItem(selectedItem);
        RefreshAll();
        ShowDetail();
    }

    private void UnequipWeapon()
    {
        ItemBoxManager.Instance?.UnequipItem(selectedItem);
        RefreshAll();
        ShowDetail();
    }

    private void DiscardFromInventory()
    {
        ItemBoxManager.Instance?.DiscardItem(selectedItem);
        Debug.Log($"[StorageView] 所持品から捨てた: {selectedItem.data.itemName}");
        HideDetail();
        RefreshAll();
    }

    private void UseConsumableFromStorage()
    {
        StorageManager.Instance?.RemoveItem(selectedItem);
        Debug.Log($"[StorageView] 倉庫から使用: {selectedItem.data.itemName}");
        HideDetail();
        RefreshAll();
    }

    private void DiscardFromStorage()
    {
        StorageManager.Instance?.RemoveItem(selectedItem);
        Debug.Log($"[StorageView] 倉庫から捨てた: {selectedItem.data.itemName}");
        HideDetail();
        RefreshAll();
    }

    private void DepositItem(InventoryItem invItem)
    {
        if (StorageManager.Instance == null || ItemBoxManager.Instance == null) return;
        if (StorageManager.Instance.IsFull) return;

        if (GameState.I != null && GameState.I.equippedWeaponUid == invItem.uid)
            GameState.I.equippedWeaponUid = "";

        ItemBoxManager.Instance.RemoveItem(invItem);
        StorageManager.Instance.AddInventoryItem(invItem);

        Debug.Log($"[StorageView] 預けた: {invItem.data.itemName}");
        HideDetail();
        RefreshAll();
    }

    private void WithdrawItem(InventoryItem invItem)
    {
        if (StorageManager.Instance == null || ItemBoxManager.Instance == null) return;
        if (ItemBoxManager.Instance.IsFull) return;

        StorageManager.Instance.RemoveItem(invItem);
        ItemBoxManager.Instance.AddItem(invItem.data);

        Debug.Log($"[StorageView] 引き出した: {invItem.data.itemName}");
        HideDetail();
        RefreshAll();
    }

    private void OnBackClicked()
    {
        SceneManager.LoadScene(mainSceneName);
    }
}