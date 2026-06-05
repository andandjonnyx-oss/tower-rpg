using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 分類CSVを読んで ItemZukanCategoryDatabase（SO）に流し込む Editor 拡張。
/// メニュー Tools → Item → Import Item Zukan CSV から実行。
///
/// CSV列: 大ジャンル, 小ジャンル, 並び順, itemId, itemName, category, Minfloor, Maxfloor
///   - 大ジャンル/小ジャンルの表示順はこのスクリプト内の MAJOR_ORDER / SUB_ORDER で制御
///   - 各小ジャンル内は「並び順」列の昇順でソート（空欄は最後）
///   - itemId から ItemData を ItemDatabase 経由で解決
///
/// 既存の majorCategories は上書き再構築される（インポートし直しても重複しない）。
/// </summary>
public static class ItemZukanCsvImporter
{
    // ---- 表示順の定義（ここを編集すれば順番を変えられる） ----
    private static readonly string[] MAJOR_ORDER = { "消費", "武器", "魔導書", "パッシブ" };

    // 大ジャンルごとの小ジャンル表示順
    private static readonly Dictionary<string, string[]> SUB_ORDER = new Dictionary<string, string[]>
    {
        { "消費",     new[] { "回復アイテム", "攻撃アイテム", "その他アイテム" } },
        { "武器",     new[] { "武器" } },
        { "魔導書",   new[] { "回復魔法", "攻撃魔法", "状態異常魔法", "バフ・デバフ" } },
        { "パッシブ", new[] { "パッシブ" } },
    };

    [MenuItem("Tools/Item/Import Item Zukan CSV")]
    public static void Import()
    {
        // 対象SOを選択（選択中を優先、なければ検索）
        ItemZukanCategoryDatabase db = Selection.activeObject as ItemZukanCategoryDatabase;
        if (db == null) db = FindFirstAsset<ItemZukanCategoryDatabase>("t:ItemZukanCategoryDatabase");
        if (db == null)
        {
            EditorUtility.DisplayDialog("インポート失敗",
                "ItemZukanCategoryDatabase が見つかりません。\n" +
                "Project で対象SOを選択してから実行してください。", "OK");
            return;
        }

        // ItemData 解決用に ItemDatabase を取得
        ItemDatabase itemDb = FindFirstAsset<ItemDatabase>("t:ItemDatabase");
        if (itemDb == null || itemDb.items == null || itemDb.items.Count == 0)
        {
            EditorUtility.DisplayDialog("インポート失敗",
                "ItemDatabase が見つからない、または items が空です。", "OK");
            return;
        }
        // itemId -> ItemData の辞書を作る
        var itemById = new Dictionary<string, ItemData>();
        foreach (var it in itemDb.items)
        {
            if (it != null && !string.IsNullOrEmpty(it.itemId) && !itemById.ContainsKey(it.itemId))
                itemById[it.itemId] = it;
        }

        // CSVファイルを選ぶ
        string path = EditorUtility.OpenFilePanel("分類CSVを選択", Application.dataPath, "csv");
        if (string.IsNullOrEmpty(path)) return;

        // 読み込み（UTF-8 BOM対応）
        string[] lines = File.ReadAllLines(path, System.Text.Encoding.UTF8);
        if (lines.Length < 2)
        {
            EditorUtility.DisplayDialog("インポート失敗", "CSVに行がありません。", "OK");
            return;
        }

        // パース（1行目はヘッダーとして読み飛ばし）
        var records = new List<Record>();
        var missingIds = new List<string>();
        var unknownCats = new HashSet<string>();

        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;

            var cols = ParseCsvLine(line);
            if (cols.Count < 4) continue;

            string major = cols[0].Trim();
            string sub = cols[1].Trim();
            string orderStr = cols[2].Trim();
            string itemId = cols[3].Trim();

            if (string.IsNullOrEmpty(itemId)) continue;

            int order = int.MaxValue; // 空欄は最後
            int parsed;
            if (int.TryParse(orderStr, out parsed)) order = parsed;

            // 順序定義に無い大/小ジャンルは警告対象
            if (!MAJOR_ORDER.Contains(major)) unknownCats.Add($"大ジャンル「{major}」");
            else if (SUB_ORDER.ContainsKey(major) && !SUB_ORDER[major].Contains(sub))
                unknownCats.Add($"小ジャンル「{major} / {sub}」");

            ItemData data;
            if (!itemById.TryGetValue(itemId, out data))
            {
                missingIds.Add(itemId);
                continue;
            }

            records.Add(new Record { major = major, sub = sub, order = order, item = data });
        }

        // 順序定義に無い分類があれば中断（表記ゆれの検出）
        if (unknownCats.Count > 0)
        {
            EditorUtility.DisplayDialog("インポート中断",
                "順序定義（MAJOR_ORDER / SUB_ORDER）に無い分類があります。\n" +
                "表記を確認してください:\n\n" + string.Join("\n", unknownCats.OrderBy(x => x)),
                "OK");
            return;
        }

        // SOを再構築
        db.majorCategories = new List<ItemZukanCategoryDatabase.MajorCategory>();

        foreach (string majorName in MAJOR_ORDER)
        {
            var major = new ItemZukanCategoryDatabase.MajorCategory { majorName = majorName };

            string[] subOrder = SUB_ORDER.ContainsKey(majorName)
                ? SUB_ORDER[majorName]
                : records.Where(r => r.major == majorName).Select(r => r.sub).Distinct().ToArray();

            foreach (string subName in subOrder)
            {
                var subRecords = records
                    .Where(r => r.major == majorName && r.sub == subName)
                    .OrderBy(r => r.order)
                    .ToList();

                if (subRecords.Count == 0) continue;

                var sub = new ItemZukanCategoryDatabase.SubCategory
                {
                    headerText = subName,
                    items = subRecords.Select(r => r.item).ToList()
                };
                major.subCategories.Add(sub);
            }

            db.majorCategories.Add(major);
        }

        // 保存
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();

        // 結果レポート
        int total = records.Count;
        string msg = $"インポート完了\n\n登録: {total}件";
        if (missingIds.Count > 0)
            msg += $"\n\n⚠ ItemDatabaseに無いID（スキップ）: {missingIds.Count}件\n" +
                   string.Join("\n", missingIds.Take(15)) +
                   (missingIds.Count > 15 ? "\n…" : "");

        Debug.Log($"[ItemZukanCsvImporter] 登録{total}件 / 欠番{missingIds.Count}件");
        EditorUtility.DisplayDialog("インポート完了", msg, "OK");
    }

    private struct Record
    {
        public string major;
        public string sub;
        public int order;
        public ItemData item;
    }

    /// <summary>CSV1行をパース（ダブルクオート囲み・エスケープ対応）。</summary>
    private static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        var sb = new System.Text.StringBuilder();
        bool inQuotes = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                    else inQuotes = false;
                }
                else sb.Append(c);
            }
            else
            {
                if (c == '"') inQuotes = true;
                else if (c == ',') { result.Add(sb.ToString()); sb.Clear(); }
                else sb.Append(c);
            }
        }
        result.Add(sb.ToString());
        return result;
    }

    private static T FindFirstAsset<T>(string filter) where T : Object
    {
        string[] guids = AssetDatabase.FindAssets(filter);
        if (guids.Length == 0) return null;
        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        return AssetDatabase.LoadAssetAtPath<T>(path);
    }
}