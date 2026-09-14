using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Itemsouko（倉庫）シーン用コントローラー。
/// 所持品と倉庫の2列を表示し、使う/装備/捨てる/預ける/引き出すの操作を提供する。
/// 倉庫側スロットはアイテム数に応じて Prefab から動的に生成される。
///
/// ボタン構築と効果適用は ItemActionHelper を経由し、
/// ItemboxContext と仕様を統一する。
///
/// 【多重入力ガード（busy）— 設計メモ】
///   ・倉庫シーンは常に同シーンに留まる（戻る以外は遷移しない）。
///   ・画面内操作（使う/装備/食べる/捨てる/預ける/引き出す）の同フレーム同時押しを
///     実際に止めているのは busy ではなく ItemDetailPanel.Hide() の
///     detailRoot.SetActive(false)。1発目の AfterAction → Hide() でボタンが
///     非アクティブ化され、Unity が2発目クリックを isActiveAndEnabled==false で
///     抑止する。※この防壁は「操作ボタンが detailRoot の子であること」に依存する。
///   ・busy は主に戻るボタンと操作の同時押し（遷移パス）を弾く役割。
///   ・例外時のソフトロック対策として、各オペレーションは try/finally で囲い、
///     同シーンに留まる場合は必ず busy を解除する。副作用中に NRE 等が出ても
///     画面から出られなくなる退行を防ぐ。
/// </summary>
public class StorageContext : MonoBehaviour, IItemContext
{
    // =========================================================
    // 戻り先シーンの動的切り替え（追加）
    // =========================================================
    /// <summary>
    /// 倉庫シーンの「戻る」ボタンで遷移するシーン名。
    /// Tower から開いた場合は "Tower" にセットされる。
    /// Main から開いた場合は "Main"（デフォルト）。
    /// 倉庫シーンを開く側で事前にセットすること。
    /// </summary>
    public static string ReturnScene = "Main";

    [Header("Inventory Slots (Left) - Inspector でアサイン")]
    [SerializeField] private ItemSlotView[] inventorySlots;

    [Header("Storage Slots - 動的生成")]
    [Tooltip("スロットの Prefab（ItemSlotView がアタッチ済み）")]
    [SerializeField] private ItemSlotView slotPrefab;

    [Tooltip("スロットを生成する親 Transform（GridLayoutGroup + ContentSizeFitter をアタッチ）")]
    [SerializeField] private Transform storageContent;

    [Tooltip("1行あたりの列数")]
    [SerializeField] private int columns = 4;

    [Tooltip("行数（最大容量 = columns × rows）")]
    [SerializeField] private int rows = 25;

    [Header("Detail Panel")]
    [SerializeField] private ItemDetailPanel detailPanel;

    [Header("Navigation")]
    [SerializeField] private Button backButton;
    [SerializeField] private string mainSceneName = "Main";

    // 現在生成されているスロット
    private List<ItemSlotView> storageSlotList = new List<ItemSlotView>();

    private InventoryItem selectedItem;

#pragma warning disable CS0414
    private bool selectedFromInventory;
#pragma warning restore CS0414

    /// <summary>
    /// 操作実行中ガード。オペレーション or 戻るが走っている間 true。
    /// 倉庫シーンは常に同シーンに留まるため、AfterAction() は常に false を返し、
    /// 各オペレーションの try/finally で必ず解除される（ソフトロック防止）。
    /// 戻るボタンのみ遷移するため、立てたら解除しない。
    /// </summary>
    private bool busy;

    private void Awake()
    {
        // StorageManager の容量を設定
        int totalCapacity = columns * rows;
        if (StorageManager.Instance != null)
            StorageManager.Instance.SetCapacity(totalCapacity);
    }

