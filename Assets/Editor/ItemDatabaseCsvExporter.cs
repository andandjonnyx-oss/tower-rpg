using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using System.IO;

/// <summary>
/// ItemDatabase 内の全アイテムを CSV に書き出す Editor 拡張。
/// メニュー Tools → Item → Export ItemDatabase to CSV から実行。
///
/// 出力列: itemId, itemName, category, Minfloor, Maxfloor
/// 文字コードは UTF-8 BOM付き（Excel で日本語が文字化けしないように）。
///
/// 後段のアイテム図鑑CSVインポーター用の台帳としても使える。
/// 出力後、大ジャンル/小ジャンル列を Excel で足して分類CSVに加工する想定。
/// </summary>
public static class ItemDatabaseCsvExporter
{
    [MenuItem("Tools/Item/Export ItemDatabase to CSV")]
    public static void ExportSelected()
    {
        // 選択中の ItemDatabase を優先。なければプロジェクト内を検索。
        ItemDatabase db = Selection.activeObject as ItemDatabase;
        if (db == null)
            db = FindFirstItemDatabase();

        if (db == null)
        {
            EditorUtility.DisplayDialog("エクスポート失敗",
                "ItemDatabase が見つかりません。\n" +
                "Project ビューで ItemDatabase アセットを選択してから実行してください。",
                "OK");
            return;
        }

        if (db.items == null || db.items.Count == 0)
        {
            EditorUtility.DisplayDialog("エクスポート失敗",
                $"'{db.name}' に items が登録されていません。", "OK");
            return;
        }

        // 保存先をダイアログで選ばせる
        string path = EditorUtility.SaveFilePanel(
            "アイテム一覧CSVの保存先",
            Application.dataPath,
            "item_list.csv",
            "csv");

        if (string.IsNullOrEmpty(path)) return; // キャンセル

        var sb = new StringBuilder();
        // ヘッダー行
        sb.AppendLine("itemId,itemName,category,Minfloor,Maxfloor");

        int count = 0;
        foreach (var item in db.items)
        {
            if (item == null) continue;

            string id = Escape(item.itemId);
            string name = Escape(item.itemName);
            string cat = CategoryToString(item.category);
            string minF = item.Minfloor.ToString();
            string maxF = item.Maxfloor.ToString();

            sb.AppendLine($"{id},{name},{cat},{minF},{maxF}");
            count++;
        }

        // UTF-8 BOM付きで書き出し（Excelで日本語が文字化けしないように）
        var encoding = new UTF8Encoding(true);
        File.WriteAllText(path, sb.ToString(), encoding);

        Debug.Log($"[ItemDatabaseCsvExporter] {count}件を書き出しました: {path}");
        EditorUtility.DisplayDialog("エクスポート完了",
            $"{count}件のアイテムを書き出しました。\n\n{path}", "OK");

        // プロジェクト内に保存された場合はインポートして見えるようにする
        if (path.StartsWith(Application.dataPath))
            AssetDatabase.Refresh();
    }

    /// <summary>category enum を読みやすい文字列に変換する。</summary>
    private static string CategoryToString(ItemCategory cat)
    {
        switch (cat)
        {
            case ItemCategory.Consumable: return "Consumable";
            case ItemCategory.Weapon: return "Weapon";
            case ItemCategory.Magic: return "Magic";
            default: return cat.ToString();
        }
    }

    /// <summary>
    /// CSV用エスケープ。値にカンマ・改行・ダブルクオートが含まれる場合は
    /// ダブルクオートで囲み、内部のダブルクオートは2重にする。
    /// </summary>
    private static string Escape(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        bool needQuote = s.Contains(",") || s.Contains("\"") || s.Contains("\n") || s.Contains("\r");
        if (s.Contains("\"")) s = s.Replace("\"", "\"\"");
        return needQuote ? $"\"{s}\"" : s;
    }

    /// <summary>プロジェクト内の最初の ItemDatabase アセットを探す。</summary>
    private static ItemDatabase FindFirstItemDatabase()
    {
        string[] guids = AssetDatabase.FindAssets("t:ItemDatabase");
        if (guids.Length == 0) return null;
        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        return AssetDatabase.LoadAssetAtPath<ItemDatabase>(path);
    }
}