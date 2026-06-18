using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// GP交換ショップのグリッドセルPrefab用スクリプト。
/// アイコンと名前を表示し、タップで GpShopView にコールバックする。
///
/// Prefab 構成例:
///   GpShopCell (Button)
///     ├ IconImage (Image)
///     ├ NameText (TMP_Text)
///     └ CostText (TMP_Text)  ← GP価格表示（任意）
/// </summary>
public class GpShopCell : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text costText;

    private GpShopData shopData;

    // プレハブで設定された元の文字色を保持する（白で上書きしないため）
    private Color defaultNameColor = Color.black;
    private Color defaultCostColor = Color.black;
    private bool colorsInitialized = false;

    /// <summary>
    /// セルがタップされた時のコールバック。
    /// GpShopView が設定する。
    /// </summary>
    public System.Action<GpShopData> onClicked;

    private void CacheDefaultColors()
    {
        if (colorsInitialized) return;
        if (nameText != null) defaultNameColor = nameText.color;
        if (costText != null) defaultCostColor = costText.color;
        colorsInitialized = true;
    }

    /// <summary>
    /// セルに商品データをセットして表示を更新する。
    /// </summary>
    public void Setup(GpShopData data)
    {
        CacheDefaultColors();

        shopData = data;

        if (data == null || data.item == null)
        {
            if (iconImage != null) iconImage.enabled = false;
            if (nameText != null) nameText.text = "";
            if (costText != null) costText.text = "";
            return;
        }

        // アイコン
        if (iconImage != null)
        {
            iconImage.sprite = data.item.icon;
            iconImage.enabled = data.item.icon != null;
        }

        // 名前
        if (nameText != null)
            nameText.text = data.item.itemName;

        // GP価格
        if (costText != null)
            costText.text = $"{data.gpCost}GP";
    }

    /// <summary>
    /// GP不足やアイテム枠満杯の場合にセルの表示を暗くする。
    /// </summary>
    public void SetInteractable(bool interactable)
    {
        CacheDefaultColors();

        // アイコンの色をグレーアウトで表現
        if (iconImage != null)
            iconImage.color = interactable ? Color.white : new Color(0.4f, 0.4f, 0.4f, 1f);

        // 文字色：有効時はプレハブの元色を維持、無効時のみグレーアウト
        if (nameText != null)
            nameText.color = interactable ? defaultNameColor : new Color(0.5f, 0.5f, 0.5f, 1f);

        if (costText != null)
            costText.color = interactable ? defaultCostColor : new Color(0.5f, 0.5f, 0.5f, 1f);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (shopData != null)
            onClicked?.Invoke(shopData);
    }
}