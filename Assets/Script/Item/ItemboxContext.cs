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
///
/// ボタン構築と効果適用は ItemActionHelper を経由し、
/// StorageContext と仕様を統一する。
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
                {
                    var btn = ItemActionHelper.BuildUseConsumableButton(
                        invItem, inBattle, () => UseConsumable(invItem));
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
                        invItem, () => EatWeapon(invItem));
                    if (eatBtn != null) list.Add(eatBtn);
                    break;
                }

            case ItemCategory.Magic:
                // Magic にはボタンなし
                break;
        }

        // バトル中は捨てる不可
        if (!inBattle)
        {
            list.Add(ItemActionHelper.BuildDiscardButton(
                invItem, () => DiscardItem(invItem)));
        }

        return list;
    }

    public void RefreshSlots()
    {
        if (slots == null) return;
        IReadOnlyList<InventoryItem> items = ItemBoxManager.Instance != null
            ? ItemBoxManager.Instance.GetItems() : null;
        int cap = (ItemBoxManager.Instance != null) ? ItemBoxManager.Instance.Capacity : slots.Length;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null) continue;

            if (i >= cap)
            {
                // 容量外のスロットは非表示
                slots[i].gameObject.SetActive(false);
                continue;
            }

            slots[i].gameObject.SetActive(true);
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

        // RemoveItem 後は invItem.data が参照できなくなる可能性があるため、
        // 必要な値を事前に取得しておく
        string itemName = invItem.data.itemName;
        int healAmount = invItem.data.healAmount;
        bool isBossFeedItem = invItem.data.bossFeedItem;
        int spGain = invItem.data.statusPointGain;
        ItemData transformInto = invItem.data.transformInto;
        int transformChanceValue = invItem.data.transformChance;

        // 攻撃アイテム: ダメージ情報の事前取得
        int battleDmg = invItem.data.battleDamage;
        WeaponAttribute battleAttr = invItem.data.battleAttribute;
        DamageCategory battleDmgCat = invItem.data.battleDamageCategory;

        // ヘルパー経由で効果適用（HP/MP/状態異常/SP すべて含む）
        ItemActionHelper.ApplyConsumableEffects(invItem);

        // 攻撃アイテム: ダメージ情報を GameState に一時保存
        if (battleDmg > 0 && inBattle && GameState.I != null)
        {
            GameState.I.pendingBattleItemDamage = battleDmg;
            GameState.I.pendingBattleItemAttribute = (int)battleAttr;
            GameState.I.pendingBattleItemDamageCategory = (int)battleDmgCat;
            GameState.I.pendingBattleItemName = itemName;
            Debug.Log($"[Itembox] 攻撃アイテム使用: {itemName} dmg={battleDmg} attr={battleAttr} cat={battleDmgCat}");
        }

        // ボス餌付けアイテム: 即勝利フラグを GameState に保存
        if (isBossFeedItem && inBattle
            && BattleContext.EnemyMonster != null
            && BattleContext.EnemyMonster.acceptsFeedItem
            && GameState.I != null)
        {
            GameState.I.pendingBattleItemInstantWin = true;
            GameState.I.pendingBattleItemName = itemName;
            Debug.Log($"[Itembox] ボス餌付けアイテム使用: {itemName} → 即勝利フラグON");
        }

        // 元アイテムを消す
        ItemBoxManager.Instance?.RemoveItem(invItem);

        // 使用後にアイテム変化（確率判定対応）
        bool transformed = false;
        if (transformInto != null && ItemBoxManager.Instance != null)
        {
            // transformChance が 0 なら常に変化（従来互換）
            // 1以上なら確率判定
            bool success = (transformChanceValue <= 0)
                || Random.Range(1, 101) <= transformChanceValue;

            if (success)
            {
                ItemBoxManager.Instance.AddItem(transformInto);
                Debug.Log($"[Itembox] アイテム変化: {itemName} → {transformInto.itemName}");
                transformed = true;
            }
            else
            {
                Debug.Log($"[Itembox] アイテム変化失敗: {itemName}（確率{transformChanceValue}%）");
            }
        }

        // ログメッセージの組み立て
        string logMsg = $"You は {itemName} を使った！";
        if (spGain > 0)
        {
            logMsg += $" ステータスポイント +{spGain}！";
        }
        if (transformed)
        {
            logMsg += $" {transformInto.itemName} を手に入れた！";
        }
        else if (transformInto != null)
        {
            // はずれ（transformInto が設定されていたが確率で失敗）
            logMsg += " …はずれ！";
        }

        AfterAction(logMsg);
    }

    // =========================================================
    // 武器を食べる
    // =========================================================

    private void EatWeapon(InventoryItem invItem)
    {
        if (invItem?.data == null) return;
        if (!invItem.data.isEdible) return;

        // 事前取得（RemoveItem 後に参照できなくなるため）
        string itemName = invItem.data.itemName;
        int healAmount = invItem.data.eatHealAmount;
        ItemData transformInto = invItem.data.transformInto;
        int transformChanceValue = invItem.data.transformChance;

        // ヘルパー経由で装備解除 + 効果適用
        ItemActionHelper.UnequipIfNeeded(invItem);
        ItemActionHelper.ApplyEatWeaponEffects(invItem);

        // 元アイテムを消す
        ItemBoxManager.Instance?.RemoveItem(invItem);

        // 変化先アイテムを追加（確率判定対応）
        bool transformed = false;
        if (transformInto != null && ItemBoxManager.Instance != null)
        {
            bool success = (transformChanceValue <= 0)
                || Random.Range(1, 101) <= transformChanceValue;

            if (success)
            {
                ItemBoxManager.Instance.AddItem(transformInto);
                Debug.Log($"[Itembox] 食べて変化: {itemName} → {transformInto.itemName}");
                transformed = true;
            }
            else
            {
                Debug.Log($"[Itembox] 食べて変化失敗: {itemName}（確率{transformChanceValue}%）");
            }
        }

        // ログ
        string logMsg = $"You は {itemName} を食べた！";
        if (healAmount > 0) logMsg += $" HP が {healAmount} 回復した！";
        if (transformed) logMsg += $" {transformInto.itemName} を手に入れた！";
        else if (transformInto != null) logMsg += " …はずれ！";
        AfterAction(logMsg);
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
        else
        {
            // 非バトル時: アイテム使用・装備変更の結果を即時セーブ
            SaveManager.Save();
        }
    }
}