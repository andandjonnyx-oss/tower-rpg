using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ItemDetailPanel : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject windowRoot;

    [Header("UI")]
    [SerializeField] private TMP_Text itemNameText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private Image itemImage;
    [SerializeField] private Button button1;
    [SerializeField] private Button button2;
    [SerializeField] private TMP_Text button1Text;
    [SerializeField] private TMP_Text button2Text;

    private ItemBoxView ownerView;
    private InventoryItem currentInvItem;

    private void Awake()
    {
        // ウィンドウは非表示から開始（windowRoot だけを閉じる）
        if (windowRoot != null) windowRoot.SetActive(false);

        // ボタンリスナーはここで一度だけ登録する
        if (button1 != null) button1.onClick.AddListener(OnButton1Clicked);
        if (button2 != null) button2.onClick.AddListener(OnButton2Clicked);
    }

    public void Show(InventoryItem invItem, ItemBoxView view)
    {
        ownerView = view;
        currentInvItem = invItem;

        if (invItem?.data == null) { HideImmediate(); return; }

        var data = invItem.data;
        if (itemNameText != null) itemNameText.text = data.itemName;
        if (descriptionText != null) descriptionText.text = data.description;
        if (itemImage != null)
        {
            itemImage.sprite = data.icon;
            itemImage.enabled = data.icon != null;
        }

        ApplyButtonLabels(invItem);

        if (windowRoot != null) windowRoot.SetActive(true);
        else gameObject.SetActive(true);
    }

    // UI を閉じるだけ。ownerView や currentInvItem はクリアしない。
    public void HideImmediate()
    {
        if (windowRoot != null) windowRoot.SetActive(false);
        else gameObject.SetActive(false);
    }

    // -----------------------------------------------------------------

    private void OnButton1Clicked()
    {
        if (currentInvItem?.data == null) return;

        switch (currentInvItem.data.category)
        {
            case ItemCategory.Consumable:
                UseConsumable();
                break;
            case ItemCategory.Weapon:
                bool isEquipped = GameState.I != null
                    && GameState.I.equippedWeaponUid == currentInvItem.uid;
                if (isEquipped) UnequipWeapon();
                else EquipWeapon();
                break;
        }
    }

    private void OnButton2Clicked()
    {
        if (currentInvItem != null) DiscardItem();
    }

    // -----------------------------------------------------------------

    private void UseConsumable()
    {
        // TODO: 回復などの効果はここに実装する
        ItemBoxManager.Instance?.RemoveItem(currentInvItem);
        HideImmediate();
        ownerView?.RefreshView();
    }

    private void EquipWeapon()
    {
        ItemBoxManager.Instance?.EquipItem(currentInvItem);
        HideImmediate();
        ownerView?.RefreshView();
    }

    private void UnequipWeapon()
    {
        ItemBoxManager.Instance?.UnequipItem(currentInvItem);
        HideImmediate();
        ownerView?.RefreshView();
    }

    private void DiscardItem()
    {
        ItemBoxManager.Instance?.DiscardItem(currentInvItem);
        HideImmediate();
        ownerView?.RefreshView();
    }

    // -----------------------------------------------------------------

    private void ApplyButtonLabels(InventoryItem invItem)
    {
        if (button1 != null) button1.gameObject.SetActive(true);
        if (button2 != null) button2.gameObject.SetActive(true);

        switch (invItem.data.category)
        {
            case ItemCategory.Consumable:
                if (button1Text != null) button1Text.text = "使う";
                if (button2Text != null) button2Text.text = "捨てる";
                break;

            case ItemCategory.Weapon:
                bool isEquipped = GameState.I != null
                    && GameState.I.equippedWeaponUid == invItem.uid;
                if (button1Text != null) button1Text.text = isEquipped ? "外す" : "装備";
                if (button2Text != null) button2Text.text = "捨てる";
                break;

            case ItemCategory.Magic:
                if (button1 != null) button1.gameObject.SetActive(false);
                if (button2Text != null) button2Text.text = "捨てる";
                break;
        }
    }
}