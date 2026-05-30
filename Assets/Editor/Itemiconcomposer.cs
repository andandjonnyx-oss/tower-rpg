// ItemIconComposer.cs
// 配置場所: Assets/Editor/ItemIconComposer.cs
// 用途    : 背景画像＋アイテム素材＋Lv表記を合成してPNG出力するEditor拡張
// 使い方  : Unityメニューから [Tools] > [Item Icon Composer] を開く
//
// 設定変更可能箇所（後日のフォント・色変更はこのファイル冒頭を編集）:
//   - FONT_ASSET_PATH    : 使用するTTF/OTFのプロジェクト内パス
//   - FONT_SIZE          : 文字サイズ（px）
//   - TEXT_COLOR         : 文字色
//   - OUTLINE_COLOR      : 縁取り色
//   - OUTLINE_WIDTH      : 縁取り太さ（px、0で無効）
//   - LABEL_MARGIN       : 右下からのマージン（px）
//   - LABEL_FORMAT       : テキスト書式（"Lv{0}", "+{0}" 等）
//   - OUTPUT_SIZE        : 出力画像サイズ（正方形）
//   - ITEM_SCALE         : 背景に対する素材の最大占有率
//   - ITEM_OFFSET_Y      : 素材の縦位置オフセット
//   - DEFAULT_OUTPUT_DIR : デフォルトの出力フォルダ

using System.IO;
using UnityEditor;
using UnityEngine;

#if UNITY_EDITOR
public class ItemIconComposer : EditorWindow
{
    // ============================================================
    // 設定セクション（後日変更する箇所はここ）
    // ============================================================

    // フォント（プロジェクト内のTTF/OTFファイルパス。Assets/からの相対パス）
    // 例: "Assets/Fonts/NotoSansJP-Bold.ttf"
    // 空文字列ならUnity標準のArialを使用
    private const string FONT_ASSET_PATH = "";

    private const int FONT_SIZE = 48;
    private static readonly Color32 TEXT_COLOR = new Color32(255, 235, 80, 255);
    private static readonly Color32 OUTLINE_COLOR = new Color32(40, 20, 0, 255);
    private const int OUTLINE_WIDTH = 3;
    private const int LABEL_MARGIN = 6;
    private const string LABEL_FORMAT = "Lv{0}";

    private const int OUTPUT_SIZE = 256;
    private const float ITEM_SCALE = 0.72f;
    private const int ITEM_OFFSET_Y = 0;

    private const string DEFAULT_OUTPUT_DIR = "Assets/Sprites/items/composed";

    // ============================================================
    // ウィンドウ状態
    // ============================================================
    private Texture2D _bgTexture;
    private Texture2D _itemTexture;
    private int _level = 0;          // 0 ならLv表記なし
    private string _outputDir = DEFAULT_OUTPUT_DIR;
    private string _outputFileName = "item_icon_composed.png";
    private string _statusMessage = "";
    private MessageType _statusType = MessageType.None;

    // ============================================================
    // メニュー
    // ============================================================
    [MenuItem("Tools/Item Icon Composer")]
    public static void ShowWindow()
    {
        var window = GetWindow<ItemIconComposer>("Item Icon Composer");
        window.minSize = new Vector2(420, 360);
    }

