using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class MonsterDatabaseViewer : EditorWindow
{
    private MonsterDatabase targetDatabase;
    private Vector2 scrollPos;
    private string searchText = "";

    // Boss フォルダから読み込んだモンスター一覧（キャッシュ）
    private List<Monster> cachedBossMonsters = new();
    private bool showBossSection = true;

    [MenuItem("Tools/Monster Database Viewer")]
    public static void Open()
    {
        GetWindow<MonsterDatabaseViewer>("Monster Viewer");
    }

    private void OnEnable()
    {
        // ウィンドウを開いた時にBossリストを読み込む
        RefreshBossList();
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

        // =========================================================
        // 自動登録・ソートボタン
        // =========================================================
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Normal フォルダから自動登録", GUILayout.Height(28)))
        {
            AutoRegisterFromNormalFolder();
        }

        if (GUILayout.Button("ID でソート", GUILayout.Height(28)))
        {
            SortByMonsterId();
        }

        if (GUILayout.Button("Boss リスト更新", GUILayout.Height(28)))
        {
            RefreshBossList();
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        // =========================================================
        // 通常モンスター一覧（データベース登録済み）
        // =========================================================
        EditorGUILayout.LabelField("■ 通常モンスター（Database 登録）", EditorStyles.boldLabel);
        DrawHeader();

        foreach (var monster in targetDatabase.monsters)
        {
            if (monster == null) continue;
            if (!IsMatch(monster, searchText)) continue;

            DrawRow(monster);
        }

        EditorGUILayout.Space();
        EditorGUILayout.Space();

        // =========================================================
        // ボスモンスター一覧（フォルダ直接読み込み）
        // =========================================================
        showBossSection = EditorGUILayout.Foldout(showBossSection, "■ ボスモンスター（Boss フォルダ）", true, EditorStyles.foldoutHeader);

        if (showBossSection)
        {
            if (cachedBossMonsters.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "Boss フォルダ内にモンスターが見つかりません。\n" +
                    "パス: Assets/ScriptableAsset/Monsterlist/Boss/",
                    MessageType.Info
                );
            }
            else
            {
                DrawBossHeader();

                foreach (var boss in cachedBossMonsters)
                {
                    if (boss == null) continue;
                    if (!IsMatch(boss, searchText)) continue;

                    DrawBossRow(boss);
                }
            }
        }

        EditorGUILayout.EndScrollView();
    }

    // =========================================================
    // Boss フォルダからモンスターを読み込み（キャッシュ更新）
    // =========================================================
    private void RefreshBossList()
    {
        cachedBossMonsters.Clear();

        string[] guids = AssetDatabase.FindAssets("t:Monster", new[] { "Assets/ScriptableAsset/Monsterlist/Boss" });

        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var monster = AssetDatabase.LoadAssetAtPath<Monster>(path);
            if (monster != null)
            {
                cachedBossMonsters.Add(monster);
            }
        }

        // ID でソート
        cachedBossMonsters.Sort((a, b) =>
        {
            string idA = a.ID ?? "";
            string idB = b.ID ?? "";
            return string.Compare(idA, idB, System.StringComparison.OrdinalIgnoreCase);
        });
    }

    // =========================================================
    // Normal フォルダ内の Monster を自動登録
    // Assets/ScriptableAsset/Monsterlist/Normal/ 以下を再帰検索し、
    // まだ monsters リストに入っていない Monster を追加する。
    // Boss フォルダ内のモンスターは対象外。
    // 追加後は自動でIDソートも実行する。
    // =========================================================
    private void AutoRegisterFromNormalFolder()
    {
        // Normal フォルダ内の全 Monster アセットを検索
        string[] guids = AssetDatabase.FindAssets("t:Monster", new[] { "Assets/ScriptableAsset/Monsterlist/Normal" });

        if (guids.Length == 0)
        {
            EditorUtility.DisplayDialog(
                "自動登録",
                "Assets/ScriptableAsset/Monsterlist/Normal/ 内に Monster が見つかりませんでした。",
                "OK"
            );
            return;
        }

        // 既存モンスターのセットを作成（重複チェック用）
        var existingSet = new HashSet<Monster>(targetDatabase.monsters.Where(m => m != null));

        int addedCount = 0;
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var monster = AssetDatabase.LoadAssetAtPath<Monster>(path);
            if (monster == null) continue;

            if (!existingSet.Contains(monster))
            {
                targetDatabase.monsters.Add(monster);
                existingSet.Add(monster);
                addedCount++;
            }
        }

        // null エントリがあれば除去
        targetDatabase.monsters.RemoveAll(m => m == null);

        // ソート実行
        SortByMonsterIdInternal();

        // 索引を再構築
        targetDatabase.InvalidateIndex();

        // 変更を保存
        EditorUtility.SetDirty(targetDatabase);
        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog(
            "自動登録完了",
            $"検出: {guids.Length} 件\n新規追加: {addedCount} 件\n合計: {targetDatabase.monsters.Count} 件",
            "OK"
        );
    }

    // =========================================================
    // ID でソート（ボタン用）
    // =========================================================
    private void SortByMonsterId()
    {
        // null エントリがあれば除去
        targetDatabase.monsters.RemoveAll(m => m == null);

        SortByMonsterIdInternal();

        targetDatabase.InvalidateIndex();
        EditorUtility.SetDirty(targetDatabase);
        AssetDatabase.SaveAssets();
    }

    // =========================================================
    // ソート内部処理
    // ID の文字列比較でソート（001, 002, ... の番号順になる）
    // =========================================================
    private void SortByMonsterIdInternal()
    {
        targetDatabase.monsters.Sort((a, b) =>
        {
            string idA = a.ID ?? "";
            string idB = b.ID ?? "";
            return string.Compare(idA, idB, System.StringComparison.OrdinalIgnoreCase);
        });
    }

    // =========================================================
    // 通常モンスター用ヘッダー・行
    // =========================================================
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

    // =========================================================
    // ボスモンスター用ヘッダー・行
    // =========================================================
    private void DrawBossHeader()
    {
        EditorGUILayout.BeginHorizontal("box");
        GUILayout.Label("ID", EditorStyles.boldLabel, GUILayout.Width(160));
        GUILayout.Label("名前", EditorStyles.boldLabel, GUILayout.Width(160));
        GUILayout.Label("HP", EditorStyles.boldLabel, GUILayout.Width(80));
        GUILayout.Label("ATK", EditorStyles.boldLabel, GUILayout.Width(80));
        GUILayout.FlexibleSpace();
        GUILayout.Label("詳細", EditorStyles.boldLabel, GUILayout.Width(60));
        EditorGUILayout.EndHorizontal();
    }

    private void DrawBossRow(Monster boss)
    {
        EditorGUILayout.BeginHorizontal("box");

        GUILayout.Label(boss.ID, GUILayout.Width(160));
        GUILayout.Label(boss.Mname, GUILayout.Width(160));
        GUILayout.Label(boss.MaxHp.ToString(), GUILayout.Width(80));
        GUILayout.Label(boss.Attack.ToString(), GUILayout.Width(80));

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("詳細", GUILayout.Width(60)))
        {
            MonsterDetailWindow.Open(boss);
        }

        EditorGUILayout.EndHorizontal();
    }

    // =========================================================
    // 共通ユーティリティ
    // =========================================================
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