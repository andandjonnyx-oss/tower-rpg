using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// アイテム図鑑のアイコンセル（1個分）。
/// ItemZukanView が小ジャンルごとの GridLayoutGroup 配下に Prefab から動的生成する。
/// MonsterIconCell と同じ設計。
///
/// 構造:
///   ItemIconCell (Button + Image + TMP_Text)
///     ├─ iconImage   … アイテム画像 or 非表示
///     ├─ nameText    … アイテム名 or 「???」
///     └─ unknownText … 未発見時の「？」
///
/// 未発見時: アイコン非表示、名前「???」、「？」表示、ボタン無効
/// 発見済み: アイコン表示、名前表示、タップでコールバック
/// </summary>
public class ItemIconCell : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("アイテム画像表示用 Image")]
    [SerializeField] private Image iconImage;

    [Tooltip("アイテム名表示用 TMP_Text")]
    [SerializeField] private TMP_Text nameText;

    [Tooltip("未発見時に表示する「？」テキスト（アイコンの上に重ねて配置）")]
    [SerializeField] private TMP_Text unknownText;

    [Tooltip("セル全体の Button コンポーネント")]
    [SerializeField] private Button cellButton;

    // 内部状態
    private ItemData item;
    private Action<ItemData> onClickCallback;

    /// <summary>
    /// セルを初期化する。
    /// </summary>
    /// <param name="data">アイテムデータ</param>
    /// <param name="discovered">発見済みかどうか</param>
    /// <param name="onClick">タップ時コールバック（発見済みのみ発火）</param>
    public void Setup(ItemData data, bool discovered, Action<ItemData> onClick)
    {
        item = data;
        onClickCallback = onClick;

        if (discovered)
        {
            // 発見済み: アイコンと名前を表示
            if (iconImage != null)
            {
                iconImage.enabled = true;
                iconImage.sprite = data.icon;
                iconImage.preserveAspect = true;
            }
            if (nameText != null) nameText.text = data.itemName;
            if (unknownText != null) unknownText.gameObject.SetActive(false);
            if (cellButton != null)
            {
                cellButton.interactable = true;
                cellButton.onClick.RemoveAllListeners();
                cellButton.onClick.AddListener(() => onClickCallback?.Invoke(item));
            }
        }
        else
        {
            // 未発見: 「？」表示、タップ無効
            if (iconImage != null) iconImage.enabled = false;
            if (nameText != null) nameText.text = "???";
            if (unknownText != null)
            {
                unknownText.gameObject.SetActive(true);
                unknownText.text = "？";
            }
            if (cellButton != null) cellButton.interactable = false;
        }
    }

    /// <summary>このセルが指定アイテムを表示しているかどうか（スクロール復元用）。</summary>
    public bool RepresentsItem(ItemData target)
    {
        return item != null && item == target;
    }
}