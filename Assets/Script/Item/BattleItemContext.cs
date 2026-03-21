using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 戦闘中アイテム使用パネル用コントローラー。
/// 消耗品 → 「使う」（1ターン消費）
/// 武器   → 「装備」or「外す」（1ターン消費）
/// 戻るボタン → ターン消費なし
/// </summary>
public class BattleItemContext : MonoBehaviour, IItemContext
{
    [Header("Slots")]
    [SerializeField] private ItemSlotView[] slots;

    [Header("Detail Panel")]
    [SerializeField] private ItemDetailPanel detailPanel;

    /// <summary>
    /// アイテム使用 or 装備変更で1ターン消費するときのコールバック。
    /// 引数の InventoryItem は使用/装備変更したアイテム。
    /// </summary>
    public System.Action<InventoryItem> onTurnConsumed;

    /// <summary>
    /// 戻る（キャンセル）時のコールバック。ターン消費なし。
    /// </summary>
    public System.Action onCancelled;

    private void Start()
    {
        if (slots != null)
            foreach (var s in slots)
                if (s != null) s.onClicked = OnSlotClicked;

        if (detailPanel != null) detailPanel.Hide();
        RefreshSlots();
    }

    private void OnSlotClicked(ItemSlotView slot, InventoryItem invItem)
    {
        if (invItem == null) { detailPanel?.Hide(); return; }
        detailPanel?.Show(invItem, this, fromInventory: true);
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
                list.Add(new DetailButtonDef("使う", () => UseInBattle(invItem)));
                break;

            case ItemCategory.Weapon:
                bool equipped = GameState.I != null
                    && GameState.I.equippedWeaponUid == invItem.uid;
                if (equipped)
                    list.Add(new DetailButtonDef("外す", () => UnequipInBattle(invItem)));
                else
                    list.Add(new DetailButtonDef("装備", () => EquipInBattle(invItem)));
                break;

            case ItemCategory.Magic:
                // 戦闘中のMagicカテゴリは操作なし
                break;
        }

        list.Add(new DetailButtonDef("やめる", () => Cancel()));

        return list;
    }

    public void RefreshSlots()
    {
        if (slots == null) return;
        IReadOnlyList<InventoryItem> items = ItemBoxManager.Instance?.GetItems();
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null) continue;
            InventoryItem invItem = (items != null && i < items.Count) ? items[i] : null;
            slots[i].SetItem(invItem);
        }
    }

    // =========================================================
    // Operations（すべて1ターン消費）
    // =========================================================

    private void UseInBattle(InventoryItem invItem)
    {
        if (invItem?.data == null) return;

        // 回復効果の適用
        if (invItem.data.healAmount > 0 && GameState.I != null)
        {
            GameState.I.currentHp += invItem.data.healAmount;
            if (GameState.I.currentHp > GameState.I.maxHp)
                GameState.I.currentHp = GameState.I.maxHp;
        }

        ItemBoxManager.Instance?.RemoveItem(invItem);
        detailPanel?.Hide();
        RefreshSlots();
        onTurnConsumed?.Invoke(invItem);
    }

    private void EquipInBattle(InventoryItem invItem)
    {
        if (invItem?.data == null) return;
        ItemBoxManager.Instance?.EquipItem(invItem);
        detailPanel?.Hide();
        RefreshSlots();
        onTurnConsumed?.Invoke(invItem);
    }

    private void UnequipInBattle(InventoryItem invItem)
    {
        if (invItem?.data == null) return;
        ItemBoxManager.Instance?.UnequipItem(invItem);
        detailPanel?.Hide();
        RefreshSlots();
        onTurnConsumed?.Invoke(invItem);
    }

    private void Cancel()
    {
        detailPanel?.Hide();
        onCancelled?.Invoke();
    }
}