    private void Start()
    {
        // 所持品スロットにコールバック登録
        if (inventorySlots != null)
            foreach (var s in inventorySlots)
                if (s != null) s.onClicked = OnInventorySlotClicked;

        if (backButton != null)
        {
            string returnTo = string.IsNullOrEmpty(ReturnScene) ? mainSceneName : ReturnScene;
            cancelReturnScene = returnTo;
            backButton.onClick.AddListener(() => OnBackClicked(returnTo));

            // ボタンラベルを戻り先に応じて変更
            var label = backButton.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (label != null)
            {
                label.text = "戻る";
            }

            ReturnScene = "Main";
        }

        if (detailPanel != null) detailPanel.Hide();
        RefreshSlots(); // 末尾で RebuildNavigation を呼ぶ
        lastDetailShown = false;
    }

    // =========================================================
    // コントローラー/キーボード（2026-09-15）
    // =========================================================

    /// <summary>Start で確定した戻り先シーン（キャンセルキーからの帰還に使う）。</summary>
    private string cancelReturnScene = "Main";

    /// <summary>前フレームの詳細パネル表示状態（変化時だけナビ再構築する）。</summary>
    private bool lastDetailShown;

    private void Update()
    {
        // 詳細パネルの開閉でナビ構造（中央列）が変わるので、その時だけ組み直す
        bool det = detailPanel != null && detailPanel.IsShown;
        if (det != lastDetailShown)
        {
            lastDetailShown = det;
            RebuildNavigation();
        }

        // キャンセルキー（Esc / パッドB）:
        //   詳細表示中は詳細を閉じる、そうでなければ戻る（＝戻るボタンと同じ）。
        //   リセット等のモーダルは無いが、他モーダルが前面のときは介入しない。
        if (ModalFocusScope.Current == null)
        {
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            bool cancel = (kb != null && kb.escapeKey.wasPressedThisFrame)
                       || (pad != null && pad.buttonEast.wasPressedThisFrame);
            if (cancel)
            {
                if (det) detailPanel.Hide();
                else OnBackClicked(cancelReturnScene);
            }
        }
    }

