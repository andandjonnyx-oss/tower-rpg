using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// F12 キーでスクリーンショットを撮影する開発用機能。
///
/// 【用途】
///   Play ストアの掲載用スクリーンショット撮影。エディタの Game ビュー、
///   および PC ビルドで動作する。実機（キーボードなし）では何もしない。
///
/// 【保存先】
///   エディタ  : &lt;プロジェクトルート&gt;/Screenshots/
///   ビルド    : Application.persistentDataPath/Screenshots/
///   ファイル名: screenshot_yyyyMMdd_HHmmss_fff.png
///
/// 【Play ストアの掲載要件（撮影時に自動チェックする）】
///   - 各辺 320px 〜 3840px
///   - 長辺は短辺の 2 倍以内（＝アスペクト比 2:1 まで）
///   - 24bit PNG（アルファなし）または JPEG
///   - 1ファイル 8MB まで
///
///   ⚠️ 実機解像度の 2400×1080 は 2.22:1 で **要件を超える**ため、そのまま撮ると
///      Play に弾かれる。撮影は Game ビューを **1920×1080（1.78:1）** にして行うこと。
///      1920×1080 は本プロジェクトの Reference Resolution かつ背景の設計値
///      （CLAUDE.md 第5節）と一致するため、左右のピラーボックスも出ない。
///      要件を外れた場合はコンソールに警告を出す。
///
/// 【入力について】
///   本プロジェクトの Active Input Handling は「Input System Package (New)」のみ。
///   旧 Input.GetKeyDown は例外を投げるため、必ず新 Input System の API を使うこと。
/// </summary>
public static class ScreenshotCaptureAutoCreate
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void CreateIfNeeded()
    {
        if (ScreenshotCapture.Instance != null) return;

        var go = new GameObject("ScreenshotCapture");
        go.AddComponent<ScreenshotCapture>();
    }
}

public class ScreenshotCapture : MonoBehaviour
{
    public static ScreenshotCapture Instance { get; private set; }

    /// <summary>Play ストアが許容する最小の辺の長さ。</summary>
    private const int MinSide = 320;
    /// <summary>Play ストアが許容する最大の辺の長さ。</summary>
    private const int MaxSide = 3840;
    /// <summary>Play ストアが許容する最大アスペクト比（長辺 / 短辺）。</summary>
    private const float MaxAspect = 2f;

    private bool isCapturing;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        // 実機にはキーボードが無いので Keyboard.current は null になる。
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.f12Key.wasPressedThisFrame)
            Capture();
    }

    /// <summary>
    /// スクリーンショットを撮影する。UI ボタン等から呼んでもよい。
    /// </summary>
    public void Capture()
    {
        if (isCapturing) return; // 連打で多重撮影しない
        StartCoroutine(CaptureRoutine());
    }

    private IEnumerator CaptureRoutine()
    {
        isCapturing = true;

        // 描画完了後でないと画面を読み取れない。
        yield return new WaitForEndOfFrame();

        Texture2D source = null;
        Texture2D rgb = null;

        try
        {
            source = ScreenCapture.CaptureScreenshotAsTexture();

            // Play ストアは「アルファチャンネルなし」を要求するため、
            // RGBA32 のまま保存せず RGB24 に変換してから PNG 化する。
            rgb = new Texture2D(source.width, source.height, TextureFormat.RGB24, false);
            rgb.SetPixels(source.GetPixels());
            rgb.Apply();

            byte[] png = rgb.EncodeToPNG();

            string dir = GetSaveDirectory();
            Directory.CreateDirectory(dir);

            string fileName = $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png";
            string path = Path.Combine(dir, fileName);
            File.WriteAllBytes(path, png);

            Debug.Log($"[Screenshot] 保存しました: {path} ({rgb.width}×{rgb.height}, {png.Length / 1024}KB)");
            WarnIfNotStoreCompliant(rgb.width, rgb.height, png.Length);
        }
        catch (Exception e)
        {
            Debug.LogError($"[Screenshot] 撮影に失敗: {e}");
        }
        finally
        {
            if (source != null) Destroy(source);
            if (rgb != null) Destroy(rgb);
            isCapturing = false;
        }
    }

    private static string GetSaveDirectory()
    {
#if UNITY_EDITOR
        // Assets の1つ上＝プロジェクトルート。Unity にインポートされない位置なので
        // 撮影してもアセットが増えない。
        return Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Screenshots");
#else
        return Path.Combine(Application.persistentDataPath, "Screenshots");
#endif
    }

    /// <summary>
    /// Play ストアの掲載要件を満たしているかを判定し、外れていれば警告する。
    /// 撮影そのものは止めない（要件外のサイズで撮りたい場合もあるため）。
    /// </summary>
    private static void WarnIfNotStoreCompliant(int width, int height, int byteLength)
    {
        int longSide = Mathf.Max(width, height);
        int shortSide = Mathf.Min(width, height);
        float aspect = (float)longSide / shortSide;

        if (aspect > MaxAspect)
        {
            Debug.LogWarning(
                $"[Screenshot] アスペクト比 {aspect:F2}:1 は Play ストアの上限 {MaxAspect}:1 を超えています。" +
                "このままでは掲載時に弾かれます。Game ビューを 1920×1080 にして撮り直してください。");
        }

        if (shortSide < MinSide || longSide > MaxSide)
        {
            Debug.LogWarning(
                $"[Screenshot] 解像度 {width}×{height} が Play ストアの範囲外です" +
                $"（各辺 {MinSide}〜{MaxSide}px）。");
        }

        const int maxBytes = 8 * 1024 * 1024;
        if (byteLength > maxBytes)
        {
            Debug.LogWarning(
                $"[Screenshot] ファイルサイズ {byteLength / 1024 / 1024}MB が Play ストアの上限 8MB を超えています。");
        }
    }
}
