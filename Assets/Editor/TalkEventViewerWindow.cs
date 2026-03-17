#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public class TalkEventViewerWindow : EditorWindow
{
    // ここを自分のTalkEvent置き場に合わせて変えてOK（空ならプロジェクト全体検索）
    private string folder = "Assets/ScriptableAsset/Talklist";
    private string idQuery = "";
    private int floorFilter = -1; // -1なら無効
    private int stepFilter = -1; // -1なら無効

    // ★追加：反映先Database
    private TalkEventDatabase targetDatabase;
    private bool applyFilteredResults = true; // trueならフィルタ後結果を反映、falseならフォルダ全件を反映

    private Vector2 scroll;
    private List<TalkEvent> cached = new();

    [MenuItem("Tools/Talk Event Viewer")]
    public static void Open()
    {
        GetWindow<TalkEventViewerWindow>("Talk Event Viewer").Refresh();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Search Scope", EditorStyles.boldLabel);
        folder = EditorGUILayout.TextField("Folder (Assets/...)", folder);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Filters", EditorStyles.boldLabel);
        idQuery = EditorGUILayout.TextField("ID contains", idQuery);

        using (new EditorGUILayout.HorizontalScope())
        {
            floorFilter = EditorGUILayout.IntField("Floor (-1=any)", floorFilter);
            stepFilter = EditorGUILayout.IntField("Step  (-1=any)", stepFilter);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Refresh", GUILayout.Height(24))) Refresh();
            if (GUILayout.Button("Clear Filters", GUILayout.Height(24)))
            {
                idQuery = "";
                floorFilter = -1;
                stepFilter = -1;
            }
        }

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Database Sync", EditorStyles.boldLabel);

        targetDatabase = (TalkEventDatabase)EditorGUILayout.ObjectField(
            "Target Database",
            targetDatabase,
            typeof(TalkEventDatabase),
            false
        );

        applyFilteredResults = EditorGUILayout.ToggleLeft(
            "Apply filtered results (unchecked = apply ALL cached results from folder)",
            applyFilteredResults
        );

        using (new EditorGUILayout.HorizontalScope())
        {
            GUI.enabled = targetDatabase != null;

            if (GUILayout.Button("Apply To Database", GUILayout.Height(26)))
            {
                var listToApply = applyFilteredResults ? GetFilteredList() : GetAllCachedSorted();
                ApplyToDatabase(targetDatabase, listToApply);
            }

            GUI.enabled = true;

            if (GUILayout.Button("Ping Database", GUILayout.Height(26)) && targetDatabase != null)
                EditorGUIUtility.PingObject(targetDatabase);
        }

        EditorGUILayout.Space(6);

        // ID重複チェック（簡易）
        var dupIds = cached
            .Where(e => e != null && !string.IsNullOrEmpty(e.id))
            .GroupBy(e => e.id)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (dupIds.Count > 0)
        {
            EditorGUILayout.HelpBox($"Duplicate IDs found: {string.Join(", ", dupIds)}", MessageType.Warning);
        }

        // 表示リスト
        var list = GetFilteredList();

        EditorGUILayout.LabelField($"Results: {list.Count} / {cached.Count}", EditorStyles.miniBoldLabel);

        // ヘッダ
        using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
        {
            GUILayout.Label("ID", GUILayout.MinWidth(220));
            GUILayout.Label("F", GUILayout.Width(30));
            GUILayout.Label("S", GUILayout.Width(30));
            GUILayout.Label("Lines", GUILayout.Width(45));
            GUILayout.FlexibleSpace();
            GUILayout.Label("Actions", GUILayout.Width(180));
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);
        foreach (var e in list)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                GUILayout.Label(e.id ?? "(no id)", GUILayout.MinWidth(220));
                GUILayout.Label(e.floor.ToString(), GUILayout.Width(30));
                GUILayout.Label(e.step.ToString(), GUILayout.Width(30));
                GUILayout.Label((e.lines?.Count ?? 0).ToString(), GUILayout.Width(45));

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Ping", GUILayout.Width(55)))
                    EditorGUIUtility.PingObject(e);

                if (GUILayout.Button("Select", GUILayout.Width(55)))
                    Selection.activeObject = e;

                if (GUILayout.Button("Open", GUILayout.Width(55)))
                    AssetDatabase.OpenAsset(e);
            }
        }
        EditorGUILayout.EndScrollView();
    }

    private List<TalkEvent> GetAllCachedSorted()
    {
        return cached
            .Where(e => e != null)
            .OrderBy(e => e.floor)
            .ThenBy(e => e.step)
            .ThenBy(e => e.id)
            .ToList();
    }

    private List<TalkEvent> GetFilteredList()
    {
        var view = cached.Where(e => e != null);

        if (!string.IsNullOrEmpty(idQuery))
            view = view.Where(e => (e.id ?? "").ToLower().Contains(idQuery.ToLower()));

        if (floorFilter >= 0)
            view = view.Where(e => e.floor == floorFilter);

        if (stepFilter >= 0)
            view = view.Where(e => e.step == stepFilter);

        return view
            .OrderBy(e => e.floor)
            .ThenBy(e => e.step)
            .ThenBy(e => e.id)
            .ToList();
    }

    private void ApplyToDatabase(TalkEventDatabase db, List<TalkEvent> listToApply)
    {
        if (db == null) return;

        // ID重複チェック（反映前に止めたい場合はここでreturnしてもOK）
        var dup = listToApply
            .Where(e => e != null && !string.IsNullOrEmpty(e.id))
            .GroupBy(e => e.id)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (dup.Count > 0)
        {
            Debug.LogWarning($"[TalkEventViewer] Duplicate IDs in applied list: {string.Join(", ", dup)}");
        }

        Undo.RecordObject(db, "Apply TalkEvents To Database");
        db.events = listToApply;

        // ★重要：Database内部のキャッシュ(index/byId)を無効化（次回BuildIndexIfNeededで作り直させる）
        InvalidateDatabaseCache(db);

        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[TalkEventViewer] Applied {listToApply.Count} TalkEvent(s) to database: {db.name}");
    }

    private void InvalidateDatabaseCache(TalkEventDatabase db)
    {
        // TalkEventDatabase.cs の private フィールド：index / byId を null にする
        // （BuildIndexIfNeeded() が「既に辞書があるならreturn」なので、ここを消さないと更新後も古い辞書を使う可能性がある）
        var t = db.GetType();
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;

        var fIndex = t.GetField("index", flags);
        var fById = t.GetField("byId", flags);

        if (fIndex != null) fIndex.SetValue(db, null);
        if (fById != null) fById.SetValue(db, null);
    }

    private void Refresh()
    {
        string[] searchIn = string.IsNullOrWhiteSpace(folder)
            ? null
            : new[] { folder.Trim() };

        var guids = (searchIn == null)
            ? AssetDatabase.FindAssets("t:TalkEvent")
            : AssetDatabase.FindAssets("t:TalkEvent", searchIn);

        cached = guids
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(p => AssetDatabase.LoadAssetAtPath<TalkEvent>(p))
            .Where(a => a != null)
            .ToList();

        Repaint();
    }
}
#endif