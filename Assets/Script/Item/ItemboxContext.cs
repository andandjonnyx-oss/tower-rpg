using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Itembox シーン用コントローラー。
/// 通常時: 使う/装備/捨てる。戻るでMainへ。
/// バトル中 (GameState.isInBattle): 使う/装備変更のみ。捨てる不可。
///   アイテム操作後は自動でバトルシーンへ戻り1ターン消費。
///   戻るボタンはターン消費なしでバトルシーンへ。
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

    /// <summary>バトル中かどうかのキャッシュ。</summary>
    private bool inBattle;

    private void Start()
    {
        inBattle = GameState.I != null && GameState.I.isInBattle;

        // スロットにコールバック登録
        if (slots != null)
        {
            foreach (var slot in slots)
            {
                if (slot != null)
                    slot.onClicked = OnSlotClicked;
            }
        }

        // 戻るボタン
        if (backButton != null)
            backButton.onClick.AddListener(OnBackClicked);

        if (detailPanel != null) detailPanel.Hide();
        RefreshSlots();
    }

    private void OnBackClicked()
    {
        if (inBattle)
        {
            // ターン消費なしでバトルへ戻る
            if (GameState.I != null)
            {
                GameState.I.battleTurnConsumed = false;
                GameState.I.isInBattle = false;
            }
            SceneManager.LoadScene(GameState.I?.previousSceneName ?? "Battle");
        }
        else
        {
            SceneManager.LoadScene(mainSceneName);
        }
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
                // Magic にはボタンなし
                break;
        }

        // バトル中は捨てる不可
        if (!inBattle)
        {
            list.Add(new DetailButtonDef("捨てる", () => DiscardItem(invItem)));
        }

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
        if (invItem?.data == null) return;

        // 回復効果の適用
        if (invItem.data.healAmount > 0 && GameState.I != null)
        {
            GameState.I.currentHp += invItem.data.healAmount;
            if (GameState.I.currentHp > GameState.I.maxHp)
                GameState.I.currentHp = GameState.I.maxHp;
        }

        // =========================================================
        // 毒消し効果の適用（追加）
        // =========================================================
        if (invItem.data.curesPoison && GameState.I != null)
        {
            StatusEffectSystem.CurePlayerPoison();
        }

        string itemName = invItem.data.itemName;
        ItemBoxManager.Instance?.RemoveItem(invItem);
        AfterAction($"You は {itemName} を使った！");
    }

    private void EquipWeapon(InventoryItem invItem)
    {
        ItemBoxManager.Instance?.EquipItem(invItem);
        AfterAction($"You は {invItem.data.itemName} を装備した！");
    }

    private void UnequipWeapon(InventoryItem invItem)
    {
        ItemBoxManager.Instance?.UnequipItem(invItem);
        AfterAction($"You は {invItem.data.itemName} を外した！");
    }

    private void DiscardItem(InventoryItem invItem)
    {
        ItemBoxManager.Instance?.DiscardItem(invItem);
        AfterAction("");
    }

    private void AfterAction(string logMessage)
    {
        if (detailPanel != null) detailPanel.Hide();
        RefreshSlots();

        // バトル中なら即座にバトルシーンへ戻る（1ターン消費）
        if (inBattle && GameState.I != null)
        {
            GameState.I.battleTurnConsumed = true;
            GameState.I.battleItemActionLog = logMessage;
            GameState.I.isInBattle = false;
            SceneManager.LoadScene(GameState.I.previousSceneName ?? "Battle");
        }
    }
}