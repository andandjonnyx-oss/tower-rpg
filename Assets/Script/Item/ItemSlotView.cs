using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// アイテムスロットUI（全シーン共通）。
/// クリック時に登録されたコールバックを呼ぶだけ。
/// </summary>
public class ItemSlotView : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private Image frameImage;
    [SerializeField] private Image iconImage;

    [Header("Equipped Tint")]
    [SerializeField] private Color equippedColor = new Color(0.4f, 0.8f, 1f, 1f);

    private InventoryItem currentInvItem;

    /// <summary>
    /// クリック時に呼ばれるコールバック。各シーンのコンテキストが設定する。
    /// </summary>
    public System.Action<ItemSlotView, InventoryItem> onClicked;

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

        bool isEquipped = GameState.I != null
            && GameState.I.equippedWeaponUid == currentInvItem.uid;

        iconImage.color = isEquipped ? equippedColor : Color.white;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        onClicked?.Invoke(this, currentInvItem);
    }
}