    /// <summary>
    /// 倉庫画面のコントローラー・ナビゲーションを組み直す。
    ///
    ///   非選択時: 所持品グリッド ⇔ 戻る ⇔ 倉庫グリッド
    ///   選択時:   所持品 ⇔ 詳細ウィンドウ（↓で最終的に戻るへ）⇔ 倉庫
    ///
    /// ・空スロットは ItemSlotView 側で Navigation.None 済み → ここでは中身ありだけ扱う。
    /// ・スクロールバーはマウス用に残すがナビ対象からは外す。
    /// ・+ステータス画面等と同じく ControllerNav の明示配線を使う。
    /// </summary>
    private void RebuildNavigation()
    {
        // スクロールバーはナビ対象外（マウス操作・位置表示のためだけに残す）。
        // StorageContext がスクロールバーの親とは限らないためシーン全体から探す。
        foreach (var sb in FindObjectsByType<Scrollbar>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            ControllerNav.SetNavigationNone(sb);

        // 中身ありスロットだけ収集（空スロットは None 済みなので除外される）
        var inv = CollectUsableSlots(inventorySlots);
        var stor = CollectUsableSlots(storageSlotList);

        Selectable invRep = inv.Count > 0 ? inv[0] : null;
        Selectable storRep = stor.Count > 0 ? stor[0] : null;

        // 中央（詳細ウィンドウ＋戻る）を固定スロット配置で2D配線する。
        //   詳細ボタンは 0=左上 1=右上 2=左下 3=右下（ItemDetailPanel の役割スロット）。
        //   位置計算に頼らず index で組むため、上下左右が視覚と必ず一致する。
        Selectable dTL = null, dTR = null, dBL = null, dBR = null;
        if (detailPanel != null && detailPanel.IsShown)
        {
            dTL = detailPanel.GetSlotButton(0);
            dTR = detailPanel.GetSlotButton(1);
            dBL = detailPanel.GetSlotButton(2);
            dBR = detailPanel.GetSlotButton(3);
        }
        Selectable back = (backButton != null && backButton.isActiveAndEnabled) ? backButton : null;

        // グリッドから中央へ入る先は常に左上（無ければ順に代替）。
        // 倉庫（右）から左キーで戻ったときも左上に入る、という要望に合わせる。
        Selectable centerEntry = dTL ?? dTR ?? dBL ?? dBR ?? back;

        // 左端 → 所持品, 右端 → 倉庫。同列で上下、隣接ボタンで左右。
        if (dTL != null)
            ControllerNav.SetExplicit(dTL, null, dBL ?? dBR ?? back, invRep, dTR ?? storRep);
        if (dTR != null)
            ControllerNav.SetExplicit(dTR, null, dBR ?? dBL ?? back, dTL ?? invRep, storRep);
        if (dBL != null)
            ControllerNav.SetExplicit(dBL, dTL ?? dTR, back, invRep, dBR ?? storRep);
        if (dBR != null)
            ControllerNav.SetExplicit(dBR, dTR ?? dTL, back, dBL ?? invRep, storRep);
        if (back != null)
            ControllerNav.SetExplicit(back, dBL ?? dBR ?? dTL ?? dTR, null, invRep, storRep);

        // 左グリッド（所持品）: 右端 → 中央、左端 → なし
        WireGrid(inv, columns, leftPanel: true, centerEntry: centerEntry);
        // 右グリッド（倉庫）: 左端 → 中央、右端 → なし
        WireGrid(stor, columns, leftPanel: false, centerEntry: centerEntry);

        // コントローラーの初期フォーカスは所持品の先頭アイテム（無ければ戻る）
        SelectionHighlighter.PreferredFallback = invRep != null ? invRep
            : (backButton != null ? backButton : null);
    }

    private static List<Selectable> CollectUsableSlots(IReadOnlyList<ItemSlotView> slots)
    {
        var list = new List<Selectable>();
        if (slots == null) return list;
        for (int i = 0; i < slots.Count; i++)
        {
            var s = slots[i];
            if (s == null || !s.gameObject.activeInHierarchy) continue;
            var sel = s.GetComponent<Selectable>();
            // 空スロットは None なので中身ありだけ拾う
            if (sel != null && sel.navigation.mode != Navigation.Mode.None)
                list.Add(sel);
        }
        return list;
    }

    /// <summary>
    /// 4列グリッドを明示配線する。行内で左右、列で上下。
    /// leftPanel=true は右端が中央へ、false（倉庫）は左端が中央へ抜ける。
    /// </summary>
    private static void WireGrid(List<Selectable> cells, int cols, bool leftPanel, Selectable centerEntry)
    {
        if (cols < 1) cols = 1;
        int n = cells.Count;
        for (int i = 0; i < n; i++)
        {
            int col = i % cols;
            Selectable up = (i - cols >= 0) ? cells[i - cols] : null;
            Selectable down = (i + cols < n) ? cells[i + cols] : null;

            Selectable left, right;
            if (leftPanel)
            {
                left = (col > 0) ? cells[i - 1] : null;
                bool hasRight = (col < cols - 1) && (i + 1 < n);
                right = hasRight ? cells[i + 1] : centerEntry; // 右端 → 中央
            }
            else
            {
                left = (col > 0) ? cells[i - 1] : centerEntry; // 左端 → 中央
                bool hasRight = (col < cols - 1) && (i + 1 < n);
                right = hasRight ? cells[i + 1] : null;
            }
            ControllerNav.SetExplicit(cells[i], up, down, left, right);
        }
    }

    /// <summary>
    /// 戻るボタン。操作中なら無視（操作との同時押し対策）。
    /// 遷移するため busy を立てたら解除しない。
    /// </summary>
    private void OnBackClicked(string returnTo)
    {
        if (busy) return;
        busy = true;
        SceneManager.LoadScene(returnTo);
    }

    private void OnInventorySlotClicked(ItemSlotView slot, InventoryItem invItem)
    {
        if (invItem == null) { detailPanel?.Hide(); return; }
        selectedItem = invItem;
        selectedFromInventory = true;
        detailPanel?.Show(invItem, this, fromInventory: true);
    }

    private void OnStorageSlotClicked(ItemSlotView slot, InventoryItem invItem)
    {
        if (invItem == null) { detailPanel?.Hide(); return; }
        selectedItem = invItem;
        selectedFromInventory = false;
        detailPanel?.Show(invItem, this, fromInventory: false);
    }

    // =========================================================
    // IItemContext
    // =========================================================
    public List<DetailButtonDef> GetButtons(InventoryItem invItem, bool fromInventory)
    {
        var list = new List<DetailButtonDef>();
        if (invItem?.data == null) return list;

        if (fromInventory)
            BuildInventoryButtons(invItem, list);
        else
            BuildStorageButtons(invItem, list);

        return list;
    }

    private void BuildInventoryButtons(InventoryItem invItem, List<DetailButtonDef> list)
    {
        // 倉庫画面は常に非バトル (inBattle = false)
        switch (invItem.data.category)
        {
            case ItemCategory.Consumable:
                {
                    var btn = ItemActionHelper.BuildUseConsumableButton(
                        invItem, inBattle: false, () => UseConsumableFromInventory(invItem));
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
                        invItem, () => EatWeaponFromInventory(invItem));
                    if (eatBtn != null) list.Add(eatBtn);
                    break;
                }
            case ItemCategory.Magic:
                break;
        }

        // 捨てるボタン（cannotDiscard チェック込み）
        list.Add(ItemActionHelper.BuildDiscardButton(
            invItem, () => DiscardFromInventory(invItem)));

        // 預けるボタン
        bool canDeposit = StorageManager.Instance != null && !StorageManager.Instance.IsFull;
        list.Add(new DetailButtonDef("預ける", () => DepositItem(invItem),
            canDeposit, role: ButtonRole.Transfer));
    }

