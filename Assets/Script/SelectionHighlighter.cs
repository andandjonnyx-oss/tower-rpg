using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// コントローラー/キーボード操作時に、選択中の Selectable へ強調枠を被せる常駐システム。
/// GameStateAutoCreate と同じパターンで自動生成され、全シーンで動く。
///
/// 【設計方針（シーン改修ゼロ）】
///   ボタン1個ずつにコンポーネントを付けるのではなく、EventSystem の
///   currentSelectedGameObject を毎フレーム監視して枠を追従させる。
///   これによりシーン内の既存ボタン・動的生成ボタン（MagicSelector/図鑑/ショップ等）
///   の全てに自動適用され、シーンやプレハブへの改修が不要になる。
///
/// 【表示条件】
///   ナビゲーション入力（十字キー/スティック/矢印キー/Tab）を使った時だけ表示し、
///   マウス/タッチ操作に切り替わったら消す。タッチしか無いモバイル実機では
///   一切表示されない（＝モバイルの見た目に影響なし）。
///
/// 【フォールバック選択】
///   ナビ入力時に何も選択されていなければ、画面内の操作可能な Selectable から
///   自動で1つ選ぶ（無いと矢印キーが空振りする）。優先度は Button/Toggle ＞ その他、
///   同率なら画面の左上寄り。シーン毎の「最適な初期選択」を指定したい場合は、
///   シーン側で EventSystem.SetSelectedGameObject を呼べばそちらが優先される
///   （このクラスは「未選択のとき」しか介入しない）。
///
/// 【枠の実体】
///   実行時に生成する4本のバー（Image）。選択中ボタンの子として親付けし、
///   ストレッチアンカーで位置・サイズ・スクロールに自動追従する。
///   LayoutElement.ignoreLayout=true でレイアウトグループの計算から除外し、
///   raycastTarget=false でクリックを妨げない。
/// </summary>
public class SelectionHighlighter : MonoBehaviour
{
    // =========================================================
    // 見た目の設定
    // =========================================================
    // 【フォーカス表現の使い分け（2026-09-17 決定）】
    //   ・羊皮紙ボタン（大半）: 画像を黄色(255,255,0)に着色。枠は出さない。
    //   ・それ以外（額縁の無いセル、YES/NO、+N 等）: 赤枠を被せる。
    //   ・Tower/Battle/Main/Zukan/Title: さらにちびキャラを左右反転してボタン左に置く。
    private const float Thickness = 6f;   // 枠線の太さ
    private const float Padding = 8f;     // ボタン外形からの余白（外側に広げる）
    private static readonly Color FrameColor = new Color(1f, 0.15f, 0.15f); // 赤（羊皮紙以外）
    private static readonly Color ParchmentTint = new Color(1f, 1f, 0f);   // 黄（羊皮紙）
    private const float PulseSpeed = 2.5f;    // 明滅速度
    private const float PulseMinAlpha = 0.55f;

    /// <summary>羊皮紙スプライト（3ファイルとも同じ絵柄）。着色方式で強調する対象。</summary>
    private static readonly HashSet<string> ParchmentSpriteNames =
        new HashSet<string> { "youhisi", "yousihi", "consumeyou" };

    /// <summary>ちびキャラのナビカーソルを出すシーン。</summary>
    private static readonly HashSet<string> CursorScenes =
        new HashSet<string> { "Tower", "Battle", "Main", "Zukan", "Title" };

    private const float CursorGap = 12f;          // ボタン左端からの隙間
    private const float CursorHeightFactor = 1.3f; // ボタン高さに対する倍率
    private const float CursorMinHeight = 80f;
    private const float CursorMaxHeight = 160f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CreateIfNeeded()
    {
        if (FindAnyObjectByType<SelectionHighlighter>() != null) return;
        var go = new GameObject("SelectionHighlighter");
        go.AddComponent<SelectionHighlighter>();
        DontDestroyOnLoad(go);
    }

    /// <summary>直近の入力がナビゲーション系だったか（枠の表示条件）。</summary>
    private bool navMode;

