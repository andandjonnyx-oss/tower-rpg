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
                // =========================================================
                // battleOnly チェック（追加）
                // battleOnly == true のアイテムは非バトル時に「使う」を表示しない
                // =========================================================
                if (invItem.data.battleOnly && !inBattle)
                {
                    // 戦闘中のみ使用可能 → 非バトル時はボタンなし
                }
                else
                {
                    list.Add(new DetailButtonDef("使う", () => UseConsumable(invItem)));
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
                // isEdible == true の場合、「食べる」ボタンを追加
                // =========================================================
                if (invItem.data.isEdible)
                {
                    list.Add(new DetailButtonDef("食べる", () => EatWeapon(invItem)));
                }
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

        // RemoveItem 後は invItem.data が参照できなくなる可能性があるため、
        // 必要な値を事前に取得しておく
        string itemName = invItem.data.itemName;
        int healAmount = invItem.data.healAmount;
        int mpHeal = invItem.data.mpHealAmount;

        bool curesPoison = invItem.data.curesPoison;
        bool curesParalyze = invItem.data.curesParalyze;
        bool curesBlind = invItem.data.curesBlind;
        bool curesSilence = invItem.data.curesSilence;

        int spGain = invItem.data.statusPointGain;
        ItemData transformInto = invItem.data.transformInto;

        // =========================================================
        // 攻撃アイテム: ダメージ情報の事前取得（追加）
        // =========================================================
        int battleDmg = invItem.data.battleDamage;
        WeaponAttribute battleAttr = invItem.data.battleAttribute;
        DamageCategory battleDmgCat = invItem.data.battleDamageCategory;

        // 回復効果の適用
        if (healAmount > 0 && GameState.I != null)
        {
            GameState.I.currentHp += healAmount;
            if (GameState.I.currentHp > GameState.I.maxHp)
                GameState.I.currentHp = GameState.I.maxHp;
        }

        if (mpHeal > 0 && GameState.I != null)
        {
            GameState.I.currentMp += mpHeal;
            if (GameState.I.currentMp > GameState.I.maxMp)
                GameState.I.currentMp = GameState.I.maxMp;
        }

        // =========================================================
        // 状態異常回復の適用（追加）
        // =========================================================
        if (curesPoison && GameState.I != null)
        {
            StatusEffectSystem.CurePlayerPoison();
        }
        if (curesParalyze && GameState.I != null)
        {
            StatusEffectSystem.CurePlayer(StatusEffect.Paralyze);
        }
        if (curesBlind && GameState.I != null)
        {
            StatusEffectSystem.CurePlayer(StatusEffect.Blind);
        }
        if (curesSilence && GameState.I != null)
        {
            StatusEffectSystem.CurePlayer(StatusEffect.Silence);
        }


        // =========================================================
        // ステータスポイント付与効果の適用（追加）
        // =========================================================
        if (spGain > 0 && GameState.I != null)
        {
            GameState.I.statusPoint += spGain;
            Debug.Log($"[Itembox] ステータスポイント +{spGain} (合計: {GameState.I.statusPoint})");
        }

        // =========================================================
        // 攻撃アイテム: ダメージ情報を GameState に一時保存（追加）
        // BattleSceneController 復帰時にダメージ計算を実行する。
        // =========================================================
        if (battleDmg > 0 && inBattle && GameState.I != null)
        {
            GameState.I.pendingBattleItemDamage = battleDmg;
            GameState.I.pendingBattleItemAttribute = (int)battleAttr;
            GameState.I.pendingBattleItemDamageCategory = (int)battleDmgCat;
            GameState.I.pendingBattleItemName = itemName;
            Debug.Log($"[Itembox] 攻撃アイテム使用: {itemName} dmg={battleDmg} attr={battleAttr} cat={battleDmgCat}");
        }

        // 元アイテムを消す
        ItemBoxManager.Instance?.RemoveItem(invItem);

        // =========================================================
        // 使用後にアイテム変化（追加）
        // 先に元アイテムを消してから追加するので枠は±0
        // =========================================================
        if (transformInto != null && ItemBoxManager.Instance != null)
        {
            ItemBoxManager.Instance.AddItem(transformInto);
            Debug.Log($"[Itembox] アイテム変化: {itemName} → {transformInto.itemName}");
        }

        // ログメッセージの組み立て
        string logMsg = $"You は {itemName} を使った！";
        if (spGain > 0)
        {
            logMsg += $" ステータスポイント +{spGain}！";
        }
        if (transformInto != null)
        {
            logMsg += $" {transformInto.itemName} を手に入れた！";
        }

        AfterAction(logMsg);
    }

    // =========================================================
    // 武器を食べる（追加）
    // =========================================================
    //
    // isEdible == true の武器を消費する。
    // 装備中の場合は自動的に外してから消費する。
    // 回復効果を適用し、transformInto があれば変化先を追加する。
    // バトル中は1ターン消費する。
    // =========================================================

    private void EatWeapon(InventoryItem invItem)
    {
        if (invItem?.data == null) return;
        if (!invItem.data.isEdible) return;

        // 事前取得（RemoveItem 後に参照できなくなるため）
        string itemName = invItem.data.itemName;
        int healAmount = invItem.data.eatHealAmount;
        bool curesPoison = invItem.data.eatCuresPoison;
        bool eatCuresParalyze = invItem.data.eatCuresParalyze;
        bool eatCuresBlind = invItem.data.eatCuresBlind;
        bool eatCuresSilence = invItem.data.eatCuresSilence;
        ItemData transformInto = invItem.data.transformInto;

        // 装備中なら外す
        if (GameState.I != null && GameState.I.equippedWeaponUid == invItem.uid)
        {
            GameState.I.equippedWeaponUid = "";
            Debug.Log($"[Itembox] 食べる前に装備を外した: {itemName}");
        }

        // 回復効果の適用
        if (healAmount > 0 && GameState.I != null)
        {
            GameState.I.currentHp += healAmount;
            if (GameState.I.currentHp > GameState.I.maxHp)
                GameState.I.currentHp = GameState.I.maxHp;
        }

        // 毒消し
        if (curesPoison && GameState.I != null)
        {
            StatusEffectSystem.CurePlayerPoison();
        }
        if (eatCuresParalyze && GameState.I != null)
        {
            StatusEffectSystem.CurePlayer(StatusEffect.Paralyze);
        }

        if (eatCuresBlind && GameState.I != null)
        {
            StatusEffectSystem.CurePlayer(StatusEffect.Blind);
        }

        if (eatCuresSilence && GameState.I != null)
        {
            StatusEffectSystem.CurePlayer(StatusEffect.Silence);
        }

        // 元アイテムを消す
        ItemBoxManager.Instance?.RemoveItem(invItem);

        // 変化先アイテムを追加
        if (transformInto != null && ItemBoxManager.Instance != null)
        {
            ItemBoxManager.Instance.AddItem(transformInto);
            Debug.Log($"[Itembox] 食べて変化: {itemName} → {transformInto.itemName}");
        }

        // ログ
        string logMsg = $"You は {itemName} を食べた！";
        if (healAmount > 0) logMsg += $" HP が {healAmount} 回復した！";
        if (transformInto != null) logMsg += $" {transformInto.itemName} を手に入れた！";

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