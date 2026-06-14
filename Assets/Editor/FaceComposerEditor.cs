#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(FaceComposer))]
public class FaceComposerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var fc = (FaceComposer)target;

        // 標準のフィールド（Image/Sprites参照）を先に表示
        DrawDefaultInspector();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("表情コントロール", EditorStyles.boldLabel);

        foreach (var p in fc.All)
        {
            if (p == null) continue;

            EditorGUILayout.BeginHorizontal();

            string name = string.IsNullOrEmpty(p.label) ? "(無名)" : p.label;
            int max = (p.sprites != null && p.sprites.Length > 0) ? p.sprites.Length - 1 : 0;

            EditorGUILayout.LabelField($"{name}", GUILayout.Width(50));

            if (GUILayout.Button("−", GUILayout.Width(28)))
            {
                Undo.RecordObject(fc, "Face Step");
                p.Step(-1);
                MarkDirty(fc);
            }

            int newIndex = EditorGUILayout.IntSlider(p.index, 0, max);
            if (newIndex != p.index)
            {
                Undo.RecordObject(fc, "Face Slider");
                p.SetIndex(newIndex);
                MarkDirty(fc);
            }

            if (GUILayout.Button("＋", GUILayout.Width(28)))
            {
                Undo.RecordObject(fc, "Face Step");
                p.Step(1);
                MarkDirty(fc);
            }

            EditorGUILayout.LabelField($"/ {max}", GUILayout.Width(40));

            EditorGUILayout.EndHorizontal();
        }
    }

    void MarkDirty(FaceComposer fc)
    {
        EditorUtility.SetDirty(fc);
        if (!Application.isPlaying)
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(fc.gameObject.scene);
    }
}
#endif