    /// <summary>
    /// ナビ操作中かどうかの外部公開（ModalFocusScope がフォーカス封じ込めの
    /// 発動条件として参照する）。navMode と常に同値。
    /// </summary>
    public static bool NavigationMode { get; private set; }

    /// <summary>
    /// ナビ開始時に未選択なら優先的に選ぶ初期フォーカス（シーンが指定する）。
    /// 破棄済み/非表示/無効なら無視され、従来どおり左上寄りが選ばれる。
    /// 別シーンの残留参照はシーン遷移で破棄されるため自然に無効化される。
    /// </summary>
    public static Selectable PreferredFallback;

    /// <summary>
    /// true の間は「未選択時の自動フォールバック選択」を行わない（選択は空のまま）。
    /// 戦闘の敵ターン中など「既定ボタンが一時的に押せない」間に、まだ押せる別のボタン
    /// （魔法選択など）へフォーカスが流れて居座るのを防ぐ。シーンロードで自動的に false へ戻る。
    /// </summary>
    public static bool SuppressFallback;

    /// <summary>
    /// 既定フォーカスを設定し、ナビ操作中なら今すぐその Selectable を選択する。
    /// シーン到着直後は本クラスの Update が各シーンの Update より先に走ることがあり、
    /// PreferredFallback が設定される前に「左上のボタン」が選ばれて固定されてしまう
    /// （塔突入時・戦闘からの帰還時に魔法が選ばれていた問題）。既定ボタンが押せる
    /// 状態になった時点で各シーンから一度呼び、その選択を上書きするために使う。
    /// モーダル表示中は ModalFocusScope が優先されるため何もしない
    /// （閉じた時点で未選択になり、PreferredFallback へ自動で戻る）。
    /// </summary>
    public static void SelectNow(Selectable s)
    {
        PreferredFallback = s;
        FocusNow(s);
    }

    /// <summary>
    /// 既定フォーカス（PreferredFallback）は変えずに、ナビ操作中なら今すぐ s を選択する。
    /// 「ポップアップを閉じたら開いた元のボタンへ戻す」「別シーンからキャンセルで戻ったら
    /// 開いた元のボタンへ戻す」など、既定とは別の一時的な戻り先に使う。
    /// </summary>
    public static void FocusNow(Selectable s)
    {
        if (!NavigationMode || s == null) return;
        if (ModalFocusScope.Current != null) return;
        if (!s.isActiveAndEnabled || !s.interactable || s.navigation.mode == Navigation.Mode.None) return;

        var es = EventSystem.current;
        if (es != null && es.currentSelectedGameObject != s.gameObject)
            es.SetSelectedGameObject(s.gameObject);
    }

    private RectTransform frameRect;
    private Image[] bars;

    // 羊皮紙の着色（元色を控えて解除時に戻す）
    private Image tintedImage;
    private Color tintedOriginal;

    // ちびキャラカーソル
    private RectTransform cursorCanvasRect; // 最前面 Canvas（常駐オブジェクト配下）
    private RectTransform cursorRect;
    private Image cursorImage;
    private Sprite cursorSprite;      // 一度読み込んだら保持
    private bool cursorSpriteLoaded;
    private bool cursorScene;
    private readonly Vector3[] worldCorners = new Vector3[4];

    private void Awake()
    {
#if CONSOLE_BUILD
        // コンソール版は起動直後からコントローラー前提
        navMode = true;
#endif
        NavigationMode = navMode;

        cursorScene = CursorScenes.Contains(SceneManager.GetActiveScene().name);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        cursorScene = CursorScenes.Contains(scene.name);
        SuppressFallback = false; // シーン固有の抑止を持ち越さない
        // 前シーンのボタンごと破棄されている可能性があるので参照を捨てる
        tintedImage = null;
    }

    private void Update()
    {
        UpdateNavMode();

        var es = EventSystem.current;
        if (es == null)
        {
            HideAll();
            return;
        }

        // モーダル表示中のフォーカス管理は ModalFocusScope 側が行う（二重に選択しない）
        if (navMode && ModalFocusScope.Current == null && !SuppressFallback) EnsureSelection(es);

        UpdateFrame(es);
    }

