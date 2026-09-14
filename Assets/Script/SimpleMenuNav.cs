using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 専用コントローラーを持たない単純なメニュー画面用の汎用コントローラー対応。
/// 図鑑トップなど「ボタンが数個並ぶだけ」の画面の Canvas 等にアタッチする。
///
/// 【機能】
///   ・シーン内の操作可能な Button を画面上の縦位置順で縦ループ配線する
///     （SceneLink などで遷移する既存ボタンをそのまま流用。名前ではなく位置で並べる）。
///   ・初期フォーカスを最上段ボタンに設定。
///   ・キャンセルキー（Esc / パッドB）で cancelScene へ遷移する。
///     cancelButton を指定した場合はそのボタンの onClick を呼ぶ（＝戻ると同じ挙動）。
///
/// 【使い方】
///   図鑑トップ（Zukan）などの Canvas にアタッチし、cancelScene に "Main" を設定
///   （または cancelButton に戻るボタンを割り当て）。モバイルには影響しない
///   （キャンセルキーもナビ枠もタッチ操作では出ない）。
/// </summary>
public class SimpleMenuNav : MonoBehaviour
{
    [Tooltip("キャンセルキーで遷移するシーン名（cancelButton 未指定時に使用）")]
    [SerializeField] private string cancelScene = "Main";

    [Tooltip("キャンセルキーで押す戻るボタン（任意）。指定時は cancelScene より優先。")]
    [SerializeField] private Button cancelButton;

    private void Start()
    {
        StartCoroutine(WireAfterLayout());
    }

    private IEnumerator WireAfterLayout()
    {
        yield return null;
        WireNav();
    }

    private void WireNav()
    {
        var list = new List<Selectable>();
        foreach (var b in FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (b == null || !b.gameObject.activeInHierarchy || !b.interactable) continue;
            if (b.navigation.mode == Navigation.Mode.None) continue;
            list.Add(b);
        }
        // 画面上の Y 降順（上のボタンが先頭）
        list.Sort((a, b) => b.transform.position.y.CompareTo(a.transform.position.y));

        var loop = new List<Selectable>(list.Count);
        foreach (var b in list) loop.Add(b);
        ControllerNav.WireVerticalLoop(loop);

        SelectionHighlighter.PreferredFallback = loop.Count > 0 ? loop[0] : null;
    }

    private void Update()
    {
        var kb = Keyboard.current;
        var pad = Gamepad.current;
        if ((kb != null && kb.escapeKey.wasPressedThisFrame)
            || (pad != null && pad.buttonEast.wasPressedThisFrame))
        {
            if (cancelButton != null && cancelButton.interactable)
                cancelButton.onClick.Invoke();
            else if (!string.IsNullOrEmpty(cancelScene))
                SceneManager.LoadScene(cancelScene);
        }
    }
}
