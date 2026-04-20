using System.Collections.Generic;
using TMPro;
using UnityEngine;
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
        if (selectedShopData == null) return;
        if (!CanExchange(selectedShopData)) return;

        var item = selectedShopData.item;
        int cost = selectedShopData.gpCost;

        // GP消費
        GameState.I.gp -= cost;

        // アイテム追加（ItemBoxManager.AddItem 内でセーブされる）
        bool added = ItemBoxManager.Instance.AddItem(item);

        if (added)
        {
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