    // =========================================================
    // 入力モード判定
    // =========================================================

    private void UpdateNavMode()
    {
        // ポインタ操作 → 枠を消す
        var m = Mouse.current;
        if (m != null && (m.leftButton.wasPressedThisFrame
                       || m.rightButton.wasPressedThisFrame
                       || m.delta.ReadValue().sqrMagnitude > 1f))
        {
            navMode = false;
        }
        var ts = Touchscreen.current;
        if (ts != null && ts.primaryTouch.press.wasPressedThisFrame)
        {
            navMode = false;
        }

        // ナビ操作 → 枠を出す（同フレームで両方来たらナビ優先）
        var k = Keyboard.current;
        if (k != null && (k.upArrowKey.wasPressedThisFrame
                       || k.downArrowKey.wasPressedThisFrame
                       || k.leftArrowKey.wasPressedThisFrame
                       || k.rightArrowKey.wasPressedThisFrame
                       || k.tabKey.wasPressedThisFrame))
        {
            navMode = true;
        }
        var g = Gamepad.current;
        if (g != null && (g.dpad.up.wasPressedThisFrame
                       || g.dpad.down.wasPressedThisFrame
                       || g.dpad.left.wasPressedThisFrame
                       || g.dpad.right.wasPressedThisFrame
                       || g.leftStick.ReadValue().sqrMagnitude > 0.25f))
        {
            navMode = true;
        }

        NavigationMode = navMode;
    }

    // =========================================================
    // フォールバック選択
    // =========================================================

    private void EnsureSelection(EventSystem es)
    {
        var cur = es.currentSelectedGameObject;
        if (cur != null && cur.activeInHierarchy)
        {
            var curSel = cur.GetComponent<Selectable>();
            if (curSel != null && curSel.interactable) return; // 有効な選択がある
        }

        var best = FindFallbackSelectable();
        if (best != null) es.SetSelectedGameObject(best.gameObject);
    }

    /// <summary>
    /// 画面内の操作可能な Selectable から自動選択の候補を1つ選ぶ。
    /// Button/Toggle を優先し、同率なら左上寄り（y が大きい→x が小さい）。
    /// スクロールバーは候補にしない（初期フォーカスとして不自然なため）。
    /// </summary>
    private Selectable FindFallbackSelectable()
    {
        // シーンが初期フォーカスを指定していればそれを最優先で使う
        if (PreferredFallback != null && PreferredFallback.isActiveAndEnabled
            && PreferredFallback.interactable
            && PreferredFallback.navigation.mode != Navigation.Mode.None)
            return PreferredFallback;

        Selectable best = null;
        bool bestIsPrimary = false;
        Vector3 bestPos = Vector3.zero;

        var all = Selectable.allSelectablesArray;
        for (int i = 0; i < all.Length; i++)
        {
            var s = all[i];
            if (s == null || !s.isActiveAndEnabled || !s.interactable) continue;
            if (s.navigation.mode == Navigation.Mode.None) continue;
            if (s is Scrollbar) continue;

            bool isPrimary = (s is Button) || (s is Toggle);
            Vector3 pos = s.transform.position;

            bool better;
            if (best == null) better = true;
            else if (isPrimary != bestIsPrimary) better = isPrimary;
            else if (!Mathf.Approximately(pos.y, bestPos.y)) better = pos.y > bestPos.y;
            else better = pos.x < bestPos.x;

            if (better)
            {
                best = s;
                bestIsPrimary = isPrimary;
                bestPos = pos;
            }
        }
        return best;
    }

    // =========================================================
    // 枠の表示・追従
    // =========================================================

    private void UpdateFrame(EventSystem es)
    {
        GameObject target = navMode ? es.currentSelectedGameObject : null;

        if (target != null)
        {
            var sel = target.GetComponent<Selectable>();
            if (sel == null || !sel.isActiveAndEnabled || !sel.interactable)
                target = null;
        }

        var rt = (target != null) ? target.transform as RectTransform : null;
        if (rt == null)
        {
            HideAll();
            return;
        }

        // 羊皮紙は着色、それ以外は赤枠
        var img = target.GetComponent<Image>();
        if (IsParchment(img))
        {
            HideFrame();
            ApplyTint(img);
        }
        else
        {
            ClearTint();
            ShowFrame(rt);
        }

        UpdateCursor(rt);
    }

