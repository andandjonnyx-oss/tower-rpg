using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// GP交換ショップのシーンコントローラー。
/// GpShopDatabase から商品リストを読み、GridLayoutGroup にセルを生成する。
/// セルタップでポップアップを表示し、交換処理を行う。
///
/// シーン構成例:
///   Canvas (Scale With Screen Size 1920×1080)
///     ├ Header
///     │   ├ TitleText ("GP交換所")
///     │   ├ GpText ("所持GP: 999")
///     │   └ BackButton
///     ├ ScrollView
///     │   └ Content (GridLayoutGroup)
///     │       └ (セルを動的生成)
///     └ DetailPopup (初期非表示)
///         ├ PopupBg (半透明黒背景、タップで閉じる)
///         └ PopupPanel
///             ├ PopupIcon (Image)
///             ├ PopupName (TMP_Text)
///             ├ PopupDesc (TMP_Text)
///             ├ PopupCost (TMP_Text)
///             ├ ExchangeButton (Button)
///             │   └ ExchangeButtonText (TMP_Text)
///             └ CloseButton (Button)
/// </summary>
public class GpShopView : MonoBehaviour
{
    // =========================================================
    // Inspector アサイン
    // =========================================================

    [Header("Data")]
    [SerializeField] private GpShopDatabase shopDatabase;

    [Header("Grid")]
    [Tooltip("GridLayoutGroup を持つ Content オブジェクト")]
    [SerializeField] private Transform gridContent;

    [Tooltip("GpShopCell Prefab")]
    [SerializeField] private GpShopCell cellPrefab;

    [Header("Header")]
    [SerializeField] private TMP_Text gpText;

    [Header("Detail Popup")]
    [Tooltip("ポップアップ全体の親オブジェクト（Blocker を含む）。\n"
           + "SetActive で表示/非表示を切り替える。\n"
           + "Blocker が画面全体を覆い、背面のボタンタップを防ぐ。")]
    [SerializeField] private GameObject detailPopup;

    [Tooltip("画面全体を覆う半透明パネル（Blocker）。\n"
           + "Raycast Target = true にして背面タップを防ぐ。\n"
           + "detailPopup の直下の子として配置する。\n"
           + "タップで閉じたい場合は Button コンポーネントを付けて closeButton と同じ動作にする。")]
    [SerializeField] private Button blockerButton;


    [SerializeField] private Image popupIcon;
    [SerializeField] private TMP_Text popupName;
    [SerializeField] private TMP_Text popupDesc;
    [SerializeField] private TMP_Text popupCost;
    [SerializeField] private Button exchangeButton;
    [SerializeField] private TMP_Text exchangeButtonText;
    [SerializeField] private Button closeButton;

    [Header("Message")]
    [Tooltip("交換結果のメッセージ表示用。一時的に表示して消える。")]
    [SerializeField] private TMP_Text messageText;

    [Header("Back")]
    [SerializeField] private Button backButton;

    // =========================================================
    // 内部状態
    // =========================================================

    private List<GpShopCell> cells = new();
    private GpShopData selectedShopData;
    private float messageTimer = 0f;
    private const float MessageDuration = 2f;

    // =========================================================
    // ライフサイクル
    // =========================================================

    private void Start()
    {
        // ポップアップ初期化
        HidePopup();
        HideMessage();

        // ボタンイベント登録
        if (exchangeButton != null)
            exchangeButton.onClick.AddListener(OnExchangeClicked);

        if (closeButton != null)
            closeButton.onClick.AddListener(HidePopup);

        // ブロッカータップでもポップアップを閉じる
        if (blockerButton != null)
            blockerButton.onClick.AddListener(HidePopup);

        if (backButton != null)
            backButton.onClick.AddListener(OnBackClicked);

        // グリッド生成
        BuildGrid();
        RefreshGpDisplay();

        // コントローラー対応:
        //   ・グリッドは位置ベースで格子配線し、最上段の上キーで戻るへ。
        //   ・詳細ポップアップは表示中フォーカスを内側に限定（キャンセル/外タップで閉じる）。
        WireGridNav();
        ControllerNav.SetNavigationNone(blockerButton);
        ModalFocusScope.Attach(detailPopup, HidePopup); // Esc/B = 閉じる（いいえ相当）
    }

    private void Update()
    {
        // メッセージの自動非表示
        if (messageTimer > 0f)
        {
            messageTimer -= Time.deltaTime;
            if (messageTimer <= 0f)
                HideMessage();
        }

        // キャンセルキー（Esc / パッドB）: ポップアップ表示中は閉じる、なければ戻る
        if (ModalFocusScope.Current == null || ModalFocusScope.Current.gameObject == detailPopup)
        {
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            bool cancel = (kb != null && kb.escapeKey.wasPressedThisFrame)
                       || (pad != null && pad.buttonEast.wasPressedThisFrame);
            if (cancel)
            {
                bool popupOpen = detailPopup != null && detailPopup.activeSelf;
                if (popupOpen) HidePopup();
                else OnBackClicked();
            }
        }
    }

