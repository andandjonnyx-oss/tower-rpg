using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Itembox シーン用コントローラー。
/// 通常時: 使う/装備/捨てる。戻るでMainへ。
/// バトル中 (GameState.isInBattle): 使う/装備変更のみ。捨てる不可。
///   アイテム操作後は自動でバトルシーンへ戻り1ターン消費。
///   戻るボタンはターン消費なしでバトルシーンへ。
///
/// ボタン構築と効果適用は ItemActionHelper を経由し、
/// StorageContext と仕様を統一する。
///
/// 【多重入力ガード（busy）— 設計メモ】
///   ・遷移パス（バトル復帰の AfterAction / 戻るボタン）の同フレーム二重発火は
///     busy フラグが確実に防ぐ。busy=true のまま LoadScene するため、
///     同フレームの2発目は if (busy) return で弾かれ、二重 LoadScene や
///     ターン二重消費が起きない。← これが busy の本来の役割。
///   ・画面内操作（非バトル時の使う/捨てる等）の同フレーム同時押しを実際に
///     止めているのは busy ではなく ItemDetailPanel.Hide() の
///     detailRoot.SetActive(false)。1発目の AfterAction → Hide() でボタンが
///     非アクティブ化され、Unity が2発目クリックを isActiveAndEnabled==false で
///     抑止する。※この防壁は「操作ボタンが detailRoot の子であること」に依存する。
///     Hide() をやめたり階層を変えると静かに穴が開くので注意。
///   ・例外時のソフトロック対策として、各オペレーションは try/finally で囲い、
///     同シーンに留まる場合（AfterAction が遷移しなかった場合）は必ず busy を
///     解除する。これにより副作用中に NRE 等が出ても画面から出られなくなる
///     退行を防ぐ。
/// </summary>
public class ItemboxContext : MonoBehaviour, IItemContext
{
    [Header("Slots")]
    [SerializeField] private ItemSlotView[] slots;

    [Header("Detail Panel")]
    [SerializeField] private ItemDetailPanel detailPanel;

    [Header("Navigation")]
    [SerializeField] private Button backButton;
    [SerializeField] private string mainSceneName = "Main";

    /// <summary>バトル中かどうかのキャッシュ。</summary>
    private bool inBattle;

    /// <summary>
    /// 操作実行中ガード。オペレーション or 戻るが走っている間 true。
    /// 同フレームの多重入力（2連打・別ボタン同時押し）を遷移パスで弾く。
    /// 同シーンに留まる操作では try/finally で必ず解除する（ソフトロック防止）。
    /// </summary>
    private bool busy;

    private void Start()
    {
        inBattle = GameState.I != null && GameState.I.isInBattle;

        // スロットにコールバック登録
        if (slots != null)
        {
            foreach (var slot in slots)
            {
                if (slot != null)
                    slot.onClicked = OnSlotClicked;
            }
        }

        // 戻るボタン
        if (backButton != null)
            backButton.onClick.AddListener(OnBackClicked);

        // コントローラー対応（戦闘中のみ）: フォーカスはアイテム格子だけに限定する。
        // 戻るは十字キーで到達させず、キャンセルキー（Esc/パッドB）で行う（Update 参照）。
        if (inBattle && backButton != null)
        {
            var nav = backButton.navigation;
            nav.mode = Navigation.Mode.None;
            backButton.navigation = nav;
        }

        if (detailPanel != null) detailPanel.Hide();
        RefreshSlots();

        // コントローラー対応（通常時＝拠点から）: 倉庫と同じ方式。
        // スクロールバーをナビ対象外にし、レイアウト確定後にグリッド配線を組む。
        if (!inBattle)
        {
            foreach (var sb in FindObjectsByType<Scrollbar>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                ControllerNav.SetNavigationNone(sb);
            StartCoroutine(RebuildNavAfterLayout());
        }
    }

    private System.Collections.IEnumerator RebuildNavAfterLayout()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
        yield return null;
        RebuildNavigation();
    }

    /// <summary>前フレームの詳細表示状態（変化時だけナビ再構築）。</summary>
    private bool lastDetailShown;

