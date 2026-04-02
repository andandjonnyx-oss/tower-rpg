using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// タイトル画面のUIコントローラー。
/// 「スタート」ボタン: セーブデータがあれば続きから、なければ初期状態で Main へ遷移。
/// 「初期化」ボタン: セーブデータを削除して GameState・所持品・倉庫を初期化する。
/// </summary>
public class TitleUIManager : MonoBehaviour
{
    [Header("Buttons")]
    [Tooltip("セーブデータがあれば続きから、なければ新規でスタート")]
    [SerializeField] private Button startButton;
    [Tooltip("セーブデータを削除して初期状態に戻す")]
    [SerializeField] private Button resetButton;

    // =========================================================
    // 初期アイテムセット（追加）
    // =========================================================
    //
    // 新規開始時・初期化時にプレイヤーに付与するアイテム。
    // インスペクターで設定する。
    //
    // 設定例:
    //   startingItems[0] = W001_Bokutou（木刀）    → 自動装備
    //   startingItems[1] = C001_Yakusou（薬草）
    //   startingItems[2] = C001_Yakusou（薬草）
    //   startingItems[3] = C001_Yakusou（薬草）
    //   startingItems[4] = M001_Fire（ファイアボール）
    //
    // 注意:
    //   同じアイテムを複数個付与する場合は、配列に複数回設定する。
    //   Weapon カテゴリのアイテムが含まれている場合、
    //   最初に見つかった Weapon を自動装備する。
    // =========================================================

    [Header("Starting Items")]
    [Tooltip("新規開始時にプレイヤーに付与するアイテム。\n"
           + "Weapon カテゴリのアイテムは最初の1つを自動装備する。")]
    [SerializeField] private ItemData[] startingItems;

    private void Start()
    {
        if (startButton != null)
            startButton.onClick.AddListener(OnStart);

        if (resetButton != null)
            resetButton.onClick.AddListener(OnReset);
    }

    /// <summary>
    /// スタートボタン: セーブデータがあればロードして続きから、なければ初期状態で Main へ。
    /// </summary>
    private void OnStart()
    {
        if (SaveManager.HasSaveData())
        {
            // セーブデータあり → ロードして続きから
            SaveManager.Load();
            Debug.Log("[Title] セーブデータをロードして続きから開始");
        }
        else
        {
            // セーブデータなし → 初期状態のまま
            // maxHp/maxMp を VIT/INT から再計算して正しい値にする
            if (GameState.I != null)
            {
                GameState.I.RecalcMaxHp();
                GameState.I.RecalcMaxMp();
            }

            // 初期アイテムを付与
            GrantStartingItems();

            Debug.Log("[Title] セーブデータなし。新規で開始");
        }

        SceneManager.LoadScene("Main");
    }

    /// <summary>
    /// 初期化ボタン: セーブデータを削除し、GameState・所持品・倉庫をリセットする。
    /// Main には遷移しない（タイトルに留まる）。
    /// </summary>
    private void OnReset()
    {
        // セーブファイルを削除
        SaveManager.DeleteSave();

        // GameState を初期値に戻す
        if (GameState.I != null)
        {
            GameState.I.floor = 1;
            GameState.I.step = 1;
            GameState.I.reachedFloor = 1;

            GameState.I.level = 1;
            GameState.I.currentExp = 0;
            GameState.I.expToNext = 100;

            GameState.I.baseSTR = 1;
            GameState.I.baseVIT = 1;
            GameState.I.baseINT = 1;
            GameState.I.baseDEX = 1;
            GameState.I.baseLUC = 1;

            GameState.I.initialSTR = 1;
            GameState.I.initialVIT = 1;
            GameState.I.initialINT = 1;
            GameState.I.initialDEX = 1;
            GameState.I.initialLUC = 1;

            GameState.I.statusPoint = 10;
            GameState.I.equippedWeaponUid = "";

            GameState.I.isInBattle = false;
            GameState.I.battleTurnConsumed = false;
            GameState.I.battleItemActionLog = "";
            GameState.I.pendingItemData = null;
            GameState.I.pendingEventId = "";

            // 既読イベントをクリア
            GameState.I.RestorePlayedIds(null);

            // maxHp/maxMp を VIT/INT から再計算（ハードコード 50/20 ではなく正しい値を使う）
            // RecalcMaxHp/RecalcMaxMp 内で currentHp/currentMp も maxHp/maxMp にクランプされる。
            // ただし currentHp が maxHp 以下の場合はそのまま維持される設計なので、
            // まず -1 にリセットして「未初期化」状態にし、Recalc 内で maxHp に揃える。
            GameState.I.maxHp = -1;
            GameState.I.currentHp = -1;
            GameState.I.maxMp = -1;
            GameState.I.currentMp = -1;
            GameState.I.RecalcMaxHp();
            GameState.I.RecalcMaxMp();
        }

        // 所持品をクリア
        if (ItemBoxManager.Instance != null)
            ItemBoxManager.Instance.ClearAll();

        // 倉庫をクリア
        if (StorageManager.Instance != null)
            StorageManager.Instance.ClearAll();

        // 初期アイテムを付与
        GrantStartingItems();

        Debug.Log("[Title] セーブデータを初期化しました");
    }

    // =========================================================
    // 初期アイテム付与（追加）
    // =========================================================

    /// <summary>
    /// startingItems 配列に設定されたアイテムを ItemBoxManager に追加する。
    /// Weapon カテゴリのアイテムが含まれている場合、最初の1つを自動装備する。
    /// </summary>
    private void GrantStartingItems()
    {
        if (startingItems == null || startingItems.Length == 0) return;
        if (ItemBoxManager.Instance == null)
        {
            Debug.LogWarning("[Title] ItemBoxManager が見つかりません。初期アイテムを付与できません。");
            return;
        }

        bool weaponEquipped = false;

        for (int i = 0; i < startingItems.Length; i++)
        {
            if (startingItems[i] == null) continue;

            bool added = ItemBoxManager.Instance.AddItem(startingItems[i]);
            if (!added)
            {
                Debug.LogWarning($"[Title] 初期アイテム追加失敗（容量不足?）: {startingItems[i].itemName}");
                continue;
            }

            Debug.Log($"[Title] 初期アイテム付与: {startingItems[i].itemName}");

            // 最初の Weapon を自動装備
            if (!weaponEquipped && startingItems[i].category == ItemCategory.Weapon)
            {
                var items = ItemBoxManager.Instance.GetItems();
                // 追加直後なので、最後に追加されたアイテム or ソート後の位置を検索
                for (int j = items.Count - 1; j >= 0; j--)
                {
                    if (items[j] != null && items[j].data == startingItems[i])
                    {
                        ItemBoxManager.Instance.EquipItem(items[j]);
                        Debug.Log($"[Title] 初期装備: {startingItems[i].itemName}");
                        weaponEquipped = true;
                        break;
                    }
                }
            }
        }
    }
}