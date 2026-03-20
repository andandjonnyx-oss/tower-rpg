using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Itemsouko シーンの Canvas にアタッチ。
/// 左パネル（所持品）と右パネル（倉庫）の両方を管理する。
/// 
/// 【所持品側を選択】
///   消費品: 使う / 捨てる / 預ける
///   武器:   装備(外す) / 捨てる / 預ける
///   魔法:   ― / 捨てる / 預ける
/// 
/// 【倉庫側を選択】
///   消費品: 使う / 捨てる / 引き出す
///   武器:   ―(装備不可) / 捨てる / 引き出す
///   魔法:   ― / 捨てる / 引き出す
/// 
/// 詳細パネルに「閉じる」ボタンは無し。
/// アイテム操作（使う/捨てる/預ける/引き出す）後に自動で非表示。
/// 別のアイテムをクリックすれば上書き表示される。
/// </summary>
public class StorageView : MonoBehaviour
{
    // =========================================================
    // スロット
    // =========================================================
    [Header("Inventory Side (Left)")]
    [SerializeField] private StorageSlotView[] inventorySlots;

    [Header("Storage Side (Right)")]
    [SerializeField] private StorageSlotView[] storageSlots;

    // =========================================================
    // 詳細パネル
    // =========================================================
    [Header("Detail Panel")]
    [SerializeField] private GameObject detailRoot;
    [SerializeField] private TMP_Text detailItemName;
    [SerializeField] private TMP_Text detailDescription;
    [SerializeField] private Image detailItemImage;

    [Header("Detail Buttons")]
    [SerializeField] private Button button1;            // 使う / 装備 / 外す
    [SerializeField] private TMP_Text button1Text;
    [SerializeField] private Button button2;            // 捨てる
    [SerializeField] private TMP_Text button2Text;
    [SerializeField] private Button button3;            // 預ける / 引き出す
    [SerializeField] private TMP_Text button3Text;

    // =========================================================
    // ナビゲーション
    // =========================================================
    [Header("Navigation")]
    [SerializeField] private Button backButton;
    [SerializeField] private string mainSceneName = "Main";

    // =========================================================
    // 内部状態
    // =========================================================
    private InventoryItem selectedItem;
    private bool selectedFromInventory;

    // =========================================================
    // 初期化
    // =========================================================
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

    // =========================================================
    // 表示更新
    // =========================================================
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
    // スロットクリック（StorageSlotView から呼ばれる）
    // =========================================================
    public void OnInventorySlotClicked(InventoryItem invItem)
    {
        if (invItem == null) { HideDetail(); return; }
        selectedItem = invItem;
        selectedFromInventory = true;
        ShowDetail();
    }

    public void OnStorageSlotClicked(InventoryItem invItem)
    {
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
        if (invItem?.data == null) { HideDetail(); return; }

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

        if (detailRoot != null) detailRoot.SetActive(true);
    }

    /// 所持品側: 使う/装備/外す + 捨てる + 預ける
    private void ApplyInventoryButtons(InventoryItem invItem)
    {
        // Button1: 使う / 装備 / 外す
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

        // Button2: 捨てる
        if (button2 != null)
        {
            button2.gameObject.SetActive(true);
            if (button2Text != null) button2Text.text = "捨てる";
        }

        // Button3: 預ける
        if (button3 != null)
        {
            button3.gameObject.SetActive(true);
            if (button3Text != null) button3Text.text = "預ける";
            bool canDeposit = StorageManager.Instance != null && !StorageManager.Instance.IsFull;
            button3.interactable = canDeposit;
        }
    }

    /// 倉庫側: 使う(消費品のみ) / 捨てる / 引き出す。武器の装備は不可
    private void ApplyStorageButtons(InventoryItem invItem)
    {
        // Button1: 消費品なら「使う」、それ以外は非表示
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

        // Button2: 捨てる
        if (button2 != null)
        {
            button2.gameObject.SetActive(true);
            if (button2Text != null) button2Text.text = "捨てる";
        }

        // Button3: 引き出す
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
        selectedItem = null;
        if (detailRoot != null) detailRoot.SetActive(false);
    }

    // =========================================================
    // ボタンハンドラ
    // =========================================================

    /// Button1: 使う / 装備 / 外す
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

    /// Button2: 捨てる（所持品側 / 倉庫側 両方対応）
    private void OnButton2Clicked()
    {
        if (selectedItem == null) return;

        if (selectedFromInventory)
            DiscardFromInventory();
        else
            DiscardFromStorage();
    }

    /// Button3: 預ける / 引き出す
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

    // --- 所持品側 ---

    private void UseConsumableFromInventory()
    {
        // TODO: 回復などの効果はここに実装する
        ItemBoxManager.Instance?.RemoveItem(selectedItem);
        Debug.Log($"[StorageView] 所持品から使用: {selectedItem.data.itemName}");
        HideDetail();
        RefreshAll();
    }

    private void EquipWeapon()
    {
        ItemBoxManager.Instance?.EquipItem(selectedItem);
        // 装備/外すはアイテムが消えないので詳細を更新して表示し直す
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

    // --- 倉庫側 ---

    private void UseConsumableFromStorage()
    {
        // TODO: 回復などの効果はここに実装する
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

    // --- 預ける / 引き出す ---

    /// 所持品 → 倉庫に預ける
    private void DepositItem(InventoryItem invItem)
    {
        if (StorageManager.Instance == null || ItemBoxManager.Instance == null) return;
        if (StorageManager.Instance.IsFull) return;

        // 装備中なら解除
        if (GameState.I != null && GameState.I.equippedWeaponUid == invItem.uid)
            GameState.I.equippedWeaponUid = "";

        ItemBoxManager.Instance.RemoveItem(invItem);
        StorageManager.Instance.AddInventoryItem(invItem);

        Debug.Log($"[StorageView] 預けた: {invItem.data.itemName}");
        HideDetail();
        RefreshAll();
    }

    /// 倉庫 → 所持品に引き出す
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

    // =========================================================
    // 戻るボタン
    // =========================================================
    private void OnBackClicked()
    {
        SceneManager.LoadScene(mainSceneName);
    }
}