    /// <summary>
    /// 通常時のコントローラー・ナビゲーション（倉庫と同方式・所持品グリッドのみ）。
    ///   非選択時: グリッド ⇔ 戻る
    ///   選択時:   グリッド ⇔ 詳細ウィンドウ（↓で最終的に戻るへ）
    /// 空スロットは ItemSlotView 側で None 済み。詳細は固定スロット配置で2D配線。
    /// </summary>
    private void RebuildNavigation()
    {
        if (inBattle) return;

        // 中身ありスロットを位置順に収集
        var cells = new List<Selectable>();
        if (slots != null)
        {
            foreach (var s in slots)
            {
                if (s == null || !s.gameObject.activeInHierarchy) continue;
                var sel = s.GetComponent<Selectable>();
                if (sel != null && sel.navigation.mode != Navigation.Mode.None)
                    cells.Add(sel);
            }
        }
        cells.Sort((a, b) =>
        {
            float ay = a.transform.position.y, by = b.transform.position.y;
            if (!Mathf.Approximately(ay, by)) return by.CompareTo(ay);
            return a.transform.position.x.CompareTo(b.transform.position.x);
        });

        var rows = new List<List<Selectable>>();
        List<Selectable> cur = null; float curY = 0f;
        foreach (var s in cells)
        {
            float y = s.transform.position.y;
            if (cur == null || Mathf.Abs(y - curY) > 20f)
            { cur = new List<Selectable>(); rows.Add(cur); curY = y; }
            cur.Add(s);
        }

        Selectable gridRep = (rows.Count > 0 && rows[0].Count > 0) ? rows[0][0] : null;

        // 中央（詳細＋戻る）を固定スロットで2D配線
        Selectable dTL = null, dTR = null, dBL = null, dBR = null;
        if (detailPanel != null && detailPanel.IsShown)
        {
            dTL = detailPanel.GetSlotButton(0);
            dTR = detailPanel.GetSlotButton(1);
            dBL = detailPanel.GetSlotButton(2);
            dBR = detailPanel.GetSlotButton(3);
        }
        Selectable back = (backButton != null && backButton.isActiveAndEnabled) ? backButton : null;
        Selectable centerEntry = dTL ?? dTR ?? dBL ?? dBR ?? back;

        if (dTL != null) ControllerNav.SetExplicit(dTL, null, dBL ?? dBR ?? back, gridRep, dTR);
        if (dTR != null) ControllerNav.SetExplicit(dTR, null, dBR ?? dBL ?? back, dTL ?? gridRep, null);
        if (dBL != null) ControllerNav.SetExplicit(dBL, dTL ?? dTR, back, gridRep, dBR);
        if (dBR != null) ControllerNav.SetExplicit(dBR, dTR ?? dTL, back, dBL ?? gridRep, null);
        if (back != null) ControllerNav.SetExplicit(back, dBL ?? dBR ?? dTL ?? dTR, null, gridRep, null);

        // グリッド2D配線。右端 → 中央（詳細/戻る）、左端 → なし。
        for (int r = 0; r < rows.Count; r++)
        {
            var row = rows[r];
            for (int i = 0; i < row.Count; i++)
            {
                var cell = row[i]; float x = cell.transform.position.x;
                Selectable left = (i > 0) ? row[i - 1] : null;
                Selectable right = (i < row.Count - 1) ? row[i + 1] : centerEntry;
                Selectable up = (r > 0) ? NearestByX(rows[r - 1], x) : null;
                Selectable down = (r < rows.Count - 1) ? NearestByX(rows[r + 1], x) : null;
                ControllerNav.SetExplicit(cell, up, down, left, right);
            }
        }

        SelectionHighlighter.PreferredFallback = gridRep != null ? gridRep : back;
    }

    private static Selectable NearestByX(List<Selectable> row, float x)
    {
        Selectable best = null; float bestD = float.MaxValue;
        for (int i = 0; i < row.Count; i++)
        {
            if (row[i] == null) continue;
            float d = Mathf.Abs(row[i].transform.position.x - x);
            if (d < bestD) { bestD = d; best = row[i]; }
        }
        return best;
    }

