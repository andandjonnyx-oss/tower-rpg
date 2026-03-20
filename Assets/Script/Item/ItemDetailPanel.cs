using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// アイテム詳細パネル（全シーン共通）。
/// アイテム情報を表示し、IItemContext から受け取ったボタン定義に従ってボタンを動的に構成する。
/// </summary>
public class ItemDetailPanel : MonoBehaviour
{
    [Header("Root (SetActive で表示/非表示)")]
    [SerializeField] private GameObject detailRoot;

    [Header("Item Info")]
    [SerializeField] private TMP_Text itemNameText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private Image itemImage;

    [Header("Buttons (Inspector でアサイン、最大数分用意)")]
    [SerializeField] private Button[] buttons;
    [SerializeField] private TMP_Text[] buttonTexts;

    private void Start()
    {
    }

    private void Awake()
    {
        Hide();
    }

    /// <summary>
    /// 詳細パネルを表示する。
    /// </summary>
    public void Show(InventoryItem invItem, IItemContext context, bool fromInventory)
    {
        if (invItem?.data == null) { Hide(); return; }

        var data = invItem.data;

        // アイテム情報
        if (itemNameText != null) itemNameText.text = data.itemName;
        if (descriptionText != null) descriptionText.text = data.description;
        if (itemImage != null)
        {
            itemImage.sprite = data.icon;
            itemImage.enabled = data.icon != null;
        }

        // ボタン構成
        var buttonDefs = context.GetButtons(invItem, fromInventory);
        SetupButtons(buttonDefs);

        if (detailRoot != null) detailRoot.SetActive(true);
    }

    /// <summary>
    /// 詳細パネルを非表示にする。
    /// </summary>
    public void Hide()
    {
        if (detailRoot != null) detailRoot.SetActive(false);
    }

    /// <summary>
    /// ボタン配列を DetailButtonDef リストに合わせて設定する。
    /// </summary>
    private void SetupButtons(List<DetailButtonDef> defs)
    {
        if (buttons == null) return;

        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] == null) continue;

            if (i < defs.Count)
            {
                var def = defs[i];
                buttons[i].gameObject.SetActive(true);
                buttons[i].interactable = def.interactable;

                // ラベル設定
                if (buttonTexts != null && i < buttonTexts.Length && buttonTexts[i] != null)
                    buttonTexts[i].text = def.label;

                // リスナーをクリアして再登録
                buttons[i].onClick.RemoveAllListeners();
                // ローカル変数にキャプチャしないとクロージャで最後の値が使われる
                var action = def.onClick;
                buttons[i].onClick.AddListener(() => action?.Invoke());
            }
            else
            {
                buttons[i].gameObject.SetActive(false);
            }
        }
    }
}