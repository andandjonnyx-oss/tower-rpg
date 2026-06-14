#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

/// <summary>
/// 表情差分シミュレーター（プレビュー専用・シーン非破壊）。
/// Tools > 表情シミュレーター から開く。
/// FaceComposer から各パーツの Sprite 配列だけを借り、
/// ウィンドウ内で重ねて描画する。シーン上のオブジェクトは一切変更しない。
/// </summary>
public class FaceSimulatorWindow : EditorWindow
{
    private FaceComposer composer;

    // ウィンドウ内だけで保持する番号（シーンには反映しない）
    private int iBody, iHair, iBrow, iEye, iMouth;

    private float previewSize = 256f;

    [MenuItem("Tools/表情シミュレーター")]
    public static void Open()
    {
        var w = GetWindow<FaceSimulatorWindow>("表情シミュレーター");
        w.minSize = new Vector2(340, 520);
        w.TryAutoFind();
    }

    private void OnEnable() => TryAutoFind();

    private void TryAutoFind()
    {
        if (composer == null)
            composer = Object.FindFirstObjectByType<FaceComposer>();
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(6);

        composer = (FaceComposer)EditorGUILayout.ObjectField(
            "FaceComposer", composer, typeof(FaceComposer), true);

        if (composer == null)
        {
            EditorGUILayout.HelpBox(
                "シーン内の FaceComposer をアサインしてください。\n"
              + "（Sprite配列を読むだけで、シーンには影響しません）", MessageType.Info);
            if (GUILayout.Button("シーンから探す")) TryAutoFind();
            return;
        }

        EditorGUILayout.HelpBox(
            "プレビュー専用です。ここでの操作はシーンに反映されません。",
            MessageType.None);

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("パーツ番号", EditorStyles.boldLabel);

        iBody = PartSlider("身体", composer.body, iBody);
        iHair = PartSlider("髪", composer.hair, iHair);
        iBrow = PartSlider("眉", composer.brow, iBrow);
        iEye = PartSlider("目", composer.eye, iEye);
        iMouth = PartSlider("口", composer.mouth, iMouth);

        EditorGUILayout.Space(6);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("全部 0"))
                iBody = iHair = iBrow = iEye = iMouth = 0;
            if (GUILayout.Button("全部 +1"))
            {
                iBody = Step(composer.body, iBody, 1);
                iHair = Step(composer.hair, iHair, 1);
                iBrow = Step(composer.brow, iBrow, 1);
                iEye = Step(composer.eye, iEye, 1);
                iMouth = Step(composer.mouth, iMouth, 1);
            }
            if (GUILayout.Button("全部 −1"))
            {
                iBody = Step(composer.body, iBody, -1);
                iHair = Step(composer.hair, iHair, -1);
                iBrow = Step(composer.brow, iBrow, -1);
                iEye = Step(composer.eye, iEye, -1);
                iMouth = Step(composer.mouth, iMouth, -1);
            }
        }

        EditorGUILayout.Space(6);

        // entry用コピー
        string combo = $"身体:{iBody}  髪:{iHair}  眉:{iBrow}  目:{iEye}  口:{iMouth}";
        EditorGUILayout.SelectableLabel(combo, EditorStyles.textField, GUILayout.Height(18));
        if (GUILayout.Button("entry用の数値をコピー (body,hair,brow,eye,mouth)"))
        {
            string csv = $"{iBody},{iHair},{iBrow},{iEye},{iMouth}";
            EditorGUIUtility.systemCopyBuffer = csv;
            ShowNotification(new GUIContent("コピー: " + csv));
        }

        EditorGUILayout.Space(8);
        previewSize = EditorGUILayout.Slider("プレビューサイズ", previewSize, 128f, 512f);

        DrawPreview();
    }

    /// <summary>
    /// 1パーツのスライダー行。ボタンとスライダーの変更を確実に拾い、
    /// 更新後の index を返す。
    /// </summary>
    private int PartSlider(string label, FaceComposer.FacePart part, int current)
    {
        int max = MaxIndex(part);
        current = Mathf.Clamp(current, 0, max);

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(label, GUILayout.Width(40));

            if (GUILayout.Button("−", GUILayout.Width(26)))
                current = Mathf.Clamp(current - 1, 0, max);

            current = EditorGUILayout.IntSlider(current, 0, max);

            if (GUILayout.Button("＋", GUILayout.Width(26)))
                current = Mathf.Clamp(current + 1, 0, max);

            EditorGUILayout.LabelField($"/ {max}", GUILayout.Width(36));
        }
        return current;
    }

    private int Step(FaceComposer.FacePart part, int current, int delta)
        => Mathf.Clamp(current + delta, 0, MaxIndex(part));

    private int MaxIndex(FaceComposer.FacePart part)
        => (part != null && part.sprites != null && part.sprites.Length > 0)
            ? part.sprites.Length - 1 : 0;

    private Sprite Get(FaceComposer.FacePart part, int index)
    {
        if (part == null || part.sprites == null || part.sprites.Length == 0) return null;
        index = Mathf.Clamp(index, 0, part.sprites.Length - 1);
        return part.sprites[index];
    }

    /// <summary>
    /// 5パーツを奥→手前の順でウィンドウ内に重ねて描画する。
    /// </summary>
    private void DrawPreview()
    {
        EditorGUILayout.LabelField("プレビュー", EditorStyles.boldLabel);

        Rect area = GUILayoutUtility.GetRect(previewSize, previewSize,
            GUILayout.ExpandWidth(false));
        // 中央寄せ
        area.x = (position.width - previewSize) * 0.5f;
        area.width = previewSize;
        area.height = previewSize;

        // 背景（透過確認用の市松ではなくグレー。必要なら市松に変更可）
        EditorGUI.DrawRect(area, new Color(0.20f, 0.20f, 0.20f));

        DrawSprite(area, Get(composer.body, iBody));
        DrawSprite(area, Get(composer.hair, iHair));
        DrawSprite(area, Get(composer.brow, iBrow));
        DrawSprite(area, Get(composer.eye, iEye));
        DrawSprite(area, Get(composer.mouth, iMouth));
    }

    /// <summary>
    /// Sprite を矩形いっぱい（アスペクト維持）に描画。
    /// 全パーツ同一サイズPNG前提なので、同じ area に重ねれば位置が合う。
    /// </summary>
    private void DrawSprite(Rect area, Sprite sprite)
    {
        if (sprite == null) return;

        Texture2D tex = sprite.texture;
        if (tex == null) return;

        // Single スプライトなので rect はテクスチャ全体。UV を算出
        Rect tr = sprite.rect;
        Rect uv = new Rect(
            tr.x / tex.width,
            tr.y / tex.height,
            tr.width / tex.width,
            tr.height / tex.height);

        // アスペクト維持で area 内に収める
        float texAspect = tr.width / tr.height;
        float areaAspect = area.width / area.height;
        Rect draw = area;
        if (texAspect > areaAspect)
        {
            float h = area.width / texAspect;
            draw = new Rect(area.x, area.y + (area.height - h) * 0.5f, area.width, h);
        }
        else
        {
            float w = area.height * texAspect;
            draw = new Rect(area.x + (area.width - w) * 0.5f, area.y, w, area.height);
        }

        GUI.DrawTextureWithTexCoords(draw, tex, uv, true);
    }
}
#endif