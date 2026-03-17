using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

public class MonsterDatabaseViewer : EditorWindow
{
    private MonsterDatabase targetDatabase;
    private Vector2 scrollPos;
    private string searchText = "";

    [MenuItem("Tools/Monster Database Viewer")]
    public static void Open()
    {
        GetWindow<MonsterDatabaseViewer>("Monster Viewer");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Monster Database Viewer", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        targetDatabase = (MonsterDatabase)EditorGUILayout.ObjectField(
            "Target Database",
            targetDatabase,
            typeof(MonsterDatabase),
            false
        );

        searchText = EditorGUILayout.TextField("Search", searchText);

        EditorGUILayout.Space();

        if (targetDatabase == null)
        {
            EditorGUILayout.HelpBox("MonsterDatabase を指定してください。", MessageType.Info);
            return;
        }

        DrawHeader();

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        foreach (var monster in targetDatabase.monsters)
        {
            if (monster == null) continue;
            if (!IsMatch(monster, searchText)) continue;

            DrawRow(monster);
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawHeader()
    {
        EditorGUILayout.BeginHorizontal("box");
        GUILayout.Label("ID", EditorStyles.boldLabel, GUILayout.Width(140));
        GUILayout.Label("名前", EditorStyles.boldLabel, GUILayout.Width(160));
        GUILayout.Label("出現範囲", EditorStyles.boldLabel, GUILayout.Width(260));
        GUILayout.FlexibleSpace();
        GUILayout.Label("詳細", EditorStyles.boldLabel, GUILayout.Width(60));
        EditorGUILayout.EndHorizontal();
    }

    private void DrawRow(Monster monster)
    {
        EditorGUILayout.BeginHorizontal("box");

        GUILayout.Label(monster.ID, GUILayout.Width(140));
        GUILayout.Label(monster.Mname, GUILayout.Width(160));
        GUILayout.Label(FormatRange(monster), GUILayout.Width(260));

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("詳細", GUILayout.Width(60)))
        {
            MonsterDetailWindow.Open(monster);
        }

        EditorGUILayout.EndHorizontal();
    }

    private string FormatRange(Monster monster)
    {
        return $"{monster.Minfloor}F {monster.Minstep}STEP ～ {monster.Maxfloor}F {monster.Maxstep}STEP";
    }

    private bool IsMatch(Monster monster, string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword)) return true;

        keyword = keyword.ToLower();

        return (!string.IsNullOrEmpty(monster.ID) && monster.ID.ToLower().Contains(keyword))
            || (!string.IsNullOrEmpty(monster.Mname) && monster.Mname.ToLower().Contains(keyword));
    }
}