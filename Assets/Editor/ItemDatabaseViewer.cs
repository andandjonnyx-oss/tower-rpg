using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class ItemDatabaseViewer : EditorWindow
{
    private ItemDatabase targetDatabase;
    private Vector2 scrollPos;
    private string searchText = "";

    [MenuItem("Tools/Item Database Viewer")]
    public static void Open()
    {
        GetWindow<ItemDatabaseViewer>("Item Viewer");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Item Database Viewer", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        targetDatabase = (ItemDatabase)EditorGUILayout.ObjectField(
            "Target Database",
            targetDatabase,
            typeof(ItemDatabase),
            false
        );

        searchText = EditorGUILayout.TextField("Search", searchText);

        EditorGUILayout.Space();

        if (targetDatabase == null)
        {
            EditorGUILayout.HelpBox("ItemDatabase を指定してください。", MessageType.Info);
            return;
        }

        // =========================================================
        // 自動登録ボタン
        // =========================================================
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Itemlist フォルダから自動登録", GUILayout.Height(28)))
        {
            AutoRegisterFromItemlistFolder();
        }

        if (GUILayout.Button("ID でソート", GUILayout.Height(28)))
        {
            SortByItemId();
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        DrawHeader();

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        foreach (var item in targetDatabase.items)
        {
            if (item == null) continue;
            if (!IsMatch(item, searchText)) continue;

            DrawRow(item);
        }

        EditorGUILayout.EndScrollView();
    }

    // =========================================================
    // Itemlist フォルダ内の全 ItemData を自動登録
    // Assets/ScriptableAsset/Itemlist/ 以下を再帰検索し、
    // まだ items リストに入っていない ItemData を追加する。
    // 追加後は自動でIDソートも実行する。
    // =========================================================
    private void AutoRegisterFromItemlistFolder()
    {
        // Itemlist フォルダ内の全 ItemData アセットを検索
        string[] guids = AssetDatabase.FindAssets("t:ItemData", new[] { "Assets/ScriptableAsset/Itemlist" });

        if (guids.Length == 0)
        {
            EditorUtility.DisplayDialog(
                "自動登録",
                "Assets/ScriptableAsset/Itemlist/ 内に ItemData が見つかりませんでした。",
                "OK"
            );
            return;
        }

        // 既存アイテムのセットを作成（重複チェック用）
        var existingSet = new HashSet<ItemData>(targetDatabase.items.Where(i => i != null));

        int addedCount = 0;
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var itemData = AssetDatabase.LoadAssetAtPath<ItemData>(path);
            if (itemData == null) continue;

            if (!existingSet.Contains(itemData))
            {
                targetDatabase.items.Add(itemData);
                existingSet.Add(itemData);
                addedCount++;
            }
        }

        // null エントリがあれば除去
        targetDatabase.items.RemoveAll(i => i == null);

        // ソート実行
        SortByItemIdInternal();

        // 索引を再構築
        targetDatabase.InvalidateIndex();

        // 変更を保存
        EditorUtility.SetDirty(targetDatabase);
        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog(
            "自動登録完了",
            $"検出: {guids.Length} 件\n新規追加: {addedCount} 件\n合計: {targetDatabase.items.Count} 件",
            "OK"
        );
    }

    // =========================================================
    // ID でソート（ボタン用）
    // =========================================================
    private void SortByItemId()
    {
        // null エントリがあれば除去
        targetDatabase.items.RemoveAll(i => i == null);

        SortByItemIdInternal();

        targetDatabase.InvalidateIndex();
        EditorUtility.SetDirty(targetDatabase);
        AssetDatabase.SaveAssets();
    }

    // =========================================================
    // ソート内部処理
    // ID のプレフィックス文字（C, M, W 等）で分類し、
    // 同じプレフィックス内では番号順にソートする。
    // =========================================================
    private void SortByItemIdInternal()
    {
        targetDatabase.items.Sort((a, b) =>
        {
            string idA = a.itemId ?? "";
            string idB = b.itemId ?? "";

            // プレフィックス文字を取得（C, M, W 等）
            char prefixA = idA.Length > 0 ? char.ToUpper(idA[0]) : ' ';
            char prefixB = idB.Length > 0 ? char.ToUpper(idB[0]) : ' ';

            // カテゴリ順: C → M → W → その他
            int orderA = GetCategoryOrder(prefixA);
            int orderB = GetCategoryOrder(prefixB);

            if (orderA != orderB) return orderA.CompareTo(orderB);

            // 同じカテゴリ内では文字列比較（番号順になる）
            return string.Compare(idA, idB, System.StringComparison.OrdinalIgnoreCase);
        });
    }

    /// <summary>
    /// カテゴリプレフィックスのソート順を返す。
    /// C(Consumable)=0, M(Magic)=1, W(Weapon)=2, その他=3
    /// </summary>
    private int GetCategoryOrder(char prefix)
    {
        switch (prefix)
        {
            case 'C': return 0;
            case 'M': return 1;
            case 'W': return 2;
            default: return 3;
        }
    }

    private void DrawHeader()
    {
        EditorGUILayout.BeginHorizontal("box");
        GUILayout.Label("ID", EditorStyles.boldLabel, GUILayout.Width(140));
        GUILayout.Label("名前", EditorStyles.boldLabel, GUILayout.Width(160));
        GUILayout.Label("カテゴリ", EditorStyles.boldLabel, GUILayout.Width(100));
        GUILayout.Label("出現範囲", EditorStyles.boldLabel, GUILayout.Width(260));
        GUILayout.FlexibleSpace();
        GUILayout.Label("詳細", EditorStyles.boldLabel, GUILayout.Width(60));
        EditorGUILayout.EndHorizontal();
    }

    private void DrawRow(ItemData item)
    {
        EditorGUILayout.BeginHorizontal("box");

        GUILayout.Label(item.itemId, GUILayout.Width(140));
        GUILayout.Label(item.itemName, GUILayout.Width(160));
        GUILayout.Label(item.category.ToString(), GUILayout.Width(100));
        GUILayout.Label(FormatRange(item), GUILayout.Width(260));

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("詳細", GUILayout.Width(60)))
        {
            ItemDetailWindow.Open(item);
        }

        EditorGUILayout.EndHorizontal();
    }

    private string FormatRange(ItemData item)
    {
        return $"{item.Minfloor}F {item.Minstep}STEP ～ {item.Maxfloor}F {item.Maxstep}STEP";
    }

    private bool IsMatch(ItemData item, string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword)) return true;

        keyword = keyword.ToLower();

        return (!string.IsNullOrEmpty(item.itemId) && item.itemId.ToLower().Contains(keyword))
            || (!string.IsNullOrEmpty(item.itemName) && item.itemName.ToLower().Contains(keyword))
            || item.category.ToString().ToLower().Contains(keyword);
    }
}