using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Itemsouko（倉庫）シーン用コントローラー。
/// 所持品と倉庫の2列を表示し、使う/装備/捨てる/預ける/引き出すの操作を提供する。
/// 倉庫側スロットはアイテム数に応じて Prefab から動的に生成される。
///
/// ボタン構築と効果適用は ItemActionHelper を経由し、
/// ItemboxContext と仕様を統一する。
/// </summary>
public class StorageContext : MonoBehaviour, IItemContext
{
    // =========================================================
    // 戻り先シーンの動的切り替え（追加）
    // =========================================================
    /// <summary>
    /// 倉庫シーンの「戻る」ボタンで遷移するシーン名。
    /// Tower から開いた場合は "Tower" にセットされる。
    /// Main から開いた場合は "Main"（デフォルト）。
    /// 倉庫シーンを開く側で事前にセットすること。
    /// </summary>
    public static string ReturnScene = "Main";

    [Header("Inventory Slots (Left) - Inspector でアサイン")]
    [SerializeField] private ItemSlotView[] inventorySlots;

    [Header("Storage Slots - 動的生成")]
    [Tooltip("スロットの Prefab（ItemSlotView がアタッチ済み）")]
    [SerializeField] private ItemSlotView slotPrefab;

    [Tooltip("スロットを生成する親 Transform（GridLayoutGroup + ContentSizeFitter をアタッチ）")]
    [SerializeField] private Transform storageContent;

    [Tooltip("1行あたりの列数")]
    [SerializeField] private int columns = 4;

    [Tooltip("行数（最大容量 = columns × rows）")]
    [SerializeField] private int rows = 25;

    [Header("Detail Panel")]
    [SerializeField] private ItemDetailPanel detailPanel;

    [Header("Navigation")]
    [SerializeField] private Button backButton;
    [SerializeField] private string mainSceneName = "Main";

    // 現在生成されているスロット
    private List<ItemSlotView> storageSlotList = new List<ItemSlotView>();

    private InventoryItem selectedItem;

#pragma warning disable CS0414
    private bool selectedFromInventory;
#pragma warning restore CS0414

    private void Awake()
    {
        // StorageManager の容量を設定
        int totalCapacity = columns * rows;
        if (StorageManager.Instance != null)
            StorageManager.Instance.SetCapacity(totalCapacity);
    }

    private void Start()
    {
        // 所持品スロットにコールバック登録
        if (inventorySlots != null)
            foreach (var s in inventorySlots)
                if (s != null) s.onClicked = OnInventorySlotClicked;

        if (backButton != null)
        {
            string returnTo = string.IsNullOrEmpty(ReturnScene) ? mainSceneName : ReturnScene;
            backButton.onClick.AddListener(() => SceneManager.LoadScene(returnTo));

            // ボタンラベルを戻り先に応じて変更
            var label = backButton.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (label != null)
            {
                label.text = "戻る";
            }

            ReturnScene = "Main";
        }

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
        // 倉庫画面は常に非バトル (inBattle = false)
        switch (invItem.data.category)
        {
            case ItemCategory.Consumable:
                {
                    var btn = ItemActionHelper.BuildUseConsumableButton(
                        invItem, inBattle: false, () => UseConsumableFromInventory(invItem));
                    if (btn != null) list.Add(btn);
                    break;
                }
            case ItemCategory.Weapon:
                {
                    bool equipped = GameState.I != null
                        && GameState.I.equippedWeaponUid == invItem.uid;
                    if (equipped)
                        list.Add(new DetailButtonDef("外す", () => UnequipWeapon(invItem)));
                    else
                        list.Add(new DetailButtonDef("装備", () => EquipWeapon(invItem)));

                    var eatBtn = ItemActionHelper.BuildEatWeaponButton(
                        invItem, () => EatWeaponFromInventory(invItem));
                    if (eatBtn != null) list.Add(eatBtn);
                    break;
                }
            case ItemCategory.Magic:
                break;
        }

        // 捨てるボタン（cannotDiscard チェック込み）
        list.Add(ItemActionHelper.BuildDiscardButton(
            invItem, () => DiscardFromInventory(invItem)));

        // 預けるボタン
        bool canDeposit = StorageManager.Instance != null && !StorageManager.Instance.IsFull;
        list.Add(new DetailButtonDef("預ける", () => DepositItem(invItem), canDeposit));
    }

