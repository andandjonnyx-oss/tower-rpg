using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Itemsouko（倉庫）シーン用コントローラー。
/// 所持品と倉庫の2列を表示し、使う/装備/捨てる/預ける/引き出すの操作を提供する。
/// 倉庫側スロットはアイテム数に応じて Prefab から動的に生成される。
/// </summary>
public class StorageContext : MonoBehaviour, IItemContext
{
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
                // =========================================================
                // battleOnly チェック（追加）
                // 倉庫画面は常に非バトルなので、battleOnly アイテムは「使う」を表示しない
                // =========================================================
                if (!invItem.data.battleOnly)
                {
                    list.Add(new DetailButtonDef("使う", () => UseConsumableFromInventory(invItem)));
                }
                break;
            case ItemCategory.Weapon:
                bool equipped = GameState.I != null
                    && GameState.I.equippedWeaponUid == invItem.uid;
                if (equipped)
                    list.Add(new DetailButtonDef("外す", () => UnequipWeapon(invItem)));
                else
                    list.Add(new DetailButtonDef("装備", () => EquipWeapon(invItem)));

                // =========================================================
                // 食べられる武器（追加）
                // =========================================================
                if (invItem.data.isEdible)
                {
                    list.Add(new DetailButtonDef("食べる", () => EatWeaponFromInventory(invItem)));
                }
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
        // =========================================================
        // battleOnly チェック（追加）
        // 倉庫側でも battleOnly アイテムは「使う」を表示しない
        // =========================================================
        if (invItem.data.category == ItemCategory.Consumable && !invItem.data.battleOnly)
            list.Add(new DetailButtonDef("使う", () => UseConsumableFromStorage(invItem)));

        // =========================================================
        // 倉庫側でも食べられる武器は「食べる」表示（追加）
        // =========================================================
        if (invItem.data.category == ItemCategory.Weapon && invItem.data.isEdible)
            list.Add(new DetailButtonDef("食べる", () => EatWeaponFromStorage(invItem)));

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

    /// <summary>
    /// 消費アイテムの効果を適用する共通メソッド。
    /// 所持品・倉庫どちらから使っても同じ効果を適用する。
    /// RemoveItem の前に呼ぶこと（RemoveItem 後は invItem.data が参照できない可能性があるため）。
    /// </summary>
    private void ApplyConsumableEffects(InventoryItem invItem)
    {
        if (invItem?.data == null || GameState.I == null) return;

        // HP回復
        if (invItem.data.healAmount > 0)
        {
            GameState.I.currentHp += invItem.data.healAmount;
            if (GameState.I.currentHp > GameState.I.maxHp)
                GameState.I.currentHp = GameState.I.maxHp;
            Debug.Log($"[Storage] HP回復 +{invItem.data.healAmount} (HP: {GameState.I.currentHp}/{GameState.I.maxHp})");
        }

        if (invItem.data.mpHealAmount > 0)
        {
            GameState.I.currentMp += invItem.data.mpHealAmount;
            if (GameState.I.currentMp > GameState.I.maxMp)
                GameState.I.currentMp = GameState.I.maxMp;
        }

        // 毒消し
        if (invItem.data.curesPoison)
        {
            StatusEffectSystem.CurePlayerPoison();
            Debug.Log("[Storage] 毒を回復した");
        }

        // ステータスポイント付与
        if (invItem.data.statusPointGain > 0)
        {
            GameState.I.statusPoint += invItem.data.statusPointGain;
            Debug.Log($"[Storage] ステータスポイント +{invItem.data.statusPointGain} (合計: {GameState.I.statusPoint})");
        }
    }

    private void UseConsumableFromInventory(InventoryItem invItem)
    {
        ApplyConsumableEffects(invItem);
        ItemData transformInto = invItem.data?.transformInto;
        ItemBoxManager.Instance?.RemoveItem(invItem);

        // =========================================================
        // 使用後にアイテム変化（追加）
        // =========================================================
        if (transformInto != null && ItemBoxManager.Instance != null)
        {
            ItemBoxManager.Instance.AddItem(transformInto);
            Debug.Log($"[Storage] アイテム変化（所持品）: → {transformInto.itemName}");
        }

        AfterAction();
    }

    private void UseConsumableFromStorage(InventoryItem invItem)
    {
        ApplyConsumableEffects(invItem);
        ItemData transformInto = invItem.data?.transformInto;
        StorageManager.Instance?.RemoveItem(invItem);

        // =========================================================
        // 使用後にアイテム変化（追加）
        // 倉庫から使った場合、変化先は倉庫に入る
        // =========================================================
        if (transformInto != null && StorageManager.Instance != null)
        {
            StorageManager.Instance.AddItem(transformInto);
            Debug.Log($"[Storage] アイテム変化（倉庫）: → {transformInto.itemName}");
        }

        AfterAction();
    }

    // =========================================================
    // 武器を食べる（追加）
    // =========================================================

    private void EatWeaponFromInventory(InventoryItem invItem)
    {
        if (invItem?.data == null || !invItem.data.isEdible) return;

        // 装備中なら外す
        if (GameState.I != null && GameState.I.equippedWeaponUid == invItem.uid)
        {
            GameState.I.equippedWeaponUid = "";
        }

        // 回復効果
        if (invItem.data.eatHealAmount > 0 && GameState.I != null)
        {
            GameState.I.currentHp += invItem.data.eatHealAmount;
            if (GameState.I.currentHp > GameState.I.maxHp)
                GameState.I.currentHp = GameState.I.maxHp;
        }
        if (invItem.data.eatCuresPoison && GameState.I != null)
        {
            StatusEffectSystem.CurePlayerPoison();
        }

        ItemData transformInto = invItem.data.transformInto;
        ItemBoxManager.Instance?.RemoveItem(invItem);

        // 変化先を所持品に追加
        if (transformInto != null && ItemBoxManager.Instance != null)
        {
            ItemBoxManager.Instance.AddItem(transformInto);
        }

        AfterAction();
    }

    private void EatWeaponFromStorage(InventoryItem invItem)
    {
        if (invItem?.data == null || !invItem.data.isEdible) return;

        // 回復効果
        if (invItem.data.eatHealAmount > 0 && GameState.I != null)
        {
            GameState.I.currentHp += invItem.data.eatHealAmount;
            if (GameState.I.currentHp > GameState.I.maxHp)
                GameState.I.currentHp = GameState.I.maxHp;
        }
        if (invItem.data.eatCuresPoison && GameState.I != null)
        {
            StatusEffectSystem.CurePlayerPoison();
        }

        ItemData transformInto = invItem.data.transformInto;
        StorageManager.Instance?.RemoveItem(invItem);

        // 変化先は倉庫に入る
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