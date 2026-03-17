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
    // このスロット GameObject 自体の InstanceID を装備の識別キーにする
    private int slotInstanceId;
    private ItemBoxView owner;

    private void Awake()
    {
        slotInstanceId = gameObject.GetInstanceID();
    }

    public void Setup(ItemBoxView ownerView) => owner = ownerView;

    public int SlotInstanceId => slotInstanceId;

    public void SetItem(ItemData item)
    {
        currentItem = item;

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

        if (currentItem == null)
        {
            iconImage.color = Color.white;
            return;
        }

        bool isEquipped = GameState.I != null
            && GameState.I.equippedWeaponInstanceId == slotInstanceId;

        iconImage.color = isEquipped ? equippedColor : Color.white;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (owner == null) return;
        owner.OnClickSlot(this, currentItem);
    }
}