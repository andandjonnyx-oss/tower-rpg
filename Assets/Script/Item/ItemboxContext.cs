using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Itembox シーン用コントローラー。
/// 所持品の一覧を表示し、使う/装備/捨てるの操作を提供する。
/// </summary>
public class ItemboxContext : MonoBehaviour, IItemContext
{
    [Header("Slots")]
    [SerializeField] private ItemSlotView[] slots;

    [Header("Detail Panel")]
    [SerializeField] private ItemDetailPanel detailPanel;

    [Header("Navigation")]
    [SerializeField] private Button backButton;
    [SerializeField] private string mainSceneName = "Main";

    private void Start()
    {
        // スロットにコールバック登録
        if (slots != null)
        {
            foreach (var slot in slots)
            {
                if (slot != null)
                    slot.onClicked = OnSlotClicked;
            }
        }

        if (backButton != null)
            backButton.onClick.AddListener(() => SceneManager.LoadScene(mainSceneName));

        if (detailPanel != null) detailPanel.Hide();
        RefreshSlots();
    }

    private void OnSlotClicked(ItemSlotView slot, InventoryItem invItem)
    {
        if (detailPanel == null) return;

        if (invItem == null)
        {
            detailPanel.Hide();
            return;
        }

        detailPanel.Show(invItem, this, fromInventory: true);
    }

    // =========================================================
    // IItemContext
    // =========================================================
    public List<DetailButtonDef> GetButtons(InventoryItem invItem, bool fromInventory)
    {
        var list = new List<DetailButtonDef>();
        if (invItem?.data == null) return list;

        switch (invItem.data.category)
        {
            case ItemCategory.Consumable:
                list.Add(new DetailButtonDef("使う", () => UseConsumable(invItem)));
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
                // Magic にはボタン1なし
                break;
        }

        list.Add(new DetailButtonDef("捨てる", () => DiscardItem(invItem)));

        return list;
    }

    public void RefreshSlots()
    {
        if (slots == null) return;
        IReadOnlyList<InventoryItem> items = ItemBoxManager.Instance != null
            ? ItemBoxManager.Instance.GetItems() : null;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null) continue;
            InventoryItem invItem = (items != null && i < items.Count) ? items[i] : null;
            slots[i].SetItem(invItem);
        }
    }

    // =========================================================
    // Operations
    // =========================================================
    private void UseConsumable(InventoryItem invItem)
    {
        // TODO: 回復効果など
        ItemBoxManager.Instance?.RemoveItem(invItem);
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

    private void DiscardItem(InventoryItem invItem)
    {
        ItemBoxManager.Instance?.DiscardItem(invItem);
        AfterAction();
    }

    private void AfterAction()
    {
        if (detailPanel != null) detailPanel.Hide();
        RefreshSlots();
    }
}