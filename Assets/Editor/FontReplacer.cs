using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections.Generic;

public class FontReplacer : EditorWindow
{
    private Font targetFont;            // 旧Text用
    private TMP_FontAsset targetTmpFont; // TMP用

    private bool processScenes = true;
    private bool processPrefabs = true;
    private bool processUguiText = true;
    private bool processTmpText = true;

    [MenuItem("Tools/Font Replacer")]
    public static void Open() => GetWindow<FontReplacer>("Font Replacer");

    private void OnGUI()
    {
        EditorGUILayout.LabelField("一括フォント変更", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        processUguiText = EditorGUILayout.Toggle("uGUI Text を対象", processUguiText);
        if (processUguiText)
            targetFont = (Font)EditorGUILayout.ObjectField("Font (uGUI)", targetFont, typeof(Font), false);

        processTmpText = EditorGUILayout.Toggle("TMP_Text を対象", processTmpText);
        if (processTmpText)
            targetTmpFont = (TMP_FontAsset)EditorGUILayout.ObjectField("Font Asset (TMP)", targetTmpFont, typeof(TMP_FontAsset), false);

        EditorGUILayout.Space();
        processScenes = EditorGUILayout.Toggle("Build Settings のシーンを処理", processScenes);
        processPrefabs = EditorGUILayout.Toggle("全Prefabを処理", processPrefabs);

        EditorGUILayout.Space();
        if (GUILayout.Button("実行", GUILayout.Height(36)))
            Run();
    }

    private void Run()
    {
        if (processUguiText && targetFont == null && processTmpText && targetTmpFont == null)
        {
            EditorUtility.DisplayDialog("エラー", "フォントを指定してください。", "OK");
            return;
        }

        int total = 0;
        if (processPrefabs) total += ProcessAllPrefabs();
        if (processScenes) total += ProcessAllScenes();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("完了", $"{total} 個のTextを変更しました。", "OK");
        Debug.Log($"[FontReplacer] 完了: {total} 件変更");
    }

    private int ProcessAllPrefabs()
    {
        int count = 0;
        string[] guids = AssetDatabase.FindAssets("t:Prefab");
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            int changed = ApplyToHierarchy(root);
            if (changed > 0)
            {
                PrefabUtility.SaveAsPrefabAsset(root, path);
                count += changed;
            }
            PrefabUtility.UnloadPrefabContents(root);
        }
        return count;
    }

    private int ProcessAllScenes()
    {
        int count = 0;
        var current = EditorSceneManager.GetActiveScene().path;

        foreach (var s in EditorBuildSettings.scenes)
        {
            if (!s.enabled) continue;
            Scene scene = EditorSceneManager.OpenScene(s.path, OpenSceneMode.Single);
            int changed = 0;
            foreach (var go in scene.GetRootGameObjects())
                changed += ApplyToHierarchy(go);

            if (changed > 0)
                EditorSceneManager.SaveScene(scene);
            count += changed;
        }

        if (!string.IsNullOrEmpty(current))
            EditorSceneManager.OpenScene(current, OpenSceneMode.Single);
        return count;
    }

    private int ApplyToHierarchy(GameObject root)
    {
        int count = 0;

        if (processUguiText && targetFont != null)
        {
            foreach (var t in root.GetComponentsInChildren<Text>(true))
            {
                if (t.font == targetFont) continue;
                Undo.RecordObject(t, "Replace Font");
                t.font = targetFont;
                EditorUtility.SetDirty(t);
                count++;
            }
        }

        if (processTmpText && targetTmpFont != null)
        {
            foreach (var t in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (t.font == targetTmpFont) continue;
                Undo.RecordObject(t, "Replace Font");
                t.font = targetTmpFont;
                EditorUtility.SetDirty(t);
                count++;
            }
        }

        return count;
    }
}