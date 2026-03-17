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

    private ItemData currentItem;

    private void Awake()
    {
        HideImmediate();
    }

    public void Show(ItemData item)
    {
        currentItem = item;

        if (item == null)
        {
            HideImmediate();
            return;
        }

        if (itemNameText != null)
            itemNameText.text = item.itemName;

        if (descriptionText != null)
            descriptionText.text = item.description;

        if (itemImage != null)
        {
            itemImage.sprite = item.icon;
            itemImage.enabled = item.icon != null;
        }

        ApplyButtonLabels(item);

        if (windowRoot != null)
            windowRoot.SetActive(true);
        else
            gameObject.SetActive(true);
    }

    public void HideImmediate()
    {
        currentItem = null;

        if (windowRoot != null)
            windowRoot.SetActive(false);
        else
            gameObject.SetActive(false);
    }

    public ItemData GetCurrentItem()
    {
        return currentItem;
    }

    private void ApplyButtonLabels(ItemData item)
    {
        if (button1 != null)
            button1.gameObject.SetActive(true);

        if (button2 != null)
            button2.gameObject.SetActive(true);

        switch (item.category)
        {
            case ItemCategory.Consumable:
                if (button1Text != null) button1Text.text = "Žg‚¤";
                if (button2Text != null) button2Text.text = "ŽÌ‚Ä‚é";
                break;

            case ItemCategory.Weapon:
                if (button1Text != null) button1Text.text = "‘•”õ";
                if (button2Text != null) button2Text.text = "ŽÌ‚Ä‚é";
                break;

            case ItemCategory.Magic:
                if (button1 != null) button1.gameObject.SetActive(false);
                if (button2Text != null) button2Text.text = "ŽÌ‚Ä‚é";
                break;
        }
    }
}