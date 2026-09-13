using System.Collections.Generic;
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

    /// <summary>ナビ配線とモーダルスコープの構成が済んだか（シーンインスタンス毎に初回1回）。</summary>
    private bool navConfigured;

    private void Update()
    {
        // Start でのボタン配線が終わった後に一度だけ構成する
        if (!navConfigured)
        {
            navConfigured = true;
            ConfigureNavigationAndScopes();
        }

        HandleKeyShortcuts();
    }

    /// <summary>
    /// 十字キーの巡回を右側6コマンドの縦ループに限定し、
    /// 各ポップアップへモーダルフォーカススコープを付与する（2026-09-13 決定）。
    ///
    ///   巡回: 魔法一覧 → 魔法 → アイテム → スキル → 攻撃 → 防御 → 先頭へ戻る
    ///   ループ外: ギブアップ = G / セレクト、ログ拡大 = O / 北ボタン（キー専用）
    ///   モーダル: ギブアップ確認（Esc/B=いいえ）、コンティニュー確認（キャンセル不可）、
    ///             ログ拡大（Esc/B=閉じる）
    /// </summary>
    private void ConfigureNavigationAndScopes()
    {
        // --- 6コマンドの縦ループ（明示ナビゲーション） ---
        var loop = new List<Selectable>();
        if (magicSelector != null && magicSelector.SelectedButton != null)
            loop.Add(magicSelector.SelectedButton);
        if (magicButton != null) loop.Add(magicButton);
        if (itemButton != null) loop.Add(itemButton);
        if (skillButton != null) loop.Add(skillButton);
        if (attackButton != null) loop.Add(attackButton);
        if (defendButton != null) loop.Add(defendButton);

        for (int i = 0; i < loop.Count; i++)
        {
            var nav = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = loop[(i - 1 + loop.Count) % loop.Count],
                selectOnDown = loop[(i + 1) % loop.Count],
                // 左右は割り当てない（6コマンドの縦ループのみ）
            };
            loop[i].navigation = nav;
        }

        // --- 十字キーで到達させないボタン（キー呼び出し専用） ---
        SetNavigationNone(giveUpButton);
        SetNavigationNone(fullLogOpenButton);

        // --- モーダルスコープ（表示中はフォーカスを内側に限定） ---
        ModalFocusScope.Attach(giveUpPopup, OnGiveUpNo);    // Esc/B = いいえ
        ModalFocusScope.Attach(continuePopup, null);        // 誤爆防止のためキャンセル不可
        ModalFocusScope.Attach(fullLogPanel, CloseFullLog); // Esc/B = 閉じる
    }

    private static void SetNavigationNone(Selectable s)
    {
        if (s == null) return;
        var nav = s.navigation;
        nav.mode = Navigation.Mode.None;
        s.navigation = nav;
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