    private void BuildStorageButtons(InventoryItem invItem, List<DetailButtonDef> list)
    {
        // 倉庫画面は常に非バトル (inBattle = false)
        if (invItem.data.category == ItemCategory.Consumable)
        {
            var btn = ItemActionHelper.BuildUseConsumableButton(
                invItem, inBattle: false, () => UseConsumableFromStorage(invItem));
            if (btn != null) list.Add(btn);
        }

        if (invItem.data.category == ItemCategory.Weapon)
        {
            var eatBtn = ItemActionHelper.BuildEatWeaponButton(
                invItem, () => EatWeaponFromStorage(invItem));
            if (eatBtn != null) list.Add(eatBtn);
        }

        // 捨てるボタン（cannotDiscard チェック込み）
        list.Add(ItemActionHelper.BuildDiscardButton(
            invItem, () => DiscardFromStorage(invItem)));

        // 引き出すボタン
        bool canWithdraw = ItemBoxManager.Instance != null && !ItemBoxManager.Instance.IsFull;
        list.Add(new DetailButtonDef("引き出す", () => WithdrawItem(invItem),
            canWithdraw, role: ButtonRole.Transfer));
    }

    public void RefreshSlots()
    {
        RefreshInventorySide();
        RefreshStorageSide();
        RebuildNavigation();
    }

    private void RefreshInventorySide()
    {
        if (inventorySlots == null) return;
        IReadOnlyList<InventoryItem> items = ItemBoxManager.Instance?.GetItems();
        int cap = (ItemBoxManager.Instance != null) ? ItemBoxManager.Instance.Capacity : inventorySlots.Length;

        for (int i = 0; i < inventorySlots.Length; i++)
        {
            if (inventorySlots[i] == null) continue;

            if (i >= cap)
            {
                inventorySlots[i].gameObject.SetActive(false);
                continue;
            }

            inventorySlots[i].gameObject.SetActive(true);
            InventoryItem invItem = (items != null && i < items.Count) ? items[i] : null;
            inventorySlots[i].SetItem(invItem);
        }
    }