    private void BuildStorageButtons(InventoryItem invItem, List<DetailButtonDef> list)
    {
        // 倉庫画面は常に非バトル (inBattle = false)
        if (invItem.data.category == ItemCategory.Consumable)
        {
            var btn = ItemActionHelper.BuildUseConsumableButton(
                invItem, inBattle: false, () => UseConsumableFromStorage(invItem));
            if (btn != null) list.Add(btn);
        }

        if (invItem.data.category == ItemCategory.Weapon)
        {
            var eatBtn = ItemActionHelper.BuildEatWeaponButton(
                invItem, () => EatWeaponFromStorage(invItem));
            if (eatBtn != null) list.Add(eatBtn);
        }

        // 捨てるボタン（cannotDiscard チェック込み）
        list.Add(ItemActionHelper.BuildDiscardButton(
            invItem, () => DiscardFromStorage(invItem)));

        // 引き出すボタン
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
        int cap = (ItemBoxManager.Instance != null) ? ItemBoxManager.Instance.Capacity : inventorySlots.Length;

        for (int i = 0; i < inventorySlots.Length; i++)
        {
            if (inventorySlots[i] == null) continue;

            if (i >= cap)
            {
                inventorySlots[i].gameObject.SetActive(false);
                continue;
            }

            inventorySlots[i].gameObject.SetActive(true);
            InventoryItem invItem = (items != null && i < items.Count) ? items[i] : null;
            inventorySlots[i].SetItem(invItem);
        }
    }

    /// <summary>
    /// 倉庫側スロットをアイテム数に合わせて動的に生成/削除する。
    /// アイテムがある分だけスロットを表示し、空スロットは作らない。
    /// </summary>
    private void RefreshStorageSide()
    {
        if (slotPrefab == null || storageContent == null) return;

        IReadOnlyList<InventoryItem> items = StorageManager.Instance?.GetItems();
        int itemCount = (items != null) ? items.Count : 0;

        // 足りないスロットを追加
        while (storageSlotList.Count < itemCount)
        {
            ItemSlotView slot = Instantiate(slotPrefab, storageContent);
            slot.gameObject.name = $"StorageSlot_{storageSlotList.Count}";
            slot.onClicked = OnStorageSlotClicked;
            storageSlotList.Add(slot);
        }

        // 余分なスロットを削除
        while (storageSlotList.Count > itemCount)
        {
            int last = storageSlotList.Count - 1;
            Destroy(storageSlotList[last].gameObject);
            storageSlotList.RemoveAt(last);
        }

        // アイテムをスロットにセット
        for (int i = 0; i < itemCount; i++)
        {
            storageSlotList[i].SetItem(items[i]);
        }
    }

    // =========================================================
    // Operations
    // =========================================================

    private void UseConsumableFromInventory(InventoryItem invItem)
    {
        ItemActionHelper.ApplyConsumableEffects(invItem);
        ItemData transformInto = invItem.data?.transformInto;
        ItemBoxManager.Instance?.RemoveItem(invItem);

        if (transformInto != null && ItemBoxManager.Instance != null)
        {
            ItemBoxManager.Instance.AddItem(transformInto);
            Debug.Log($"[Storage] アイテム変化（所持品）: → {transformInto.itemName}");
        }

        AfterAction();
    }

    private void UseConsumableFromStorage(InventoryItem invItem)
    {
        ItemActionHelper.ApplyConsumableEffects(invItem);
        ItemData transformInto = invItem.data?.transformInto;
        StorageManager.Instance?.RemoveItem(invItem);

        if (transformInto != null && StorageManager.Instance != null)
        {
            StorageManager.Instance.AddItem(transformInto);
            Debug.Log($"[Storage] アイテム変化（倉庫）: → {transformInto.itemName}");
        }

        AfterAction();
    }

    // =========================================================
    // 武器を食べる
    // =========================================================

    private void EatWeaponFromInventory(InventoryItem invItem)
    {
        if (invItem?.data == null || !invItem.data.isEdible) return;

        ItemActionHelper.UnequipIfNeeded(invItem);
        ItemActionHelper.ApplyEatWeaponEffects(invItem);

        ItemData transformInto = invItem.data.transformInto;
        ItemBoxManager.Instance?.RemoveItem(invItem);

        if (transformInto != null && ItemBoxManager.Instance != null)
        {
            ItemBoxManager.Instance.AddItem(transformInto);
        }

        AfterAction();
    }

    private void EatWeaponFromStorage(InventoryItem invItem)
    {
        if (invItem?.data == null || !invItem.data.isEdible) return;

        // 倉庫内の武器は装備中にならないため UnequipIfNeeded は不要
        ItemActionHelper.ApplyEatWeaponEffects(invItem);

        ItemData transformInto = invItem.data.transformInto;
        StorageManager.Instance?.RemoveItem(invItem);

        if (transformInto != null && StorageManager.Instance != null)
        {
            StorageManager.Instance.AddItem(transformInto);
        }

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
        SaveManager.Save(); // 操作結果を即時セーブ
    }
}