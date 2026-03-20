using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// 倉庫画面（Itemsouko）専用のスロットView。
/// isStorageSide で所持品側/倉庫側を区別し、クリック時に StorageView へ通知する。
/// </summary>
public class StorageSlotView : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private Image frameImage;
    [SerializeField] private Image iconImage;

    [Header("Equipped Tint")]
    [SerializeField] private Color equippedColor = new Color(0.4f, 0.8f, 1f, 1f);

    [Header("Side")]
    [Tooltip("true = 倉庫側スロット, false = 所持品側スロット")]
    [SerializeField] private bool isStorageSide = false;

    private InventoryItem currentInvItem;
    private StorageView storageView;

    public void Setup(StorageView view)
    {
        storageView = view;
        Debug.Log($"[StorageSlotView] Setup: {gameObject.name}, storageView={(view != null ? "セット済み" : "NULL")}, isStorageSide={isStorageSide}");
    }

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

        // 倉庫側は装備表示しない
        if (isStorageSide)
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
        // ★ デバッグ: クリックが届いているか確認
        Debug.Log($"[StorageSlotView] OnPointerClick: {gameObject.name}, storageView={(storageView != null ? "あり" : "NULL")}, isStorageSide={isStorageSide}, hasItem={(currentInvItem != null)}");

        if (storageView == null)
        {
            Debug.LogWarning($"[StorageSlotView] storageView が NULL のためクリック無視: {gameObject.name}");
            return;
        }

        if (isStorageSide)
            storageView.OnStorageSlotClicked(currentInvItem);
        else
            storageView.OnInventorySlotClicked(currentInvItem);
    }
}