    /// <summary>
    /// 倉庫側スロットをアイテム数に合わせて動的に生成/削除する。
    /// アイテムがある分だけスロットを表示し、空スロットは作らない。
    /// </summary>
    private void RefreshStorageSide()
    {
        if (slotPrefab == null || storageContent == null) return;

        IReadOnlyList<InventoryItem> items = StorageManager.Instance?.GetItems();
        int itemCount = (items != null) ? items.Count : 0;

        // 足りないスロットを追加
        while (storageSlotList.Count < itemCount)
        {
            ItemSlotView slot = Instantiate(slotPrefab, storageContent);
            slot.gameObject.name = $"StorageSlot_{storageSlotList.Count}";
            slot.onClicked = OnStorageSlotClicked;
            storageSlotList.Add(slot);
        }

        // 余分なスロットを削除
        while (storageSlotList.Count > itemCount)
        {
            int last = storageSlotList.Count - 1;
            Destroy(storageSlotList[last].gameObject);
            storageSlotList.RemoveAt(last);
        }

        // アイテムをスロットにセット
        for (int i = 0; i < itemCount; i++)
        {
            storageSlotList[i].SetItem(items[i]);
        }
    }

    // =========================================================
    // Operations
    // =========================================================

    private void UseConsumableFromInventory(InventoryItem invItem)
    {
        // ★多重入力ガード（副作用の前）。
        if (busy) return;
        busy = true;
        try
        {
            ItemActionHelper.ApplyConsumableEffects(invItem);
            ItemData transformInto = invItem.data?.transformInto;
            int transformChanceValue = invItem.data != null ? invItem.data.transformChance : 0;
            ItemBoxManager.Instance?.RemoveItem(invItem);

            if (transformInto != null && ItemBoxManager.Instance != null)
            {
                bool success = (transformChanceValue <= 0) || Random.Range(1, 101) <= transformChanceValue;
                if (success)
                {
                    ItemBoxManager.Instance.AddItem(transformInto);
                    Debug.Log($"[Storage] アイテム変化（所持品）: → {transformInto.itemName}");
                }
                else
                {
                    Debug.Log($"[Storage] アイテム変化失敗（所持品）: 確率{transformChanceValue}%");
                }
            }

            AfterAction();
        }
        finally
        {
            busy = false; // 倉庫は常に同シーン
        }
    }

    private void UseConsumableFromStorage(InventoryItem invItem)
    {
        // ★多重入力ガード（副作用の前）。
        if (busy) return;
        busy = true;
        try
        {
            ItemActionHelper.ApplyConsumableEffects(invItem);
            ItemData transformInto = invItem.data?.transformInto;
            int transformChanceValue = invItem.data != null ? invItem.data.transformChance : 0;
            StorageManager.Instance?.RemoveItem(invItem);

            if (transformInto != null && StorageManager.Instance != null)
            {
                bool success = (transformChanceValue <= 0) || Random.Range(1, 101) <= transformChanceValue;
                if (success)
                {
                    StorageManager.Instance.AddItem(transformInto);
                    Debug.Log($"[Storage] アイテム変化（倉庫）: → {transformInto.itemName}");
                }
                else
                {
                    Debug.Log($"[Storage] アイテム変化失敗（倉庫）: 確率{transformChanceValue}%");
                }
            }

            AfterAction();
        }
        finally
        {
            busy = false;
        }
    }

    // =========================================================
    // 武器を食べる
    // =========================================================

    private void EatWeaponFromInventory(InventoryItem invItem)
    {
        if (invItem?.data == null || !invItem.data.isEdible) return;

        // ★多重入力ガード（副作用の前）。
        if (busy) return;
        busy = true;
        try
        {
            ItemActionHelper.UnequipIfNeeded(invItem);
            ItemActionHelper.ApplyEatWeaponEffects(invItem);

            ItemData transformInto = invItem.data.transformInto;
            int transformChanceValue = invItem.data.transformChance;
            ItemBoxManager.Instance?.RemoveItem(invItem);

            if (transformInto != null && ItemBoxManager.Instance != null)
            {
                bool success = (transformChanceValue <= 0) || Random.Range(1, 101) <= transformChanceValue;
                if (success)
                {
                    ItemBoxManager.Instance.AddItem(transformInto);
                }
            }

            AfterAction();
        }
        finally
        {
            busy = false;
        }
    }

