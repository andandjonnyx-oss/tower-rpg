using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// モンスター図鑑のアイコンセル（1体分）。
/// MonsterZukanView の GridLayoutGroup 配下に Prefab から動的生成される。
///
/// 構造:
///   MonsterIconCell (Button + Image + TMP_Text)
///     ├─ iconImage  … モンスター画像 or 「？」表示
///     └─ nameText   … モンスター名 or 「???」
///
/// 未遭遇時: アイコン非表示、名前「???」、ボタン無効
/// 遭遇済み: アイコン表示、名前表示、タップでコールバック
/// </summary>
public class MonsterIconCell : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("モンスター画像表示用 Image")]
    [SerializeField] private Image iconImage;

    [Tooltip("モンスター名表示用 TMP_Text")]
    [SerializeField] private TMP_Text nameText;

    [Tooltip("未遭遇時に表示する「？」テキスト（アイコンの上に重ねて配置）")]
    [SerializeField] private TMP_Text unknownText;

    [Tooltip("セル全体の Button コンポーネント")]
    [SerializeField] private Button cellButton;

    // 内部状態
    private Monster monster;
    private Action<Monster> onClickCallback;

    /// <summary>
    /// セルを初期化する。
    /// </summary>
    /// <param name="m">モンスターデータ</param>
    /// <param name="encountered">遭遇済みかどうか</param>
    /// <param name="onClick">タップ時コールバック（遭遇済みのみ発火）</param>
    public void Setup(Monster m, bool encountered, Action<Monster> onClick)
    {
        monster = m;
        onClickCallback = onClick;

        if (encountered)
        {
            // 遭遇済み: アイコンと名前を表示
            if (iconImage != null)
            {
                iconImage.enabled = true;
                iconImage.sprite = m.Image;
                iconImage.preserveAspect = true;
            }
            if (nameText != null) nameText.text = m.Mname;
            if (unknownText != null) unknownText.gameObject.SetActive(false);
            if (cellButton != null)
            {
                cellButton.interactable = true;
                cellButton.onClick.RemoveAllListeners();
                cellButton.onClick.AddListener(() => onClickCallback?.Invoke(monster));
            }
        }
        else
        {
            // 未遭遇: 「？」表示、タップ無効
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
}