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

    /// <summary>6コマンドの巡回順（Start 後に一度だけ構築。実配線は RefreshCommandNavigation）。</summary>
    private readonly List<Selectable> commandLoop = new List<Selectable>();

    /// <summary>RefreshCommandNavigation 用の作業リスト（毎フレームのGCアロケーション回避）。</summary>
    private static readonly List<Selectable> usableCommands = new List<Selectable>();

    private void Update()
    {
        // Start でのボタン配線が終わった後に一度だけ構成する
        if (!navConfigured)
        {
            navConfigured = true;
            ConfigureNavigationAndScopes();
        }

        // ボタンの使用可否（武器スキルのクールタイム・敵ターン中の無効化等）は
        // 頻繁に変わるため、巡回配線は毎フレーム張り直す
        RefreshCommandNavigation();

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
        // --- 6コマンドの巡回順を登録（実配線は毎フレームの RefreshCommandNavigation） ---
        commandLoop.Clear();
        if (magicSelector != null && magicSelector.SelectedButton != null)
            commandLoop.Add(magicSelector.SelectedButton);
        if (magicButton != null) commandLoop.Add(magicButton);
        if (itemButton != null) commandLoop.Add(itemButton);
        if (skillButton != null) commandLoop.Add(skillButton);
        if (attackButton != null) commandLoop.Add(attackButton);
        if (defendButton != null) commandLoop.Add(defendButton);

        // --- 十字キーで到達させないボタン（キー呼び出し専用） ---
        SetNavigationNone(giveUpButton);
        SetNavigationNone(fullLogOpenButton);

        // --- モーダルスコープ（表示中はフォーカスを内側に限定） ---
        ModalFocusScope.Attach(giveUpPopup, OnGiveUpNo);    // Esc/B = いいえ
        ModalFocusScope.Attach(continuePopup, null);        // 誤爆防止のためキャンセル不可
        ModalFocusScope.Attach(fullLogPanel, CloseFullLog); // Esc/B = 閉じる
    }

    /// <summary>
    /// 6コマンドの縦ループを「いま押せるボタンだけ」で張り直す（毎フレーム実行）。
    /// 武器スキルのクールタイム等で interactable が切り替わるため、固定配線だと
    /// 使用不能ボタンで巡回が堰き止められ、その先の攻撃/防御に到達できなくなる
    /// （2026-09-14 報告）。使用不能ボタンはナビゲーションから完全に外し、
    /// 押せるボタンだけで縦ループを組み直すことで自然にスキップされる。
    ///
    /// 既定フォーカス（2026-09-16）: 武器スキル。スキルがクールタイム中など押せない時は
    /// 攻撃。敵ターン中は全ボタンが無効になって選択が外れるため、自ターンが戻るたびに
    /// SelectionHighlighter がこの既定へ戻す（決定ボタン連打でスキル/攻撃を繰り返せる）。
    /// </summary>
    private void RefreshCommandNavigation()
    {
        ControllerNav.WireVerticalLoopUsable(commandLoop, usableCommands);

        Selectable preferred = CanUseButton(skillButton) ? (Selectable)skillButton : attackButton;
        SelectionHighlighter.PreferredFallback = preferred;

        // 自ターン判定は攻撃ボタンの可否をミラーする（敵ターン中・戦闘終了後は false）。
        bool playerTurn = CanUseButton(attackButton);

        // 敵ターン中は自動フォールバックを止める。止めないと、行動直後に選択が外れた瞬間、
        // まだ押せる魔法選択ボタンへフォーカスが流れ、次ターンもそこに居座る
        // （2ターン目以降の初期位置が魔法になる問題）。決定連打の誤爆防止にもなる。
        SelectionHighlighter.SuppressFallback = !playerTurn;

        // 抑止が効く前のフレームでコマンド（魔法選択など）へ流れた選択も外す。
        // 敵ターン中にコマンドへフォーカスが残ると、決定連打で魔法一覧が開いてしまう。
        if (!playerTurn)
        {
            var es = EventSystem.current;
            var cur = es != null ? es.currentSelectedGameObject : null;
            if (cur != null && ModalFocusScope.Current == null)
            {
                for (int i = 0; i < commandLoop.Count; i++)
                {
                    if (commandLoop[i] != null && commandLoop[i].gameObject == cur)
                    {
                        es.SetSelectedGameObject(null);
                        break;
                    }
                }
            }
        }

        // 自ターンが始まるたび（シーン到着直後を含む）に既定を明示的に選び直す。
        //   ターン開始時                     → スキル（CT中は攻撃）
        //   アイテム画面をキャンセルして復帰 → アイテム（開いた元へ戻す。ターンは進んでいない）
        //   魔法一覧を閉じた時（決定/キャンセル）→ 魔法選択ボタン（MagicSelector.CloseList が担当）
        if (playerTurn && !wasPlayerTurn)
        {
            if (returnedFromItemCancel && CanUseButton(itemButton))
                SelectionHighlighter.FocusNow(itemButton);
            else
                SelectionHighlighter.SelectNow(preferred);
            returnedFromItemCancel = false;
        }
        wasPlayerTurn = playerTurn;
    }

    /// <summary>アイテム画面からターン消費なしで戻った直後か（Start が立て、最初の自ターン開始で消費）。</summary>
    private bool returnedFromItemCancel;

    /// <summary>前フレームが自ターン（コマンド入力可）だったか。自ターン開始の検出用。</summary>
    private bool wasPlayerTurn;

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
