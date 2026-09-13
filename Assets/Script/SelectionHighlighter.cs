using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
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
    private const float Thickness = 6f;   // 枠線の太さ
    private const float Padding = 8f;     // ボタン外形からの余白（外側に広げる）
    private static readonly Color FrameColor = new Color(1f, 0.85f, 0.2f); // 金色
    private const float PulseSpeed = 2.5f;    // 明滅速度
    private const float PulseMinAlpha = 0.55f;

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

    private RectTransform frameRect;
    private Image[] bars;

    private void Awake()
    {
#if CONSOLE_BUILD
        // コンソール版は起動直後からコントローラー前提
        navMode = true;
#endif
        NavigationMode = navMode;
    }

    private void Update()
    {
        UpdateNavMode();

        var es = EventSystem.current;
        if (es == null)
        {
            HideFrame();
            return;
        }

        // モーダル表示中のフォーカス管理は ModalFocusScope 側が行う（二重に選択しない）
        if (navMode && ModalFocusScope.Current == null) EnsureSelection(es);

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
            HideFrame();
            return;
        }

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
