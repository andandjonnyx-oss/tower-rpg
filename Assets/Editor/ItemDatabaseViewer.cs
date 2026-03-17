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