    // ============================================================
    // GUI
    // ============================================================
    private void OnGUI()
    {
        EditorGUILayout.LabelField("アイテムアイコン合成", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        // 背景・素材選択
        _bgTexture = (Texture2D)EditorGUILayout.ObjectField("背景画像 (羊皮紙等)", _bgTexture, typeof(Texture2D), false);
        _itemTexture = (Texture2D)EditorGUILayout.ObjectField("アイテム素材", _itemTexture, typeof(Texture2D), false);

        EditorGUILayout.Space(8);

        // Lv表記
        EditorGUILayout.LabelField("Lv表記（0または空欄なら表記なし）");
        _level = EditorGUILayout.IntField("Level", _level);
        if (_level < 0) _level = 0;

        EditorGUILayout.Space(8);

        // 出力先
        EditorGUILayout.LabelField("出力設定", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        _outputDir = EditorGUILayout.TextField("出力フォルダ", _outputDir);
        if (GUILayout.Button("...", GUILayout.Width(28)))
        {
            string sel = EditorUtility.OpenFolderPanel("出力フォルダを選択", Application.dataPath, "");
            if (!string.IsNullOrEmpty(sel))
            {
                // プロジェクト内パスに変換
                if (sel.StartsWith(Application.dataPath))
                {
                    _outputDir = "Assets" + sel.Substring(Application.dataPath.Length);
                }
                else
                {
                    _outputDir = sel;
                }
            }
        }
        EditorGUILayout.EndHorizontal();

        _outputFileName = EditorGUILayout.TextField("ファイル名 (.png)", _outputFileName);

        EditorGUILayout.Space(12);

        // 実行ボタン
        using (new EditorGUI.DisabledScope(_bgTexture == null || _itemTexture == null || string.IsNullOrEmpty(_outputFileName)))
        {
            if (GUILayout.Button("合成して書き出す", GUILayout.Height(32)))
            {
                ExecuteCompose();
            }
        }

        // ステータス表示
        if (!string.IsNullOrEmpty(_statusMessage))
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.HelpBox(_statusMessage, _statusType);
        }

        // 設定確認用の情報表示
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("現在の合成設定（変更はスクリプトを編集）", EditorStyles.miniBoldLabel);
        EditorGUILayout.LabelField($"  出力サイズ: {OUTPUT_SIZE}px / 素材占有率: {ITEM_SCALE:F2}");
        EditorGUILayout.LabelField($"  フォント: {(string.IsNullOrEmpty(FONT_ASSET_PATH) ? "LegacyRuntime(標準)" : FONT_ASSET_PATH)}");
        EditorGUILayout.LabelField($"  Lvサイズ: {FONT_SIZE}px / 縁取り: {OUTLINE_WIDTH}px");
    }

    // ============================================================
    // 実行本体
    // ============================================================
    private void ExecuteCompose()
    {
        try
        {
            // 出力フォルダを保証（Assets内のときはAssetDatabaseの整合性も保つ）
            if (!Directory.Exists(_outputDir))
            {
                Directory.CreateDirectory(_outputDir);
            }

            string fileName = _outputFileName;
            if (!fileName.ToLower().EndsWith(".png")) fileName += ".png";
            string outputPath = Path.Combine(_outputDir, fileName).Replace("\\", "/");

            // テクスチャを読み取り可能な状態で取得
            Texture2D bgReadable = GetReadableCopy(_bgTexture);
            Texture2D itemReadable = GetReadableCopy(_itemTexture);

            // 合成処理
            Texture2D result = ComposeIcon(bgReadable, itemReadable);

            // Lv表記
            if (_level > 0)
            {
                DrawLevelLabel(result, _level);
            }

            // PNG書き出し
            byte[] png = result.EncodeToPNG();
            File.WriteAllBytes(outputPath, png);

            // 一時テクスチャ破棄
            DestroyImmediate(bgReadable);
            DestroyImmediate(itemReadable);
            DestroyImmediate(result);

            // AssetDatabase更新（Assets配下なら）
            if (outputPath.StartsWith("Assets/"))
            {
                AssetDatabase.ImportAsset(outputPath, ImportAssetOptions.ForceUpdate);
            }
            AssetDatabase.Refresh();

            _statusMessage = $"出力完了: {outputPath}";
            _statusType = MessageType.Info;
            Debug.Log($"[ItemIconComposer] {_statusMessage}");
        }
        catch (System.Exception e)
        {
            _statusMessage = $"エラー: {e.Message}";
            _statusType = MessageType.Error;
            Debug.LogError($"[ItemIconComposer] {e}");
        }
    }

    // ============================================================
    // テクスチャを読み取り可能な状態で取得
    //   元のインポート設定を変えずに済むよう、AssetDatabaseから一時的にRead/Writeを有効化→
    //   ピクセルをコピーした新規Texture2Dを返す→元の設定に戻す
    // ============================================================
    private static Texture2D GetReadableCopy(Texture2D source)
    {
        string assetPath = AssetDatabase.GetAssetPath(source);
        TextureImporter importer = null;
        bool originalReadable = false;
        TextureImporterCompression originalCompression = TextureImporterCompression.Compressed;
        bool importerModified = false;

        if (!string.IsNullOrEmpty(assetPath))
        {
            importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null)
            {
                originalReadable = importer.isReadable;
                originalCompression = importer.textureCompression;
                if (!originalReadable || originalCompression != TextureImporterCompression.Uncompressed)
                {
                    importer.isReadable = true;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.SaveAndReimport();
                    importerModified = true;
                }
            }
        }

        // GetPixelsで読み取り、新規Texture2Dにコピー
        Texture2D copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
        copy.SetPixels(source.GetPixels());
        copy.Apply();

        // 元のインポート設定に戻す
        if (importerModified && importer != null)
        {
            importer.isReadable = originalReadable;
            importer.textureCompression = originalCompression;
            importer.SaveAndReimport();
        }

        return copy;
    }

