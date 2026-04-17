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

        // =========================================================
        // 報酬アイテム判定（追加）
        // =========================================================
        //
        // isRewardItem == true の場合:
        //   Talk イベント報酬で初めて pendingItemData がセットされたケース。
        //   「整理が完了しました」ではなく通常の入手ポップアップを表示する。
        //
        // isRewardItem == false の場合:
        //   Itembox での整理後に Tower に戻ってきたケース。
        //   従来通り「整理が完了しました。入手できます。」を表示する。
        // =========================================================
        bool isReward = gs.isRewardItem;
        gs.isRewardItem = false; // フラグをリセット

        if (!isFull)
        {
            if (isReward)
            {
                // 報酬アイテム: 通常の入手ポップアップ
                itemPickupWindow.Show(
                    pending.itemName,
                    pending.description,
                    pending.icon,
                    canGet: true,
                    isFull: false,
                    OnItemResult,
                    cannotIgnore: pending.cannotDiscard);
            }
            else
            {
                // 整理後の復帰: 従来メッセージ
                itemPickupWindow.Show(
                    pending.itemName,
                    pending.description + "\n\n整理が完了しました。入手できます。",
                    pending.icon,
                    canGet: true,
                    isFull: false,
                    OnItemResult);
            }
        }
        else
        {
            // 満杯 → 「交換する」ボタンでポップアップ再表示
            itemPickupWindow.Show(
                pending.itemName, pending.description, pending.icon,
                canGet: false,
                isFull: true,
                OnItemResult,
                cannotIgnore: pending.cannotDiscard);
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

                // ★ 魔法アイテム入手時にTowerの魔法UIを即時更新
                var towerState = FindAnyObjectByType<TowerState>();
                if (towerState != null) towerState.RefreshFieldMagicFromExternal();

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