    /// <summary>
    /// 詳細ポップアップの中のボタン（交換 / 閉じる）を縦に配線する。
    /// ShowPopup のたびに交換ボタンの有効状態が変わるので都度呼ぶ。
    /// </summary>
    private void WirePopupNav()
    {
        var inside = new List<Selectable>();
        if (exchangeButton != null && exchangeButton.gameObject.activeInHierarchy && exchangeButton.interactable)
            inside.Add(exchangeButton);
        if (closeButton != null && closeButton.gameObject.activeInHierarchy && closeButton.interactable)
            inside.Add(closeButton);
        ControllerNav.WireHorizontalLoop(inside); // 交換/閉じるは横並び
    }

    /// <summary>
    /// ショップのグリッドを画面位置から格子として明示配線する。
    /// 列数は GridLayoutGroup が可変（Flexible）なので、セルの座標を行ごとに
    /// まとめて判定する。最上段の上キーは戻るボタンへ、戻るの下キーは左上セルへ。
    /// </summary>
    private void WireGridNav()
    {
        // アクティブなセルの Selectable を上→下・左→右で収集
        var list = new List<Selectable>();
        foreach (var c in cells)
        {
            if (c == null || !c.gameObject.activeInHierarchy) continue;
            var sel = c.GetComponent<Selectable>();
            if (sel != null) list.Add(sel);
        }
        list.Sort((a, b) =>
        {
            float ay = a.transform.position.y, by = b.transform.position.y;
            if (!Mathf.Approximately(ay, by)) return by.CompareTo(ay);
            return a.transform.position.x.CompareTo(b.transform.position.x);
        });

        // 行にまとめる
        var rows = new List<List<Selectable>>();
        List<Selectable> cur = null;
        float curY = 0f;
        foreach (var sel in list)
        {
            float y = sel.transform.position.y;
            if (cur == null || Mathf.Abs(y - curY) > 20f)
            {
                cur = new List<Selectable>();
                rows.Add(cur);
                curY = y;
            }
            cur.Add(sel);
        }

        Selectable back = (backButton != null && backButton.isActiveAndEnabled) ? backButton : null;

        for (int r = 0; r < rows.Count; r++)
        {
            var row = rows[r];
            for (int i = 0; i < row.Count; i++)
            {
                var cell = row[i];
                float x = cell.transform.position.x;
                Selectable left = (i > 0) ? row[i - 1] : null;
                Selectable right = (i < row.Count - 1) ? row[i + 1] : null;
                Selectable up = (r > 0) ? NearestByX(rows[r - 1], x) : back;   // 最上段の上 → 戻る
                Selectable down = (r < rows.Count - 1) ? NearestByX(rows[r + 1], x) : null;
                ControllerNav.SetExplicit(cell, up, down, left, right);
            }
        }

        // 戻る: 下キーで左上セルへ（グリッドへ入る動線）。左右/上は割り当てない。
        if (back != null)
        {
            Selectable topLeft = (rows.Count > 0 && rows[0].Count > 0) ? rows[0][0] : null;
            ControllerNav.SetExplicit(back, null, topLeft, null, null);
        }

        // コントローラーの初期フォーカスは左上セル
        SelectionHighlighter.PreferredFallback =
            (rows.Count > 0 && rows[0].Count > 0) ? rows[0][0] : back;
    }

    private static Selectable NearestByX(List<Selectable> row, float x)
    {
        Selectable best = null;
        float bestDx = float.MaxValue;
        for (int i = 0; i < row.Count; i++)
        {
            if (row[i] == null) continue;
            float dx = Mathf.Abs(row[i].transform.position.x - x);
            if (dx < bestDx) { bestDx = dx; best = row[i]; }
        }
        return best;
    }

    // =========================================================
    // グリッド構築
    // =========================================================

    private void BuildGrid()
    {
        // 既存セルをクリア
        foreach (var cell in cells)
        {
            if (cell != null)
                Destroy(cell.gameObject);
        }
        cells.Clear();

        if (shopDatabase == null || cellPrefab == null || gridContent == null) return;

        int reachedFloor = GameState.I != null ? GameState.I.reachedFloor : 1;
        var available = shopDatabase.GetAvailableItems(reachedFloor);

        foreach (var shopItem in available)
        {
            var cellObj = Instantiate(cellPrefab, gridContent);
            var cell = cellObj.GetComponent<GpShopCell>();
            if (cell == null) continue;

            cell.Setup(shopItem);
            cell.onClicked = OnCellClicked;

            // GP不足またはアイテム枠満杯ならグレーアウト
            bool canExchange = CanExchange(shopItem);
            cell.SetInteractable(canExchange);

            cells.Add(cell);
        }
    }

    /// <summary>
    /// 交換可能かどうかを判定する。
    /// </summary>
    private bool CanExchange(GpShopData shopData)
    {
        if (shopData == null || shopData.item == null) return false;
        if (GameState.I == null) return false;

        // GP不足
        if (GameState.I.gp < shopData.gpCost) return false;

        // アイテム枠満杯
        if (ItemBoxManager.Instance != null && ItemBoxManager.Instance.IsFull) return false;

        return true;
    }