    // ============================================================
    // 合成本体：背景を出力サイズにリサイズ → 中央に素材を縦横比保持でリサイズして重ねる
    // ============================================================
    private static Texture2D ComposeIcon(Texture2D bg, Texture2D item)
    {
        // 出力キャンバス（透明）
        Texture2D canvas = new Texture2D(OUTPUT_SIZE, OUTPUT_SIZE, TextureFormat.RGBA32, false);
        Color32[] empty = new Color32[OUTPUT_SIZE * OUTPUT_SIZE];
        canvas.SetPixels32(empty);

        // 背景をリサイズしてキャンバスに描画
        Texture2D bgResized = ResizeBilinear(bg, OUTPUT_SIZE, OUTPUT_SIZE);
        BlitAlphaComposite(canvas, bgResized, 0, 0);

        // 素材を ITEM_SCALE 内に収まるように縦横比保持リサイズ
        int target = Mathf.RoundToInt(OUTPUT_SIZE * ITEM_SCALE);
        float scale = Mathf.Min((float)target / item.width, (float)target / item.height);
        int newW = Mathf.Max(1, Mathf.RoundToInt(item.width * scale));
        int newH = Mathf.Max(1, Mathf.RoundToInt(item.height * scale));
        Texture2D itemResized = ResizeBilinear(item, newW, newH);

        // 中央配置（オフセット適用）
        int px = (OUTPUT_SIZE - newW) / 2;
        int py = (OUTPUT_SIZE - newH) / 2 + ITEM_OFFSET_Y;
        BlitAlphaComposite(canvas, itemResized, px, py);

        canvas.Apply();

        // 一時破棄
        Object.DestroyImmediate(bgResized);
        Object.DestroyImmediate(itemResized);

        return canvas;
    }

