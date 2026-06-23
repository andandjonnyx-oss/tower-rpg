using UnityEngine;
using UnityEditor;
using TMPro;

/// <summary>
/// Status詳細画面の「値ボックス」を一括生成する Editor 拡張。
///
/// 使い方：
///   1. Status シーンを開く。
///   2. テンプレート（基準ラベル kougeki と 基準値ボックス kougeki1）が
///      シーン内に存在することを確認。
///   3. Hierarchy で「値ボックスを付けたいラベル」を1つ以上選択。
///   4. メニュー [Tools > Status > 選択ラベルに値ボックスを生成] を実行。
///
/// 動作：
///   - kougeki1 を複製し、各ラベルの隣（kougeki→kougeki1 と同じ相対オフセット）に配置。
///   - 生成名は「ラベル名 + Value」。同名の子が既にあればスキップ。
///   - Undo 対応（Ctrl+Z で取り消し可能）。
///   - Statusview への参照アサインは行わない（手動）。
/// </summary>
public static class StatusValueBoxGenerator
{
    // テンプレートの名前（必要なら変更）
    private const string TemplateLabelName = "kougeki";   // 基準ラベル
    private const string TemplateValueName = "kougeki1";  // 基準値ボックス

    private const string MenuPath = "Tools/Status/選択ラベルに値ボックスを生成";

    [MenuItem(MenuPath)]
    private static void GenerateValueBoxes()
    {
        // テンプレートを名前で検索（シーン全体から、非アクティブ含む）
        var templateLabel = FindInSceneByName(TemplateLabelName);
        var templateValue = FindInSceneByName(TemplateValueName);

        if (templateLabel == null || templateValue == null)
        {
            EditorUtility.DisplayDialog(
                "テンプレートが見つかりません",
                $"基準ラベル「{TemplateLabelName}」と基準値ボックス「{TemplateValueName}」が\n"
                + "シーン内に必要です。名前を確認してください。",
                "OK");
            return;
        }

        var labelRT = templateLabel.GetComponent<RectTransform>();
        var valueRT = templateValue.GetComponent<RectTransform>();
        if (labelRT == null || valueRT == null)
        {
            EditorUtility.DisplayDialog("エラー",
                "テンプレートに RectTransform がありません。", "OK");
            return;
        }

        // ラベル→値ボックスの相対オフセット（anchoredPosition 差分）
        Vector2 offset = valueRT.anchoredPosition - labelRT.anchoredPosition;

        // 選択中のラベル
        var selected = Selection.gameObjects;
        if (selected == null || selected.Length == 0)
        {
            EditorUtility.DisplayDialog("選択がありません",
                "値ボックスを付けたいラベルを Hierarchy で選択してください。", "OK");
            return;
        }

        int created = 0, skipped = 0;

        foreach (var labelGo in selected)
        {
            // テンプレート自身は対象外
            if (labelGo == templateLabel || labelGo == templateValue)
            {
                skipped++;
                continue;
            }

            var lrt = labelGo.GetComponent<RectTransform>();
            if (lrt == null)
            {
                Debug.LogWarning($"[ValueBoxGen] {labelGo.name} に RectTransform がないためスキップ");
                skipped++;
                continue;
            }

            string newName = labelGo.name + "Value";

            // 同名の子が既にあればスキップ（二重生成防止）
            var parent = lrt.parent;
            if (parent != null && parent.Find(newName) != null)
            {
                Debug.Log($"[ValueBoxGen] {newName} は既に存在するためスキップ");
                skipped++;
                continue;
            }

            // kougeki1 を複製（通常のシーンオブジェクトなので Object.Instantiate を使う）
            var clone = Object.Instantiate(templateValue);

            Undo.RegisterCreatedObjectUndo(clone, "Generate Value Box");

            clone.name = newName;

            // 親をラベルと同じにする
            var crt = clone.GetComponent<RectTransform>();
            crt.SetParent(lrt.parent, worldPositionStays: false);

            // アンカー類はテンプレ（kougeki1）の値を維持しつつ、位置だけラベル基準で再計算
            crt.anchorMin = valueRT.anchorMin;
            crt.anchorMax = valueRT.anchorMax;
            crt.pivot     = valueRT.pivot;
            crt.sizeDelta = valueRT.sizeDelta;
            crt.localScale = valueRT.localScale;
            crt.anchoredPosition = lrt.anchoredPosition + offset;

            // 値テキストは空に（数値は実行時に Statusview が入れる）。
            // テンプレの "00000" を消してプレースホルダ無しにする。
            var tmp = clone.GetComponent<TMP_Text>();
            if (tmp != null) tmp.text = "";

            created++;
        }

        EditorUtility.DisplayDialog("値ボックス生成完了",
            $"生成: {created} 個 / スキップ: {skipped} 個\n\n"
            + "※ Statusview への参照アサインは手動で行ってください。", "OK");

        Debug.Log($"[ValueBoxGen] 完了 — 生成 {created}, スキップ {skipped}");
    }

    /// <summary>
    /// シーン内（非アクティブ含む）から名前一致の GameObject を探す。
    /// </summary>
    private static GameObject FindInSceneByName(string name)
    {
        // Resources.FindObjectsOfTypeAll で非アクティブも拾う
        var all = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (var go in all)
        {
            if (go.name != name) continue;
            // シーン上のオブジェクトのみ（Prefabアセット等を除外）
            if (go.scene.IsValid() && go.hideFlags == HideFlags.None)
                return go;
        }
        return null;
    }

    // メニューの有効/無効：ラベルが1つ以上選択されているとき有効
    [MenuItem(MenuPath, true)]
    private static bool ValidateGenerate()
    {
        return Selection.gameObjects != null && Selection.gameObjects.Length > 0;
    }
}
