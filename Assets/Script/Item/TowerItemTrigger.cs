using UnityEngine;
using UnityEngine.SceneManagement;

public class TowerItemTrigger : MonoBehaviour
{
    public static TowerItemTrigger Instance { get; private set; }

    [Header("Pickup Rate")]
    [Range(0f, 1f)]
    [SerializeField] private float itemPickupRate = 0.20f;

    [Header("Database")]
    [SerializeField] private ItemDatabase itemDatabase;

    [Header("UI")]
    [SerializeField] private ItemPickupWindow itemPickupWindow;

    private ItemData currentItem;

    public bool IsBusy { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // Tower シーンに戻ってきた時に呼ばれる
    private void Start()
    {
        CheckPendingExchange();
    }

    /// Tower に戻った時、交換待ちアイテムがあれば処理する
    private void CheckPendingExchange()
    {
        var gs = GameState.I;
        if (gs == null || gs.pendingItemData == null) return;

        ItemData pending = gs.pendingItemData;
        currentItem = pending;
        gs.pendingItemData = null;
        IsBusy = true;

        bool isFull = ItemBoxManager.Instance != null && ItemBoxManager.Instance.IsFull;

        if (!isFull)
        {
            // 枠が空いた → 「入手する」ボタン付きでポップアップ再表示
            // 自動入手はしない（SE やエフェクトを挟む余地を残す）
            itemPickupWindow.Show(
                pending.itemName,
                pending.description + "\n\n整理が完了しました。入手できます。",
                pending.icon,
                canGet: true,
                isFull: false,
                OnItemResult);
        }
        else
        {
            // まだ満杯 → 「交換する」ボタンでポップアップ再表示
            itemPickupWindow.Show(
                pending.itemName, pending.description, pending.icon,
                canGet: false,
                isFull: true,
                OnItemResult);
        }
    }

    public bool TryTriggerItemEvent(int floor, int step)
    {
        if (IsBusy) return true;

        if (Random.value >= itemPickupRate)
            return false;

        StartItemEvent(floor, step);
        return true;
    }

    private void StartItemEvent(int floor, int step)
    {
        IsBusy = true;
        currentItem = null;

        if (itemDatabase == null)
        {
            Debug.LogError("[TowerItemTrigger] ItemDatabase is not assigned.");
            IsBusy = false;
            return;
        }

        if (itemPickupWindow == null)
        {
            Debug.LogError("[TowerItemTrigger] ItemPickupWindow is not assigned.");
            IsBusy = false;
            return;
        }

        ItemData item = itemDatabase.GetRandomCandidate(floor, step);

        if (item == null)
        {
            Debug.Log($"[TowerItemTrigger] {floor}階 {step}STEP に出現可能なアイテムがありません。");
            IsBusy = false;
            return;
        }

        currentItem = item;

        bool isFull = ItemBoxManager.Instance != null && ItemBoxManager.Instance.IsFull;
        bool canGet = ItemBoxManager.Instance != null && ItemBoxManager.Instance.CanAddItem(item);

        itemPickupWindow.Show(item.itemName, item.description, item.icon,
                              canGet, isFull, OnItemResult);
    }

    private void OnItemResult(ItemPickupResult result)
    {
        Debug.Log($"[TowerItemTrigger] OnItemResult: {result}");

        switch (result)
        {
            case ItemPickupResult.Get:
                if (currentItem != null && ItemBoxManager.Instance != null)
                {
                    bool added = ItemBoxManager.Instance.AddItem(currentItem);
                    Debug.Log(added
                        ? $"アイテムを入手した: {currentItem.itemName}"
                        : "アイテムBOXが満杯のため入手できなかった");
                }
                currentItem = null;
                IsBusy = false;
                break;

            case ItemPickupResult.Exchange:
                // 交換フロー開始: pendingItemData に記録して Itembox へ遷移
                if (GameState.I != null)
                    GameState.I.pendingItemData = currentItem;

                // IsBusy は true のまま（Towerに戻った時に解除される）
                SceneManager.LoadScene("Itembox");
                break;

            case ItemPickupResult.Ignore:
                Debug.Log("アイテムを諦めた");
                currentItem = null;
                IsBusy = false;
                break;
        }
    }
}