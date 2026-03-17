using UnityEngine;

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

        bool canGet = ItemBoxManager.Instance != null && ItemBoxManager.Instance.CanAddItem(item);

        string itemName = item.itemName;
        string description = item.description;
        Sprite sprite = item.icon;

        itemPickupWindow.Show(itemName, description, sprite, canGet, OnItemResult);
    }

    private void OnItemResult(ItemPickupResult result)
    {
        Debug.Log($"[TowerItemTrigger] OnItemResult: {result}");
        Debug.Log($"[TowerItemTrigger] currentItem: {(currentItem != null ? currentItem.itemName : "NULL")}");

        switch (result)
        {
            case ItemPickupResult.Get:
                if (currentItem == null)
                {
                    Debug.LogWarning("[TowerItemTrigger] currentItem is null.");
                    break;
                }

                if (ItemBoxManager.Instance == null)
                {
                    Debug.LogError("[TowerItemTrigger] ItemBoxManager not found.");
                    break;
                }

                Debug.Log($"[TowerItemTrigger] Before AddItem Count = {ItemBoxManager.Instance.Count}");

                bool added = ItemBoxManager.Instance.AddItem(currentItem);

                Debug.Log($"[TowerItemTrigger] AddItem Result = {added}");
                Debug.Log($"[TowerItemTrigger] After AddItem Count = {ItemBoxManager.Instance.Count}");

                if (added)
                {
                    Debug.Log($"アイテムを入手した: {currentItem.itemName}");
                }
                else
                {
                    Debug.Log("アイテムBOXが満杯のため入手できなかった");
                }
                break;

            case ItemPickupResult.Ignore:
                Debug.Log("アイテムを諦めた");
                break;
        }

        currentItem = null;
        IsBusy = false;
    }
}