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
    private ItemData currentItem;
    private int currentSlotInstanceId;

    private void Awake()
    {
        HideImmediate();
        // onClick はここで一度だけ登録する
        if (button1 != null) button1.onClick.AddListener(OnButton1Clicked);
        if (button2 != null) button2.onClick.AddListener(OnButton2Clicked);
    }

    /// <summary>
    /// ItemBoxView から呼ぶ。スロットのインスタンスIDも一緒に受け取る。
    /// </summary>
    public void Show(ItemData item, int slotInstanceId, ItemBoxView view)
    {
        ownerView = view;
        currentItem = item;
        currentSlotInstanceId = slotInstanceId;

        if (item == null) { HideImmediate(); return; }

        if (itemNameText != null) itemNameText.text = item.itemName;
        if (descriptionText != null) descriptionText.text = item.description;
        if (itemImage != null)
        {
            itemImage.sprite = item.icon;
            itemImage.enabled = item.icon != null;
        }

        ApplyButtonLabels(item, slotInstanceId);

        if (windowRoot != null) windowRoot.SetActive(true);
        else gameObject.SetActive(true);
    }

    public void HideImmediate()
    {
        currentItem = null;
        ownerView = null;
        currentSlotInstanceId = -1;

        if (windowRoot != null) windowRoot.SetActive(false);
        else gameObject.SetActive(false);
    }

    public ItemData GetCurrentItem() => currentItem;

    // -----------------------------------------------------------------
    // ボタン処理
    // -----------------------------------------------------------------

    private void OnButton1Clicked()
    {
        if (currentItem == null) return;
        switch (currentItem.category)
        {
            case ItemCategory.Consumable:
                UseConsumable(currentItem);
                break;
            case ItemCategory.Weapon:
                bool isEquipped = GameState.I != null
                    && GameState.I.equippedWeaponInstanceId == currentSlotInstanceId;
                if (isEquipped) UnequipWeapon();
                else EquipWeapon(currentItem);
                break;
        }
    }

    private void OnButton2Clicked()
    {
        if (currentItem != null) DiscardItem(currentItem);
    }

    // -----------------------------------------------------------------

    private void UseConsumable(ItemData item)
    {
        // TODO: 回復などの効果はここに実装する
        ItemBoxManager.Instance?.RemoveItem(item);
        HideImmediate();
        ownerView?.RefreshView();
    }

    private void EquipWeapon(ItemData item)
    {
        ItemBoxManager.Instance?.EquipItem(item, currentSlotInstanceId);
        HideImmediate();
        ownerView?.RefreshView();
    }

    private void UnequipWeapon()
    {
        ItemBoxManager.Instance?.UnequipItem(currentSlotInstanceId);
        HideImmediate();
        ownerView?.RefreshView();
    }

    private void DiscardItem(ItemData item)
    {
        ItemBoxManager.Instance?.DiscardItem(item, currentSlotInstanceId);
        HideImmediate();
        ownerView?.RefreshView();
    }

    // -----------------------------------------------------------------

    private void ApplyButtonLabels(ItemData item, int slotInstanceId)
    {
        if (button1 != null) button1.gameObject.SetActive(true);
        if (button2 != null) button2.gameObject.SetActive(true);

        switch (item.category)
        {
            case ItemCategory.Consumable:
                if (button1Text != null) button1Text.text = "使う";
                if (button2Text != null) button2Text.text = "捨てる";
                break;

            case ItemCategory.Weapon:
                bool isEquipped = GameState.I != null
                    && GameState.I.equippedWeaponInstanceId == slotInstanceId;
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