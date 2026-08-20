using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// アイテム図鑑の分類データベース（専用SO）。
/// 大ジャンル(消費/武器/魔導書/パッシブ)→小ジャンル(回復/攻撃 等)→アイテム の入れ子構造。
///
/// どのアイテムをどの大ジャンル・小ジャンルに入れるか、表示順はすべて
/// このSOのインスペクターで手動設定する。ItemData.category には依存しない。
///
/// 構造:
///   ItemZukanCategoryDatabase
///     └ majorCategories[4]  … 大ジャンル（消費/武器/魔導書/パッシブ）
///         ├ majorName        … 大ジャンル表示名（ボタン用）
///         └ subCategories[]  … 小ジャンル
///             ├ headerText   … 見出し帯に出すテキスト（例:「回復アイテム」）
///             └ items[]      … この小ジャンルに属する ItemData（並び順=表示順）
/// </summary>
[CreateAssetMenu(menuName = "Items/Item Zukan Category Database")]
public class ItemZukanCategoryDatabase : ScriptableObject
{
    [Tooltip("大ジャンル一覧（消費/武器/魔導書/パッシブの4つを想定）。\n"
           + "ジャンルボタンの並び順と対応させる。")]
    public List<MajorCategory> majorCategories = new List<MajorCategory>();

    /// <summary>大ジャンル（消費/武器/魔導書/パッシブ）。</summary>
    [System.Serializable]
    public class MajorCategory
    {
        [Tooltip("大ジャンルの表示名（管理用・任意）。例: 消費アイテム")]
        public string majorName;

        [Tooltip("この大ジャンルに含まれる小ジャンル一覧（並び順=表示順）。")]
        public List<SubCategory> subCategories = new List<SubCategory>();
    }

    /// <summary>小ジャンル（見出し＋アイテム群）。</summary>
    [System.Serializable]
    public class SubCategory
    {
        [Tooltip("見出し帯に表示するテキスト。例: 回復アイテム")]
        public string headerText;

        [Tooltip("この小ジャンルに属するアイテム（並び順がそのまま表示順）。")]
        public List<ItemData> items = new List<ItemData>();
    }

    // =========================================================
    // ヘルパー
    // =========================================================

    /// <summary>
    /// 指定した大ジャンルインデックスの全アイテムを、
    /// 小ジャンル順→アイテム順に1列に並べて返す（↑↓移動用）。
    /// 発見済みフィルタはここでは行わない（呼び出し側で行う）。
    /// </summary>
    public List<ItemData> GetFlatItems(int majorIndex)
    {
        var result = new List<ItemData>();
        if (majorIndex < 0 || majorIndex >= majorCategories.Count) return result;

        foreach (var sub in majorCategories[majorIndex].subCategories)
        {
            if (sub == null || sub.items == null) continue;
            foreach (var item in sub.items)
            {
                if (item != null) result.Add(item);
            }
        }
        return result;
    }
}