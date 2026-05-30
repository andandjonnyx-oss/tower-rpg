// SlotImageAlphaCleaner.cs
// 配置場所: Assets/Editor/SlotImageAlphaCleaner.cs
// 用途    : Hierarchyで選択中のGameObject配下にある全ての ItemSlotView の
//           Frame Image と Icon Image のα値を0に一括設定する。
//           プレハブ化されていないスロット群の整備用。
//
// 使い方:
//   1. Hierarchyで、複数スロットを子に持つ親GameObject（例: Content）を選択
//   2. メニュー Tools > Slot Image Alpha Cleaner を開く
//   3. 「選択中の配下を一括α0化」ボタンを押す
//   4. 確認ダイアログで OK
//
// 仕様:
//   - 選択GameObjectの子孫を再帰的に走査
//   - ItemSlotView コンポーネントを持つGameObjectを検出
//   - その frameImage と iconImage フィールド（private SerializeField）を
//     SerializedObject 経由で取得
//   - 取得したImageコンポーネントの color.a を 0 に設定
//   - Undo対応（Ctrl+Z で元に戻せる）
//
// 注意:
//   - 実行時には iconImage.color が ItemSlotView.RefreshEquipColor() で
//     Color.white に上書きされるため、α0化しても実機でアイテム画像は表示される。
//   - これはプレハブ側で既に実証済みの挙動。

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
public class SlotImageAlphaCleaner : EditorWindow
{
    // ============================================================
    // 設定セクション
    // ============================================================

    // 処理対象のフィールド名（ItemSlotView の private SerializeField 名）
    private const string FIELD_FRAME_IMAGE = "frameImage";
    private const string FIELD_ICON_IMAGE = "iconImage";

    // ============================================================
    // 状態
    // ============================================================
    private bool _processFrameImage = true;
    private bool _processIconImage = true;
    private string _statusMessage = "";
    private MessageType _statusType = MessageType.None;

    // ============================================================
    // メニュー
    // ============================================================
    [MenuItem("Tools/Slot Image Alpha Cleaner")]
    public static void ShowWindow()
    {
        var window = GetWindow<SlotImageAlphaCleaner>("Slot Alpha Cleaner");
        window.minSize = new Vector2(400, 280);
    }

    // ============================================================
    // GUI
    // ============================================================
    private void OnGUI()
    {
        EditorGUILayout.LabelField("スロット画像 一括α0化", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        EditorGUILayout.HelpBox(
            "Hierarchyで選択中のGameObjectとその子孫を走査し、\n" +
            "見つかった ItemSlotView の Frame/Icon Image を一括でα0にします。\n" +
            "Undo（Ctrl+Z）で元に戻せます。",
            MessageType.Info);

        EditorGUILayout.Space(4);

        _processFrameImage = EditorGUILayout.Toggle("Frame Image をα0化", _processFrameImage);
        _processIconImage = EditorGUILayout.Toggle("Icon Image をα0化", _processIconImage);

        EditorGUILayout.Space(8);

        // 選択中の情報を表示
        var selected = Selection.activeGameObject;
        if (selected != null)
        {
            EditorGUILayout.LabelField($"選択中: {selected.name}", EditorStyles.miniBoldLabel);
            int countBelow = CountSlotsInChildren(selected);
            EditorGUILayout.LabelField($"配下の ItemSlotView 数: {countBelow}");
        }
        else
        {
            EditorGUILayout.LabelField("選択中: (なし)", EditorStyles.miniBoldLabel);
        }

        EditorGUILayout.Space(8);

        using (new EditorGUI.DisabledScope(selected == null ||
                                           (!_processFrameImage && !_processIconImage)))
        {
            if (GUILayout.Button("選択中の配下を一括α0化", GUILayout.Height(32)))
            {
                if (EditorUtility.DisplayDialog(
                    "確認",
                    $"「{selected.name}」配下の全 ItemSlotView を一括処理します。\n" +
                    $"  Frame: {_processFrameImage}\n" +
                    $"  Icon:  {_processIconImage}\n\n" +
                    "実行しますか？（Undo可能）",
                    "実行", "キャンセル"))
                {
                    ProcessSelection(selected);
                }
            }
        }

        // ステータス表示
        if (!string.IsNullOrEmpty(_statusMessage))
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.HelpBox(_statusMessage, _statusType);
        }
    }

    // ============================================================
    // ItemSlotView を子孫から数える（表示用）
    // ============================================================
    private static int CountSlotsInChildren(GameObject root)
    {
        if (root == null) return 0;
        // GetComponentsInChildren(true) で非アクティブも含める
        var slots = root.GetComponentsInChildren<ItemSlotView>(true);
        return slots.Length;
    }

    // ============================================================
    // 実行本体
    // ============================================================
    private void ProcessSelection(GameObject root)
    {
        if (root == null) return;

        var slots = root.GetComponentsInChildren<ItemSlotView>(true);
        if (slots.Length == 0)
        {
            _statusMessage = "配下に ItemSlotView が見つかりませんでした。";
            _statusType = MessageType.Warning;
            return;
        }

        // Undo グループ開始
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Slot Image Alpha Clean");

        int frameProcessed = 0;
        int iconProcessed = 0;
        int skipped = 0;

        foreach (var slot in slots)
        {
            if (slot == null) { skipped++; continue; }

            // SerializedObject 経由で private SerializeField のImageを取得
            var so = new SerializedObject(slot);

            if (_processFrameImage)
            {
                Image frameImg = GetImageField(so, FIELD_FRAME_IMAGE);
                if (frameImg != null)
                {
                    SetAlphaWithUndo(frameImg, 0f);
                    frameProcessed++;
                }
            }

            if (_processIconImage)
            {
                Image iconImg = GetImageField(so, FIELD_ICON_IMAGE);
                if (iconImg != null)
                {
                    SetAlphaWithUndo(iconImg, 0f);
                    iconProcessed++;
                }
            }
        }

        Undo.CollapseUndoOperations(undoGroup);

        _statusMessage = $"処理完了: {slots.Length}個の ItemSlotView を走査\n" +
                         $"  Frame Image α0化: {frameProcessed} 件\n" +
                         $"  Icon Image α0化:  {iconProcessed} 件\n" +
                         (skipped > 0 ? $"  スキップ(null): {skipped} 件\n" : "") +
                         "Ctrl+Z で元に戻せます。";
        _statusType = MessageType.Info;

        // Sceneビュー再描画
        SceneView.RepaintAll();
    }

    // ============================================================
    // SerializedObject から Image 型フィールドを取得
    // ============================================================
    private static Image GetImageField(SerializedObject so, string fieldName)
    {
        var prop = so.FindProperty(fieldName);
        if (prop == null) return null;
        if (prop.propertyType != SerializedPropertyType.ObjectReference) return null;
        return prop.objectReferenceValue as Image;
    }

    // ============================================================
    // Image の color.a を変更（Undo対応）
    // ============================================================
    private static void SetAlphaWithUndo(Image img, float alpha)
    {
        Undo.RecordObject(img, "Set Image Alpha");
        Color c = img.color;
        c.a = alpha;
        img.color = c;
        EditorUtility.SetDirty(img);
    }
}
#endif