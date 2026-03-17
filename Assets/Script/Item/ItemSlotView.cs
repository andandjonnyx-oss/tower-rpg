using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ItemSlotView : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private Image frameImage;
    [SerializeField] private Image iconImage;

    [Header("Equipped Tint")]
    [SerializeField] private Color equippedColor = new Color(0.4f, 0.8f, 1f, 1f);

    private ItemData currentItem;
    private int slotIndex = -1;  
    public int SlotIndex => slotIndex;
    private ItemBoxView owner;


    public void Setup(ItemBoxView ownerView) => owner = ownerView;


    public void SetItem(ItemData item, int index)
    {
        currentItem = item;
        slotIndex = index;

        if (frameImage != null)
            frameImage.enabled = (item != null);

        if (iconImage == null) return;

        if (item != null && item.icon != null)
        {
            iconImage.sprite = item.icon;
            iconImage.enabled = true;
        }
        else
        {
            iconImage.sprite = null;
            iconImage.enabled = false;
        }

        RefreshEquipColor();
    }

    /// <summary>
    /// GameState のインスタンスIDと自分のIDを比較して色を更新する。
    /// スロットのGOインスタンスIDで判定するので同名武器が複数あっても1つだけ光る。
    /// </summary>
    public void RefreshEquipColor()
    {
        if (iconImage == null) return;
        if (currentItem == null || slotIndex < 0)
        {
            iconImage.color = Color.white;
            return;
        }
        bool isEquipped = false;
        if (GameState.I != null && GameState.I.equippedWeapon == currentItem)
        {
            // 同名武器が複数ある場合、最初に見つかったインデックスのスロットだけ光る
            var mgr = ItemBoxManager.Instance;
            if (mgr != null)
            {
                var allItems = mgr.GetItems();
                int equippedIndex = -1;
                for (int i = 0; i < allItems.Count; i++)
                {
                    if (allItems[i] == GameState.I.equippedWeapon) { equippedIndex = i; break; }
                }
                isEquipped = (equippedIndex == slotIndex);
            }
        }
        iconImage.color = isEquipped ? equippedColor : Color.white;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (owner == null) return;
        owner.OnClickSlot(this, currentItem);
    }
}