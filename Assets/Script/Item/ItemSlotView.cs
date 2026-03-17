using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ItemSlotView : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private Image frameImage;
    [SerializeField] private Image iconImage;

    private ItemData currentItem;
    private ItemBoxView owner;

    public void Setup(ItemBoxView ownerView)
    {
        owner = ownerView;
    }

    public void SetItem(ItemData item)
    {
        currentItem = item;

        if (frameImage != null)
            frameImage.enabled = true;

        if (iconImage == null)
            return;

        if (item != null && item.icon != null)
        {
            iconImage.sprite = item.icon;
            iconImage.enabled = true;
            iconImage.color = Color.white;
        }
        else
        {
            iconImage.sprite = null;
            iconImage.enabled = false;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (owner == null)
            return;

        owner.OnClickSlot(this, currentItem);
    }
}