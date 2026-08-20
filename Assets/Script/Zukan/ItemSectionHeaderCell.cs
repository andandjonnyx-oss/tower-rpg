using TMPro;
using UnityEngine;

/// <summary>
/// アイテム図鑑の小ジャンル見出し帯（横いっぱいの細長いフレーム＋テキスト）。
/// ItemZukanView が VerticalLayoutGroup 配下に Prefab から動的生成する。
/// 各小ジャンルの先頭に1つ置かれ、その下にアイテムの5列グリッドが続く。
///
/// 構造:
///   ItemSectionHeaderCell (Image=フレーム背景)
///     └─ headerText … 見出しテキスト（例:「回復アイテム」）
/// </summary>
public class ItemSectionHeaderCell : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("見出しテキスト表示用 TMP_Text")]
    [SerializeField] private TMP_Text headerText;

    /// <summary>
    /// 見出しテキストを設定する。
    /// </summary>
    public void Setup(string text)
    {
        if (headerText != null)
            headerText.text = text;
    }
}