    private void EatWeaponFromStorage(InventoryItem invItem)
    {
        if (invItem?.data == null || !invItem.data.isEdible) return;

        // ★多重入力ガード（副作用の前）。
        if (busy) return;
        busy = true;
        try
        {
            ItemActionHelper.ApplyEatWeaponEffects(invItem);

            ItemData transformInto = invItem.data.transformInto;
            int transformChanceValue = invItem.data.transformChance;
            StorageManager.Instance?.RemoveItem(invItem);

            if (transformInto != null && StorageManager.Instance != null)
            {
                bool success = (transformChanceValue <= 0) || Random.Range(1, 101) <= transformChanceValue;
                if (success)
                {
                    StorageManager.Instance.AddItem(transformInto);
                }
            }

            AfterAction();
        }
        finally
        {
            busy = false;
        }
    }

    private void EquipWeapon(InventoryItem invItem)
    {
        // ★多重入力ガード（副作用の前）。
        if (busy) return;
        busy = true;
        try
        {
            ItemBoxManager.Instance?.EquipItem(invItem);
            AfterAction();
        }
        finally
        {
            busy = false;
        }
    }

    private void UnequipWeapon(InventoryItem invItem)
    {
        // ★多重入力ガード（副作用の前）。
        if (busy) return;
        busy = true;
        try
        {
            ItemBoxManager.Instance?.UnequipItem(invItem);
            AfterAction();
        }
        finally
        {
            busy = false;
        }
    }

    private void DiscardFromInventory(InventoryItem invItem)
    {
        // ★多重入力ガード（副作用の前）。
        if (busy) return;
        busy = true;
        try
        {
            ItemBoxManager.Instance?.DiscardItem(invItem);
            AfterAction();
        }
        finally
        {
            busy = false;
        }
    }

    private void DiscardFromStorage(InventoryItem invItem)
    {
        // ★多重入力ガード（副作用の前）。
        if (busy) return;
        busy = true;
        try
        {
            StorageManager.Instance?.RemoveItem(invItem);
            AfterAction();
        }
        finally
        {
            busy = false;
        }
    }

    private void DepositItem(InventoryItem invItem)
    {
        if (StorageManager.Instance == null || ItemBoxManager.Instance == null) return;
        if (StorageManager.Instance.IsFull) return;

        // ★多重入力ガード（副作用の前）。Remove+Add の非アトミック交差を防ぐ。
        if (busy) return;
        busy = true;
        try
        {
            if (GameState.I != null && GameState.I.equippedWeaponUid == invItem.uid)
                GameState.I.equippedWeaponUid = "";

            ItemBoxManager.Instance.RemoveItem(invItem);
            StorageManager.Instance.AddInventoryItem(invItem);
            AfterAction();
        }
        finally
        {
            busy = false;
        }
    }

    private void WithdrawItem(InventoryItem invItem)
    {
        if (StorageManager.Instance == null || ItemBoxManager.Instance == null) return;
        if (ItemBoxManager.Instance.IsFull) return;

        // ★多重入力ガード（副作用の前）。Remove+Add の非アトミック交差を防ぐ。
        if (busy) return;
        busy = true;
        try
        {
            StorageManager.Instance.RemoveItem(invItem);
            ItemBoxManager.Instance.AddItem(invItem.data);
            AfterAction();
        }
        finally
        {
            busy = false;
        }
    }

    private void AfterAction()
    {
        detailPanel?.Hide();
        selectedItem = null;
        RefreshSlots();
        SaveManager.Save(); // 操作結果を即時セーブ
    }



}