    // =========================================================
    // コントローラー/キーボード ショートカット（戦闘中のみ）
    //
    // 戦闘中の詳細パネルのボタンは最大2つ（使う／食べられる武器のみ+食べる）なので、
    // フォーカスはアイテム格子に固定したまま、キーでボタンを直接押す方式にする
    // （2026-09-13 決定）。通常時（拠点から開いた場合）は従来のナビゲーションのまま。
    //
    //   1キー / パッド西(X) … ボタン1（使う・装備等 = Primary スロット）
    //   2キー / パッド北(Y) … ボタン2（食べる = Secondary スロット）
    //   Esc  / パッド東(B) … 詳細を閉じる → もう一度でバトルへ戻る
    // =========================================================
    private void Update()
    {
        if (!inBattle)
        {
            UpdateNonBattle();
            return;
        }

        var kb = Keyboard.current;
        var pad = Gamepad.current;
        if (kb == null && pad == null) return;

        bool cancel = (kb != null && kb.escapeKey.wasPressedThisFrame)
                   || (pad != null && pad.buttonEast.wasPressedThisFrame);
        if (cancel)
        {
            if (detailPanel != null && detailPanel.IsShown)
                detailPanel.Hide();
            else
                OnBackClicked();
            return;
        }

        if (detailPanel == null || !detailPanel.IsShown) return;

        bool action1 = (kb != null && kb.digit1Key.wasPressedThisFrame)
                    || (pad != null && pad.buttonWest.wasPressedThisFrame);
        bool action2 = (kb != null && kb.digit2Key.wasPressedThisFrame)
                    || (pad != null && pad.buttonNorth.wasPressedThisFrame);

        if (action1) detailPanel.PressSlotButton(0);
        else if (action2) detailPanel.PressSlotButton(1);
    }

    /// <summary>
    /// 通常時のキー処理: 詳細開閉でナビ再構築、キャンセルは2段
    /// （詳細表示中は詳細を閉じる、なければ戻る）。
    /// </summary>
    private void UpdateNonBattle()
    {
        bool det = detailPanel != null && detailPanel.IsShown;
        if (det != lastDetailShown)
        {
            lastDetailShown = det;
            RebuildNavigation();
        }

        var kb = Keyboard.current;
        var pad = Gamepad.current;
        bool cancel = (kb != null && kb.escapeKey.wasPressedThisFrame)
                   || (pad != null && pad.buttonEast.wasPressedThisFrame);
        if (cancel)
        {
            if (det) detailPanel.Hide();
            else OnBackClicked();
        }
    }

    private void OnBackClicked()
    {
        // 操作中なら戻るを無視（操作との同時押し対策）。
        // 戻る自体は必ずシーン遷移するため、busy を立てたら解除しない。
        if (busy) return;
        busy = true;

        if (inBattle)
        {
            // ターン消費なしでバトルへ戻る
            if (GameState.I != null)
            {
                GameState.I.battleTurnConsumed = false;
                GameState.I.isInBattle = false;
            }
            SceneManager.LoadScene(GameState.I?.previousSceneName ?? "Battle");
        }
        else
        {
            SceneManager.LoadScene(mainSceneName);
        }
    }

    private void OnSlotClicked(ItemSlotView slot, InventoryItem invItem)
    {
        if (detailPanel == null) return;

        if (invItem == null)
        {
            detailPanel.Hide();
            if (!inBattle) RebuildNavigation();
            return;
        }

        detailPanel.Show(invItem, this, fromInventory: true);
        // 別アイテムへ切り替えても詳細ボタン構成に合わせて配線し直す（倉庫と同様）
        if (!inBattle) RebuildNavigation();
    }

    // =========================================================
    // IItemContext
    // =========================================================
    public List<DetailButtonDef> GetButtons(InventoryItem invItem, bool fromInventory)
    {
        var list = new List<DetailButtonDef>();
        if (invItem?.data == null) return list;

        switch (invItem.data.category)
        {
            case ItemCategory.Consumable:
                {
                    var btn = ItemActionHelper.BuildUseConsumableButton(
                        invItem, inBattle, () => UseConsumable(invItem));
                    if (btn != null) list.Add(btn);
                    break;
                }

            case ItemCategory.Weapon:
                {
                    list.Add(ItemActionHelper.BuildEquipButton(
                        invItem,
                        () => EquipWeapon(invItem),
                        () => UnequipWeapon(invItem)));

                    var eatBtn = ItemActionHelper.BuildEatWeaponButton(
                        invItem, () => EatWeapon(invItem));
                    if (eatBtn != null) list.Add(eatBtn);
                    break;
                }

            case ItemCategory.Magic:
                // Magic にはボタンなし
                break;
        }

        // バトル中は捨てる不可
        if (!inBattle)
        {
            list.Add(ItemActionHelper.BuildDiscardButton(
                invItem, () => DiscardItem(invItem)));
        }

        return list;
    }

