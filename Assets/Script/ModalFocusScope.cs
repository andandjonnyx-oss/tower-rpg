using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// モーダルUI（ポップアップ）表示中、コントローラー/キーボードのフォーカスを
/// このオブジェクト配下に閉じ込めるコンポーネント。
/// ポップアップのルートに付けると SetActive の開閉に連動してスタックに積まれ、
/// 最前面のスコープだけが機能する（ポップアップの多重表示にも対応）。
///
/// 【機能】
///   1. フォーカス封じ込め: ナビ操作中、選択がスコープ外へ出たら配下の
///      Selectable へ引き戻す。uGUI のナビゲーションはレイキャストブロッカーを
///      素通りするため、パネルで覆うだけでは裏のボタンへ十字キーで移動できて
///      しまう。これを防ぐ唯一の汎用手段がこの引き戻し。
///   2. キャンセルキー: Esc / パッド東ボタン(B) で onCancel を呼ぶ。
///      onCancel が null のスコープはキャンセル不可（コンティニュー確認など、
///      誤爆されると困るポップアップに使う）。
///
/// 【使い方】
///   初期化時に一度 ModalFocusScope.Attach(popupRoot, onCancel) を呼ぶだけ。
///   以後は SetActive の開閉に自動追従する。非アクティブなルートに Attach しても
///   よい（表示された時点から機能する）。
/// </summary>
public class ModalFocusScope : MonoBehaviour
{
    private static readonly List<ModalFocusScope> stack = new List<ModalFocusScope>();

    /// <summary>現在最前面のスコープ（モーダル非表示なら null）。</summary>
    public static ModalFocusScope Current
        => stack.Count > 0 ? stack[stack.Count - 1] : null;

    /// <summary>キャンセルキー（Esc / パッドB）で呼ばれる。null ならキャンセル不可。</summary>
    public Action onCancel;

    /// <summary>
    /// root にスコープを付与（既にあれば onCancel だけ更新）する。
    /// </summary>
    public static ModalFocusScope Attach(GameObject root, Action onCancel)
    {
        if (root == null) return null;
        var scope = root.GetComponent<ModalFocusScope>();
        if (scope == null) scope = root.AddComponent<ModalFocusScope>();
        scope.onCancel = onCancel;
        return scope;
    }

    /// <summary>go がこのスコープ配下にあるか。</summary>
    public bool Contains(GameObject go)
        => go != null && go.transform.IsChildOf(transform);

    private void OnEnable()
    {
        stack.Add(this);
    }

    private void OnDisable()
    {
        stack.Remove(this);
    }

    private void Update()
    {
        if (Current != this) return; // 最前面のスコープだけが機能する

        // --- キャンセルキー ---
        if (onCancel != null)
        {
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            bool cancel = (kb != null && kb.escapeKey.wasPressedThisFrame)
                       || (pad != null && pad.buttonEast.wasPressedThisFrame);
            if (cancel)
            {
                onCancel.Invoke();
                return;
            }
        }

        // --- フォーカス封じ込め（ナビ操作中のみ。マウス/タッチには干渉しない） ---
        if (!SelectionHighlighter.NavigationMode) return;

        var es = EventSystem.current;
        if (es == null) return;

        var cur = es.currentSelectedGameObject;
        if (cur != null && cur.activeInHierarchy && Contains(cur))
        {
            var sel = cur.GetComponent<Selectable>();
            if (sel != null && sel.interactable) return; // スコープ内の有効な選択
        }

        var fallback = FindFirstSelectable();
        if (fallback != null) es.SetSelectedGameObject(fallback.gameObject);
    }

    /// <summary>
    /// スコープ配下から最初の操作可能な Selectable を探す（Button/Toggle 優先、
    /// スクロールバーとナビ無効は除外）。
    /// </summary>
    private Selectable FindFirstSelectable()
    {
        Selectable first = null;
        var all = GetComponentsInChildren<Selectable>(false);
        for (int i = 0; i < all.Length; i++)
        {
            var s = all[i];
            if (s == null || !s.isActiveAndEnabled || !s.interactable) continue;
            if (s.navigation.mode == Navigation.Mode.None) continue;
            if (s is Scrollbar) continue;
            if (s is Button || s is Toggle) return s; // 優先種が見つかったら即決
            if (first == null) first = s;
        }
        return first;
    }
}
