using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// アイテム図鑑の詳細画面（Istatus シーン）。
/// ItemZukanContext から選択アイテムと発見済みリストを受け取り表示する。
/// ↑↓で発見済みアイテムを大ジャンル内（小ジャンル跨ぎ）で巡る。
/// Monsterstatusview と同じ設計。
///
/// シーン構成（想定）:
///   Canvas
///     └ Panel1
///         ├ itemimage (Image)
///         ├ nameText (TMP_Text)
///         ├ floorRangeText (TMP_Text)
///         ├ helpText (TMP_Text)
///         ├ modoru (戻る Button)
///         ├ ue (↑ Button)
///         └ sita (↓ Button)
/// </summary>
public class ItemStatusView : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image itemImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text floorRangeText;
    [SerializeField] private TMP_Text helpText;

    [Header("Navigation Buttons")]
    [SerializeField] private Button backButton;   // modoru
    [SerializeField] private Button upButton;     // ue
    [SerializeField] private Button downButton;   // sita

    [Tooltip("戻る先シーン名（アイテム図鑑一覧）")]
    [SerializeField] private string zukanISceneName = "ZukanI";

    // 内部状態
    private List<ItemData> list;
    private int index;

    private void Start()
    {
        list = ItemZukanContext.DiscoveredList;
        index = ItemZukanContext.CurrentIndex;

        if (backButton != null) backButton.onClick.AddListener(OnBackClicked);
        if (upButton != null) upButton.onClick.AddListener(OnUpClicked);
        if (downButton != null) downButton.onClick.AddListener(OnDownClicked);

        // 安全策: リストが無ければ単体表示
        if (list == null || list.Count == 0)
        {
            if (ItemZukanContext.SelectedItem != null)
            {
                list = new List<ItemData> { ItemZukanContext.SelectedItem };
                index = 0;
            }
        }

        if (index < 0 || index >= (list?.Count ?? 0)) index = 0;

        Refresh();
    }

    // =========================================================
    // 表示更新
    // =========================================================

    private void Refresh()
    {
        if (list == null || list.Count == 0) return;

        ItemData item = list[index];
        if (item == null) return;

        if (itemImage != null)
        {
            itemImage.sprite = item.icon;
            itemImage.enabled = item.icon != null;
            itemImage.preserveAspect = true;
        }

        if (nameText != null) nameText.text = item.itemName;

        if (floorRangeText != null)
            floorRangeText.text = BuildFloorRange(item);

        if (helpText != null) helpText.text = item.description;

        // 端で↑↓を無効化（巡回させない場合）
        if (upButton != null) upButton.interactable = (index > 0);
        if (downButton != null) downButton.interactable = (index < list.Count - 1);
    }

    /// <summary>出現フロア表示文字列を作る。Minfloor〜Maxfloor。</summary>
    private string BuildFloorRange(ItemData item)
    {
        // Minfloor / Maxfloor が両方0のアイテムはイベント入手扱い。
        if (item.Minfloor == 0 && item.Maxfloor == 0)
            return "出現: イベント";

        // Maxfloor が 0 や未設定の場合の表記はプロジェクトの慣習に合わせて調整可。
        if (item.Maxfloor > 0 && item.Maxfloor != item.Minfloor)
            return $"出現: {item.Minfloor}〜{item.Maxfloor}F";
        return $"出現: {item.Minfloor}F〜";
    }

    // =========================================================
    // ↑↓ナビゲーション
    // =========================================================

    private void OnUpClicked()
    {
        if (list == null || list.Count == 0) return;
        if (index > 0)
        {
            index--;
            Refresh();
        }
    }

    private void OnDownClicked()
    {
        if (list == null || list.Count == 0) return;
        if (index < list.Count - 1)
        {
            index++;
            Refresh();
        }
    }

    // =========================================================
    // 戻る
    // =========================================================

    private void OnBackClicked()
    {
        // 戻り時のタブ・スクロール復元情報をセット
        ItemZukanContext.ReturningFromDetail = true;
        ItemZukanContext.ReturnMajorIndex = ItemZukanContext.CurrentMajorIndex;
        ItemZukanContext.ReturnTargetItem = (list != null && index < list.Count) ? list[index] : null;

        SceneManager.LoadScene(zukanISceneName);
    }
}