    public void RefreshSlots()
    {
        if (slots == null) return;
        IReadOnlyList<InventoryItem> items = ItemBoxManager.Instance != null
            ? ItemBoxManager.Instance.GetItems() : null;
        int cap = (ItemBoxManager.Instance != null) ? ItemBoxManager.Instance.Capacity : slots.Length;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null) continue;

            if (i >= cap)
            {
                // 容量外のスロットは非表示
                slots[i].gameObject.SetActive(false);
                continue;
            }

            slots[i].gameObject.SetActive(true);
            InventoryItem invItem = (items != null && i < items.Count) ? items[i] : null;
            slots[i].SetItem(invItem);
        }
    }

    // =========================================================
    // Operations
    // =========================================================
    private void UseConsumable(InventoryItem invItem)
    {
        if (invItem?.data == null) return;

        // ★多重入力ガード（副作用の前）。
        if (busy) return;
        busy = true;
        bool transitioned = false;
        try
        {
            // RemoveItem 後は invItem.data が参照できなくなる可能性があるため、
            // 必要な値を事前に取得しておく
            string itemName = invItem.data.itemName;
            int healAmount = invItem.data.healAmount;
            bool isBossFeedItem = invItem.data.bossFeedItem;
            int spGain = invItem.data.statusPointGain;
            ItemData transformInto = invItem.data.transformInto;
            int transformChanceValue = invItem.data.transformChance;

            // 攻撃アイテム: ダメージ情報の事前取得
            int battleDmg = invItem.data.battleDamage;
            WeaponAttribute battleAttr = invItem.data.battleAttribute;
            DamageCategory battleDmgCat = invItem.data.battleDamageCategory;

            // ヘルパー経由で効果適用（HP/MP/状態異常/SP すべて含む）
            ItemActionHelper.ApplyConsumableEffects(invItem);

            // 攻撃アイテム: ダメージ情報を GameState に一時保存
            if (battleDmg > 0 && inBattle && GameState.I != null)
            {
                GameState.I.pendingBattleItemDamage = battleDmg;
                GameState.I.pendingBattleItemAttribute = (int)battleAttr;
                GameState.I.pendingBattleItemDamageCategory = (int)battleDmgCat;
                GameState.I.pendingBattleItemName = itemName;
                Debug.Log($"[Itembox] 攻撃アイテム使用: {itemName} dmg={battleDmg} attr={battleAttr} cat={battleDmgCat}");
            }

            // ボス餌付けアイテム: 即勝利フラグを GameState に保存
            if (isBossFeedItem && inBattle
                && BattleContext.EnemyMonster != null
                && BattleContext.EnemyMonster.acceptsFeedItem
                && GameState.I != null)
            {
                GameState.I.pendingBattleItemInstantWin = true;
                GameState.I.pendingBattleItemName = itemName;
                Debug.Log($"[Itembox] ボス餌付けアイテム使用: {itemName} → 即勝利フラグON");
            }

            // 元アイテムを消す
            ItemBoxManager.Instance?.RemoveItem(invItem);

            // 使用後にアイテム変化（確率判定対応）
            bool transformed = false;
            if (transformInto != null && ItemBoxManager.Instance != null)
            {
                // transformChance が 0 なら常に変化（従来互換）
                // 1以上なら確率判定
                bool success = (transformChanceValue <= 0)
                    || Random.Range(1, 101) <= transformChanceValue;

                if (success)
                {
                    ItemBoxManager.Instance.AddItem(transformInto);
                    Debug.Log($"[Itembox] アイテム変化: {itemName} → {transformInto.itemName}");
                    transformed = true;
                }
                else
                {
                    Debug.Log($"[Itembox] アイテム変化失敗: {itemName}（確率{transformChanceValue}%）");
                }
            }

            // ログメッセージの組み立て
            string logMsg = $"You は {itemName} を使った！";
            if (spGain > 0)
            {
                logMsg += $" ステータスポイント +{spGain}！";
            }
            if (transformed)
            {
                logMsg += $" {transformInto.itemName} を手に入れた！";
            }
            else if (transformInto != null)
            {
                // はずれ（transformInto が設定されていたが確率で失敗）
                logMsg += " …はずれ！";
            }

            transitioned = AfterAction(logMsg);
        }
        finally
        {
            // 同シーンに留まる場合のみ解除（遷移時は次シーンで新インスタンス）
            if (!transitioned) busy = false;
        }
    }

    // =========================================================
    // 武器を食べる
    // =========================================================

    private void EatWeapon(InventoryItem invItem)
    {
        if (invItem?.data == null) return;
        if (!invItem.data.isEdible) return;

        // ★多重入力ガード（副作用の前）。
        if (busy) return;
        busy = true;
        bool transitioned = false;
        try
        {
            // 事前取得（RemoveItem 後に参照できなくなるため）
            string itemName = invItem.data.itemName;
            int healAmount = invItem.data.eatHealAmount;
            ItemData transformInto = invItem.data.transformInto;
            int transformChanceValue = invItem.data.transformChance;

            // ヘルパー経由で装備解除 + 効果適用
            ItemActionHelper.UnequipIfNeeded(invItem);
            ItemActionHelper.ApplyEatWeaponEffects(invItem);

            // 元アイテムを消す
            ItemBoxManager.Instance?.RemoveItem(invItem);

            // 変化先アイテムを追加（確率判定対応）
            bool transformed = false;
            if (transformInto != null && ItemBoxManager.Instance != null)
            {
                bool success = (transformChanceValue <= 0)
                    || Random.Range(1, 101) <= transformChanceValue;

                if (success)
                {
                    ItemBoxManager.Instance.AddItem(transformInto);
                    Debug.Log($"[Itembox] 食べて変化: {itemName} → {transformInto.itemName}");
                    transformed = true;
                }
                else
                {
                    Debug.Log($"[Itembox] 食べて変化失敗: {itemName}（確率{transformChanceValue}%）");
                }
            }

            // ログ
            string logMsg = $"You は {itemName} を食べた！";
            if (healAmount > 0) logMsg += $" HP が {healAmount} 回復した！";
            if (transformed) logMsg += $" {transformInto.itemName} を手に入れた！";
            else if (transformInto != null) logMsg += " …はずれ！";
            transitioned = AfterAction(logMsg);
        }
        finally
        {
            if (!transitioned) busy = false;
        }
    }

    private void EquipWeapon(InventoryItem invItem)
    {
        // ★多重入力ガード（副作用の前）。装備と捨て/食べの同時押し対策。
        if (busy) return;
        busy = true;
        bool transitioned = false;
        try
        {
            ItemBoxManager.Instance?.EquipItem(invItem);
            string name = (invItem != null && invItem.data != null) ? invItem.data.itemName : "武器";
            transitioned = AfterAction($"You は {name} を装備した！");
        }
        finally
        {
            if (!transitioned) busy = false;
        }
    }

    private void UnequipWeapon(InventoryItem invItem)
    {
        // ★多重入力ガード（副作用の前）。
        if (busy) return;
        busy = true;
        bool transitioned = false;
        try
        {
            ItemBoxManager.Instance?.UnequipItem(invItem);
            string name = (invItem != null && invItem.data != null) ? invItem.data.itemName : "武器";
            transitioned = AfterAction($"You は {name} を外した！");
        }
        finally
        {
            if (!transitioned) busy = false;
        }
    }

    private void DiscardItem(InventoryItem invItem)
    {
        // ★多重入力ガード（副作用の前）。捨てと使う/装備の同時押し対策。
        if (busy) return;
        busy = true;
        bool transitioned = false;
        try
        {
            ItemBoxManager.Instance?.DiscardItem(invItem);
            transitioned = AfterAction("");
        }
        finally
        {
            if (!transitioned) busy = false;
        }
    }

    /// <summary>
    /// 操作後の共通処理。
    /// バトル中はバトルシーンへ遷移して true を返す（呼び出し側は busy を解除しない）。
    /// 非バトル時はセーブして false を返す（呼び出し側の finally で busy を解除）。
    /// </summary>
    /// <returns>シーン遷移したら true。</returns>
    private bool AfterAction(string logMessage)
    {
        if (detailPanel != null) detailPanel.Hide();
        RefreshSlots();

        // バトル中なら即座にバトルシーンへ戻る（1ターン消費）
        if (inBattle && GameState.I != null)
        {
            GameState.I.battleTurnConsumed = true;
            GameState.I.battleItemActionLog = logMessage;
            GameState.I.isInBattle = false;
            SceneManager.LoadScene(GameState.I.previousSceneName ?? "Battle");
            return true; // 遷移した → busy は保持
        }
        else
        {
            // 非バトル時: アイテム使用・装備変更の結果を即時セーブ
            SaveManager.Save();
            return false; // 同シーン継続 → 呼び出し側で busy 解除
        }
    }
}