    private static bool IsParchment(Image img)
    {
        return img != null && img.sprite != null && ParchmentSpriteNames.Contains(img.sprite.name);
    }

    // =========================================================
    // 羊皮紙の着色
    // =========================================================

    private void ApplyTint(Image img)
    {
        if (tintedImage == img) return;
        ClearTint();
        tintedImage = img;
        tintedOriginal = img.color;
        img.color = ParchmentTint;
    }

    private void ClearTint()
    {
        if (tintedImage != null) tintedImage.color = tintedOriginal;
        tintedImage = null;
    }

    // =========================================================
    // ちびキャラカーソル
    // =========================================================

    /// <summary>
    /// ちびキャラをフォーカス中ボタンの左に置く。
    /// 枠と違いボタンの子にはせず、専用の最前面 Canvas（自前の常駐オブジェクト配下）に置いて
    /// 毎フレーム座標を写す。ボタンの子にすると描画順がボタンの階層に縛られ、
    /// HUD やポップアップの裏に隠れる（2026-09-18 報告）。常駐側に置くことで
    /// シーン遷移で破棄されることもなくなる。
    /// </summary>
    private void UpdateCursor(RectTransform rt)
    {
        if (!cursorScene)
        {
            HideCursor();
            return;
        }
        if (cursorRect == null) BuildCursor();
        if (cursorImage.sprite == null)
        {
            HideCursor();
            return;
        }

        // ボタンの四隅 → スクリーン座標 → 最前面 Canvas のローカル座標
        var targetCanvas = rt.GetComponentInParent<Canvas>();
        Camera cam = (targetCanvas != null && targetCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            ? targetCanvas.worldCamera : null;
        rt.GetWorldCorners(worldCorners);
        float minX = float.MaxValue, minY = float.MaxValue, maxY = float.MinValue;
        for (int i = 0; i < 4; i++)
        {
            Vector2 sp = RectTransformUtility.WorldToScreenPoint(cam, worldCorners[i]);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(cursorCanvasRect, sp, null, out Vector2 lp);
            if (lp.x < minX) minX = lp.x;
            if (lp.y < minY) minY = lp.y;
            if (lp.y > maxY) maxY = lp.y;
        }

        float h = Mathf.Clamp((maxY - minY) * CursorHeightFactor, CursorMinHeight, CursorMaxHeight);
        cursorRect.sizeDelta = new Vector2(h, h);
        // ボタン左端の中央を基準点にし、左右反転（scale.x=-1）でピボットの左側へ描く
        cursorRect.anchoredPosition = new Vector2(minX - CursorGap, (minY + maxY) * 0.5f);

        if (!cursorRect.gameObject.activeSelf) cursorRect.gameObject.SetActive(true);
    }

    private void HideCursor()
    {
        if (cursorRect == null) return;
        if (cursorRect.gameObject.activeSelf) cursorRect.gameObject.SetActive(false);
    }

    /// <summary>
    /// 最前面 Canvas とカーソル（Image 1枚）を実行時生成する。スプライトは Resources の
    /// SelectionHighlighterConfig から取る（常駐オブジェクトはシーン参照を持てないため）。
    /// Canvas はシーン側と同じ CanvasScaler 設定（1920×1080・高さ基準）にして
    /// ローカル単位を揃える。レイキャスターは付けない（操作を妨げない）。
    /// </summary>
    private void BuildCursor()
    {
        if (!cursorSpriteLoaded)
        {
            cursorSpriteLoaded = true;
            var config = Resources.Load<SelectionHighlighterConfig>("SelectionHighlighterConfig");
            if (config == null || config.cursorSprite == null)
                Debug.LogWarning("[SelectionHighlighter] Resources/SelectionHighlighterConfig の cursorSprite が未設定。ちびキャラカーソルは表示しない");
            else
                cursorSprite = config.cursorSprite;
        }

        var canvasGo = new GameObject("SelectionCursorCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue; // 全シーンの Canvas より前面
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;
        cursorCanvasRect = (RectTransform)canvasGo.transform;

        var go = new GameObject("SelectionCursor", typeof(RectTransform), typeof(Image));
        cursorRect = (RectTransform)go.transform;
        cursorRect.SetParent(canvasGo.transform, false);
        cursorRect.anchorMin = new Vector2(0.5f, 0.5f);
        cursorRect.anchorMax = new Vector2(0.5f, 0.5f);
        cursorRect.pivot = new Vector2(0f, 0.5f);
        cursorRect.localScale = new Vector3(-1f, 1f, 1f);

        cursorImage = go.GetComponent<Image>();
        cursorImage.raycastTarget = false;
        cursorImage.preserveAspect = true;
        cursorImage.sprite = cursorSprite;

        go.SetActive(false);
    }

    private void HideAll()
    {
        HideFrame();
        ClearTint();
        HideCursor();
    }

    private void ShowFrame(RectTransform rt)
    {
        if (frameRect == null) BuildFrame();

        // 選択対象の子として親付けし、ストレッチで追従させる
        if (frameRect.parent != rt)
        {
            frameRect.SetParent(rt, false);
            frameRect.anchorMin = Vector2.zero;
            frameRect.anchorMax = Vector2.one;
            frameRect.offsetMin = new Vector2(-Padding, -Padding);
            frameRect.offsetMax = new Vector2(Padding, Padding);
            frameRect.SetAsLastSibling();
        }
        if (!frameRect.gameObject.activeSelf) frameRect.gameObject.SetActive(true);

        // 明滅（気付きやすさ優先。Time.timeScale の影響を受けない）
        float a = PulseMinAlpha + (1f - PulseMinAlpha)
                  * Mathf.PingPong(Time.unscaledTime * PulseSpeed, 1f);
        var c = FrameColor;
        c.a = a;
        for (int i = 0; i < bars.Length; i++)
            if (bars[i] != null) bars[i].color = c;
    }

    private void HideFrame()
    {
        if (frameRect == null) return;
        if (frameRect.gameObject.activeSelf) frameRect.gameObject.SetActive(false);
        // 対象ボタンごと破棄される事故を避けるため、非表示中は自分の下に退避する
        if (frameRect.parent != transform) frameRect.SetParent(transform, false);
    }

    /// <summary>
    /// 枠（4本バー）を実行時生成する。選択対象と一緒に破棄された場合は再生成される。
    /// </summary>
    private void BuildFrame()
    {
        var root = new GameObject("SelectionFrame", typeof(RectTransform), typeof(LayoutElement));
        frameRect = (RectTransform)root.transform;
        frameRect.SetParent(transform, false);

        // レイアウトグループに巻き込まれないようにする
        root.GetComponent<LayoutElement>().ignoreLayout = true;

        bars = new Image[4];
        // 上下バー: 横ストレッチ＋角を覆うため左右に太さ分はみ出す
        bars[0] = CreateBar("Top", new Vector2(0, 1), new Vector2(1, 1),
                            new Vector2(Thickness * 2f, Thickness));
        bars[1] = CreateBar("Bottom", new Vector2(0, 0), new Vector2(1, 0),
                            new Vector2(Thickness * 2f, Thickness));
        // 左右バー: 縦ストレッチ
        bars[2] = CreateBar("Left", new Vector2(0, 0), new Vector2(0, 1),
                            new Vector2(Thickness, 0));
        bars[3] = CreateBar("Right", new Vector2(1, 0), new Vector2(1, 1),
                            new Vector2(Thickness, 0));

        root.SetActive(false);
    }

    private Image CreateBar(string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 sizeDelta)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(frameRect, false);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = sizeDelta;

        var img = go.GetComponent<Image>();
        img.color = FrameColor;
        img.raycastTarget = false; // クリックを妨げない
        return img;
    }
}