    // =========================================================
    // セルタップ → ポップアップ表示
    // =========================================================

    private void OnCellClicked(GpShopData shopData)
    {
        if (shopData == null || shopData.item == null) return;

        selectedShopData = shopData;
        ShowPopup(shopData);
    }

    private void ShowPopup(GpShopData shopData)
    {
        var item = shopData.item;

        // アイコン
        if (popupIcon != null)
        {
            popupIcon.sprite = item.icon;
            popupIcon.enabled = item.icon != null;
        }

        // 名前
        if (popupName != null)
            popupName.text = item.itemName;

        // 説明
        if (popupDesc != null)
            popupDesc.text = item.description;

        // GP価格
        if (popupCost != null)
            popupCost.text = $"必要GP: {shopData.gpCost}";

        // 交換ボタンの状態
        bool canExchange = CanExchange(shopData);
        if (exchangeButton != null)
            exchangeButton.interactable = canExchange;

        if (exchangeButtonText != null)
        {
            if (ItemBoxManager.Instance != null && ItemBoxManager.Instance.IsFull)
                exchangeButtonText.text = "持ち物がいっぱい";
            else if (GameState.I != null && GameState.I.gp < shopData.gpCost)
                exchangeButtonText.text = "GPが足りない";
            else
                exchangeButtonText.text = $"交換する（{shopData.gpCost}GP）";
        }

        // ポップアップ表示
        if (detailPopup != null)
            detailPopup.SetActive(true);

        WirePopupNav(); // 交換/閉じるの配線（交換の有効状態は都度変わる）
    }

    private void HidePopup()
    {
        selectedShopData = null;
        if (detailPopup != null)
            detailPopup.SetActive(false);
    }

    // =========================================================
    // 交換処理
    // =========================================================

    private void OnExchangeClicked()
    {
        // ★多重押下ガード（同フレーム2連打によるGP二重課金・アイテム二重付与対策）
        //   selectedShopData を副作用の前にローカル退避して即 null 化することで、
        //   2回目以降の呼び出しは冒頭の null チェックで弾かれる。
        //   （OnAdResult の adResultHandled と同じ「一度きり」保証の考え方）
        if (selectedShopData == null) return;
        if (!CanExchange(selectedShopData)) return;

        var shopData = selectedShopData;
        selectedShopData = null; // ★以降の再入を即遮断

        var item = shopData.item;
        int cost = shopData.gpCost;

        // GP消費
        GameState.I.gp -= cost;

        // アイテム追加（ItemBoxManager.AddItem 内でセーブされる）
        bool added = ItemBoxManager.Instance.AddItem(item);

        if (added)
        {
            // 入手SE
            if (AudioManager.I != null) AudioManager.I.PlayItemGetSe();

            // 図鑑記録（交換成立時点で登録）
            if (GameState.I != null) GameState.I.MarkItemDiscovered(item.itemId);

            Debug.Log($"[GpShopView] 交換成功: {item.itemName} ({cost}GP消費, 残りGP={GameState.I.gp})");
            ShowMessage($"{item.itemName} を手に入れた！");

            // 追加のセーブ（GP変更分）
            SaveManager.Save();
        }
        else
        {
            // AddItem が失敗した場合（通常は IsFull チェックで弾かれるので到達しないが念のため）
            GameState.I.gp += cost; // GP返却
            Debug.LogWarning($"[GpShopView] 交換失敗: {item.itemName} のアイテム追加に失敗（GP返却）");
            ShowMessage("交換に失敗しました");
        }

        // ポップアップを閉じてグリッドを更新
        HidePopup();
        RefreshGpDisplay();
        RefreshCellStates();
    }

    // =========================================================
    // 表示更新
    // =========================================================

    private void RefreshGpDisplay()
    {
        if (gpText != null && GameState.I != null)
            gpText.text = $"所持GP: {GameState.I.gp}";
    }

    /// <summary>
    /// 全セルの交換可能状態を再チェックする。
    /// 交換後にGPが減っているので、他の商品もグレーアウトが変わる可能性がある。
    /// </summary>
    private void RefreshCellStates()
    {
        if (shopDatabase == null) return;

        int reachedFloor = GameState.I != null ? GameState.I.reachedFloor : 1;
        var available = shopDatabase.GetAvailableItems(reachedFloor);

        for (int i = 0; i < cells.Count && i < available.Count; i++)
        {
            if (cells[i] == null) continue;
            bool canExchange = CanExchange(available[i]);
            cells[i].SetInteractable(canExchange);
        }
    }

    // =========================================================
    // メッセージ表示
    // =========================================================

    private void ShowMessage(string msg)
    {
        if (messageText != null)
        {
            messageText.text = msg;
            messageText.gameObject.SetActive(true);
            messageTimer = MessageDuration;
        }
    }

    private void HideMessage()
    {
        if (messageText != null)
            messageText.gameObject.SetActive(false);

        messageTimer = 0f;
    }

    // =========================================================
    // 戻る
    // =========================================================

    private void OnBackClicked()
    {
        SceneManager.LoadScene("Main");
    }
}