    // ============================================================
    // バイリニアリサイズ（Texture2D汎用）
    // ============================================================
    private static Texture2D ResizeBilinear(Texture2D src, int targetW, int targetH)
    {
        // RenderTexture経由が高速だが、ここでは依存を減らすためTexture2DのGetPixelBilinearで実装
        Texture2D result = new Texture2D(targetW, targetH, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[targetW * targetH];
        for (int y = 0; y < targetH; y++)
        {
            float v = (y + 0.5f) / targetH;
            for (int x = 0; x < targetW; x++)
            {
                float u = (x + 0.5f) / targetW;
                pixels[y * targetW + x] = src.GetPixelBilinear(u, v);
            }
        }
        result.SetPixels(pixels);
        result.Apply();
        return result;
    }

    // ============================================================
    // 透明合成（src を dst の (offsetX, offsetY) に重ねる、アルファブレンド）
    //   Y軸はUnityの慣例どおり下向き（画像描画なのでyが上の方が小さい）
    //   = ここでは Texture2D の座標系（左下原点）を活かして直接合成
    //
    //   外部から座標を渡すとき、画像左上を (0,0) で考えた方が直感的なので
    //   呼び出し側で「左上原点 → 左下原点」に補正してから渡す
    // ============================================================
    private static void BlitAlphaComposite(Texture2D dst, Texture2D src, int offsetX_topLeft, int offsetY_topLeft)
    {
        int sw = src.width;
        int sh = src.height;
        int dw = dst.width;
        int dh = dst.height;

        // 左上原点→左下原点 変換: src の上端が dst の上から offsetY_topLeft px の位置に来るようにする
        int dstYBottomOfSrc = dh - (offsetY_topLeft + sh); // src の左下が dst のどこに来るか（左下原点）

        Color[] srcPx = src.GetPixels();
        Color[] dstPx = dst.GetPixels();

        for (int y = 0; y < sh; y++)
        {
            int dstY = dstYBottomOfSrc + y;
            if (dstY < 0 || dstY >= dh) continue;
            for (int x = 0; x < sw; x++)
            {
                int dstX = offsetX_topLeft + x;
                if (dstX < 0 || dstX >= dw) continue;
                Color s = srcPx[y * sw + x];
                if (s.a <= 0f) continue;
                Color d = dstPx[dstY * dw + dstX];
                float outA = s.a + d.a * (1f - s.a);
                if (outA <= 0f) { dstPx[dstY * dw + dstX] = Color.clear; continue; }
                Color outC = new Color(
                    (s.r * s.a + d.r * d.a * (1f - s.a)) / outA,
                    (s.g * s.a + d.g * d.a * (1f - s.a)) / outA,
                    (s.b * s.a + d.b * d.a * (1f - s.a)) / outA,
                    outA
                );
                dstPx[dstY * dw + dstX] = outC;
            }
        }
        dst.SetPixels(dstPx);
        dst.Apply();
    }

    // ============================================================
    // Lv表記描画
    //   Font.RequestCharactersInTexture でフォントのグリフテクスチャを生成し、
    //   その上でCharacterInfoを使ってピクセル単位で描画する。
    //   Unity Editorでも動くダイナミックフォント方式。
    // ============================================================
    private static void DrawLevelLabel(Texture2D target, int level)
    {
        string text = string.Format(LABEL_FORMAT, level);

        // フォント取得
        Font font = LoadFont();
        if (font == null)
        {
            Debug.LogWarning("[ItemIconComposer] フォントが読み込めなかったため、Lv表記を省略しました");
            return;
        }

        // ダイナミックフォントのテクスチャ生成
        font.RequestCharactersInTexture(text, FONT_SIZE, FontStyle.Bold);
        Texture2D fontTex = font.material.mainTexture as Texture2D;

        // テキスト幅・高さ計算
        int totalWidth = 0;
        int maxHeight = 0;
        int maxAscent = 0;
        foreach (char c in text)
        {
            if (font.GetCharacterInfo(c, out CharacterInfo ci, FONT_SIZE, FontStyle.Bold))
            {
                totalWidth += ci.advance;
                maxHeight = Mathf.Max(maxHeight, Mathf.Abs(ci.maxY - ci.minY));
                maxAscent = Mathf.Max(maxAscent, ci.maxY);
            }
        }

        // フォントテクスチャをCPUから読めるようにする
        Texture2D fontTexReadable = GetReadableCopyOfTexture(fontTex);

        // 右下配置（左上原点座標）
        int dstX = target.width - totalWidth - LABEL_MARGIN;
        int dstY = target.height - maxHeight - LABEL_MARGIN;

        // 縁取りを先に描画（テキストを上下左右にずらしてOUTLINE_COLORで描く）
        if (OUTLINE_WIDTH > 0)
        {
            for (int dx = -OUTLINE_WIDTH; dx <= OUTLINE_WIDTH; dx++)
            {
                for (int dy = -OUTLINE_WIDTH; dy <= OUTLINE_WIDTH; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    DrawTextRun(target, fontTexReadable, font, text, dstX + dx, dstY + dy, maxAscent, OUTLINE_COLOR);
                }
            }
        }

        // 本体テキストを描画
        DrawTextRun(target, fontTexReadable, font, text, dstX, dstY, maxAscent, TEXT_COLOR);

        target.Apply();
        Object.DestroyImmediate(fontTexReadable);
    }

    // ============================================================
    // 1行テキストを target に描画
    //   textOriginX, textOriginY は左上原点の描画開始位置（テキスト全体のbbox左上）
    //   ascent はベースラインの位置補正用
    //
    // 【UV補正について】
    //   CharacterInfo の uvTopLeft / uvTopRight / uvBottomLeft / uvBottomRight は
    //   Unity のダイナミックフォントテクスチャ上での「グリフの4角」のUV座標を持つ。
    //   フォントアトラスの実装によっては:
    //     - グリフが90度回転して格納される（横向きに敷き詰める最適化）
    //     - Y軸が反転している（テクスチャの上下が逆）
    //   といったケースがあるため、Mathf.Min/Max で範囲だけ取ると向きの情報が失われ、
    //   結果として文字が反転・回転してしまう。
    //   ここではグリフ内の正規化座標 (u_norm, v_norm) ∈ [0,1] を、
    //   4角UVの「バイリニア補間」で実UVに変換することで、向きを正しく保つ。
    // ============================================================
    private static void DrawTextRun(Texture2D target, Texture2D fontTex, Font font, string text,
                                    int textOriginX, int textOriginY, int ascent, Color32 color)
    {
        int penX = textOriginX;
        foreach (char c in text)
        {
            if (!font.GetCharacterInfo(c, out CharacterInfo ci, FONT_SIZE, FontStyle.Bold)) continue;

            int glyphW = Mathf.Abs(ci.maxX - ci.minX);
            int glyphH = Mathf.Abs(ci.maxY - ci.minY);

            // 描画位置（左上原点）
            int glyphOriginX = penX + ci.minX;
            int glyphOriginY = textOriginY + (ascent - ci.maxY);

            // フォントアトラス上のグリフ4角UV
            //   uvTopLeft     : グリフの左上
            //   uvTopRight    : グリフの右上
            //   uvBottomLeft  : グリフの左下
            //   uvBottomRight : グリフの右下
            // これら4点をバイリニア補間してグリフ内ピクセルのUVを求める。
            Vector2 uvTL = ci.uvTopLeft;
            Vector2 uvTR = ci.uvTopRight;
            Vector2 uvBL = ci.uvBottomLeft;
            Vector2 uvBR = ci.uvBottomRight;

            // ピクセル単位コピー（フォントテクスチャはアルファチャンネルにグリフ情報）
            for (int gy = 0; gy < glyphH; gy++)
            {
                int dstY = target.height - 1 - (glyphOriginY + gy); // 左下原点へ
                if (dstY < 0 || dstY >= target.height) continue;

                // グリフ内の縦方向正規化座標。
                // gy=0 がグリフの「上端」なので、上端→下端へ進む = TopからBottomへ補間 = v_norm 0→1
                float v_norm = (gy + 0.5f) / glyphH;

                for (int gx = 0; gx < glyphW; gx++)
                {
                    int dstX = glyphOriginX + gx;
                    if (dstX < 0 || dstX >= target.width) continue;

                    // グリフ内の横方向正規化座標。
                    // gx=0 がグリフの「左端」なので、左端→右端へ進む = LeftからRightへ補間 = u_norm 0→1
                    float u_norm = (gx + 0.5f) / glyphW;

                    // バイリニア補間で4角UVから実UVを算出（回転・反転に関係なく正しい）
                    Vector2 uvTop = Vector2.Lerp(uvTL, uvTR, u_norm);
                    Vector2 uvBottom = Vector2.Lerp(uvBL, uvBR, u_norm);
                    Vector2 uv = Vector2.Lerp(uvTop, uvBottom, v_norm);

                    Color fontPx = fontTex.GetPixelBilinear(uv.x, uv.y);
                    // ダイナミックフォントテクスチャはアルファに濃度
                    float coverage = fontPx.a;
                    if (coverage <= 0.01f) continue;

                    Color s = new Color(color.r / 255f, color.g / 255f, color.b / 255f, coverage * (color.a / 255f));
                    Color d = target.GetPixel(dstX, dstY);
                    float outA = s.a + d.a * (1f - s.a);
                    if (outA <= 0f) continue;
                    Color outC = new Color(
                        (s.r * s.a + d.r * d.a * (1f - s.a)) / outA,
                        (s.g * s.a + d.g * d.a * (1f - s.a)) / outA,
                        (s.b * s.a + d.b * d.a * (1f - s.a)) / outA,
                        outA
                    );
                    target.SetPixel(dstX, dstY, outC);
                }
            }

            penX += ci.advance;
        }
    }

    // ============================================================
    // フォント読み込み
    //
    // Unity 2022 以降は組み込みフォントが Arial.ttf から LegacyRuntime.ttf に
    // 変更されているため、LegacyRuntime.ttf を優先的に読み込む。
    // ============================================================
    private static Font LoadFont()
    {
        if (!string.IsNullOrEmpty(FONT_ASSET_PATH))
        {
            Font f = AssetDatabase.LoadAssetAtPath<Font>(FONT_ASSET_PATH);
            if (f != null) return f;
            Debug.LogWarning($"[ItemIconComposer] 指定フォントが見つかりません: {FONT_ASSET_PATH} → 標準フォントを使用");
        }
        // Unity 2022以降: Arial.ttf → LegacyRuntime.ttf に変更
        Font legacy = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (legacy != null) return legacy;
        // 古いUnity向けフォールバック
        Font arial = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (arial != null) return arial;
        // 最終フォールバック: OS提供のフォント
        return Font.CreateDynamicFontFromOSFont("Arial", FONT_SIZE);
    }

    // ============================================================
    // フォントテクスチャを読み取り可能にする
    //   RenderTexture経由で複製
    // ============================================================
    private static Texture2D GetReadableCopyOfTexture(Texture2D source)
    {
        RenderTexture rt = RenderTexture.GetTemporary(source.width, source.height, 0,
                                                      RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        Graphics.Blit(source, rt);
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;
        Texture2D copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
        copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
        copy.Apply();
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        return copy;
    }
}
#endif