using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// BattleSceneController のキー/コントローラーショートカットパート（partial class）。
/// コンソール版で「離れた場所にあるボタン」への到達手段としてキー呼び出しを提供する
/// （2026-09-13 決定）。マウス/タッチ用の既存ボタンは削除せずそのまま併存させる。
///
/// 【割り当て】
///   G キー / パッドのセレクトボタン  … ギブアップ確認の開閉
///   O キー / パッドの北ボタン(Y等)   … 戦闘ログ拡大の開閉
///   拡大中の ↑↓ キー / 十字キー上下  … 1ページ（ビューポート高さ）ぶんのページ送り
///
/// 【設計の要点】
///   - ショートカットは対応する既存ボタンの状態（activeInHierarchy / interactable）を
///     ミラーする。敵ターン中・敗北後の SetButtonsInteractable(false) や
///     クイズボス中の giveUpButton 無効化が、キー側にも自動で効く。
///     ボタンを経由しない専用の可否判定を作らないこと（判定が二重になり必ずズレる）。
///   - ログ拡大中は選択フォーカスを閉じるボタンに固定する。固定しないと
///     十字キーのナビゲーションがオーバーレイ裏の攻撃ボタン等へ移動し、
///     決定ボタンで誤操作できてしまう（ナビゲーションはレイキャストブロッカーを
///     素通りするため、パネルで覆うだけでは防げない）。
///   - Update() はこの partial にのみ定義している。他のパートに Update を
///     追加したくなったら、ここへ処理を足すこと（partial 越しの重複定義はコンパイルエラー）。
/// </summary>
public partial class BattleSceneController
{
    /// <summary>全文ログのページ送りに使う ScrollRect（fullLogContent の親から遅延取得）。</summary>
    private ScrollRect fullLogScrollRect;

    private void Update()
    {
        HandleKeyShortcuts();
    }

    private void HandleKeyShortcuts()
    {
        var kb = Keyboard.current;
        var pad = Gamepad.current;
        if (kb == null && pad == null) return;

        bool logOpen = fullLogPanel != null && fullLogPanel.activeSelf;

        // =========================================================
        // O / 北ボタン: ログ拡大の開閉トグル
        // =========================================================
        bool logToggle = (kb != null && kb.oKey.wasPressedThisFrame)
                      || (pad != null && pad.buttonNorth.wasPressedThisFrame);
        if (logToggle)
        {
            if (logOpen)
                CloseFullLog();
            else if (CanUseButton(fullLogOpenButton))
                OpenFullLog();
            return; // 同一フレームで他のショートカットと競合させない
        }

        // =========================================================
        // ログ拡大中: フォーカス固定＋上下キーでページ送り
        // （拡大中は G 等の他ショートカットを受け付けない）
        // =========================================================
        if (logOpen)
        {
            var es = EventSystem.current;
            if (fullLogCloseButton != null && es != null
                && es.currentSelectedGameObject != fullLogCloseButton.gameObject)
            {
                es.SetSelectedGameObject(fullLogCloseButton.gameObject);
            }

            bool pageUp = (kb != null && (kb.upArrowKey.wasPressedThisFrame
                                       || kb.pageUpKey.wasPressedThisFrame))
                       || (pad != null && pad.dpad.up.wasPressedThisFrame);
            bool pageDown = (kb != null && (kb.downArrowKey.wasPressedThisFrame
                                         || kb.pageDownKey.wasPressedThisFrame))
                         || (pad != null && pad.dpad.down.wasPressedThisFrame);

            if (pageUp) ScrollFullLogByPage(+1);
            else if (pageDown) ScrollFullLogByPage(-1);
            return;
        }

        // =========================================================
        // G / セレクトボタン: ギブアップ確認の開閉トグル
        // =========================================================
        bool giveUpKey = (kb != null && kb.gKey.wasPressedThisFrame)
                      || (pad != null && pad.selectButton.wasPressedThisFrame);
        if (giveUpKey)
        {
            if (giveUpPopup != null && giveUpPopup.activeSelf)
                OnGiveUpNo();
            else if (CanUseButton(giveUpButton))
                OnGiveUpClicked();
        }
    }

    /// <summary>対応するボタンが現在押せる状態か（ショートカットの可否はこれで判定する）。</summary>
    private static bool CanUseButton(Button button)
    {
        return button != null && button.gameObject.activeInHierarchy && button.interactable;
    }

    /// <summary>
    /// 全文ログを1ページ（ビューポート高さ）ぶん送る。
    /// direction: +1 = 上（前のログへ戻る）, -1 = 下（新しいログへ進む）。
    /// verticalNormalizedPosition は 1=先頭(上) / 0=末尾(下)。
    /// </summary>
    private void ScrollFullLogByPage(int direction)
    {
        if (fullLogScrollRect == null && fullLogContent != null)
            fullLogScrollRect = fullLogContent.GetComponentInParent<ScrollRect>(true);

        var sr = fullLogScrollRect;
        if (sr == null || sr.content == null) return;

        RectTransform viewport = (sr.viewport != null)
            ? sr.viewport
            : (RectTransform)sr.transform;

        float contentHeight = sr.content.rect.height;
        float viewHeight = viewport.rect.height;
        if (contentHeight <= viewHeight) return; // 全文が収まっている＝スクロール不要

        // 正規化座標での1ページぶん = ビューポート高さ / スクロール可能距離
        float step = viewHeight / (contentHeight - viewHeight);
        sr.verticalNormalizedPosition =
            Mathf.Clamp01(sr.verticalNormalizedPosition + step * direction);
    }
}
