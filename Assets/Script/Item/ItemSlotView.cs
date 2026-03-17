using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ItemSlotView : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private Image frameImage;
    [SerializeField] private Image iconImage;

    [Header("Equipped Tint")]
    [SerializeField] private Color equippedColor = new Color(0.4f, 0.8f, 1f, 1f);

    private InventoryItem currentInvItem;
    private ItemBoxView owner;

    public void Setup(ItemBoxView ownerView) => owner = ownerView;

    public void SetItem(InventoryItem invItem)
    {
        currentInvItem = invItem;

        if (frameImage != null)
            frameImage.enabled = (invItem != null);

        if (iconImage == null) return;

        if (invItem != null && invItem.data != null && invItem.data.icon != null)
        {
            iconImage.sprite = invItem.data.icon;
            iconImage.enabled = true;
        }
        else
        {
            iconImage.sprite = null;
            iconImage.enabled = false;
        }

        RefreshEquipColor();
    }

    public void RefreshEquipColor()
    {
        if (iconImage == null) return;

        if (currentInvItem == null)
        {
            iconImage.color = Color.white;
            return;
        }

        // uid ‚Å”»’è‚·‚é‚Ì‚ÅA“¯–¼•Ší‚ª•¡”‚ ‚Á‚Ä‚à‘•”õ’†‚Ì1ŒÂ‚¾‚¯Œõ‚é
        bool isEquipped = GameState.I != null
            && GameState.I.equippedWeaponUid == currentInvItem.uid;

        iconImage.color = isEquipped ? equippedColor : Color.white;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (owner == null) return;
        owner.OnClickSlot(this, currentInvItem);
    }
}