using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// アイテムスロットUI（全シーン共通）。
/// クリック時に登録されたコールバックを呼ぶだけ。
/// </summary>
public class ItemSlotView : MonoBehaviour, IPointerClickHandler, ISubmitHandler
{
    /// <summary>ナビゲーション対象にするための Selectable（Awake で用意）。</summary>
    private Selectable selectable;

    private void Awake()
    {
        // コントローラー対応: 十字キーのナビゲーション対象にするため Selectable を
        // 実行時付与する（プレハブ改修不要）。決定ボタンは OnSubmit で受ける。
        selectable = GetComponent<Selectable>();
        if (selectable == null)
        {
            selectable = gameObject.AddComponent<Selectable>();
            selectable.transition = Selectable.Transition.None; // 見た目は SelectionHighlighter の枠に任せる
        }
    }

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

        // コントローラー対応: 空スロットはナビゲーション対象にしない
        //（空アイコンにフォーカスが乗る問題の対策）。中身ありは Automatic に戻す。
        //   StorageContext 等が後段でさらに Explicit 配線で上書きすることがある。
        if (selectable != null)
        {
            var nav = selectable.navigation;
            nav.mode = (invItem != null) ? Navigation.Mode.Automatic : Navigation.Mode.None;
            selectable.navigation = nav;
        }

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

    /// <summary>コントローラー/キーボードの決定ボタン。タップと同じ処理を呼ぶ。</summary>
    public void OnSubmit(BaseEventData eventData)
    {
        onClicked?.Invoke(this, currentInvItem);
    }
}