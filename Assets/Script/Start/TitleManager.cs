using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// タイトル画面のUIコントローラー。
/// 「スタート」ボタン: セーブデータがあれば続きから、なければ初期状態で Main へ遷移。
/// 「初期化」ボタン: セーブデータを削除して GameState・所持品・倉庫を初期化する。
/// 「オープニング」ボタン: オープニングイベントを Talk シーンで再生する。
/// </summary>
public class TitleUIManager : MonoBehaviour
{
    [Header("Buttons")]
    [Tooltip("セーブデータがあれば続きから、なければ新規でスタート")]
    [SerializeField] private Button startButton;
    [Tooltip("セーブデータを削除して初期状態に戻す")]
    [SerializeField] private Button resetButton;

    [Header("Reset Confirm Popup")]
    [Tooltip("初期化確認ポップアップのルートオブジェクト")]
    [SerializeField] private GameObject resetConfirmPopup;
    [Tooltip("初期化確認メッセージテキスト")]
    [SerializeField] private TMP_Text resetConfirmText;
    [Tooltip("はいボタン")]
    [SerializeField] private Button resetConfirmYes;
    [Tooltip("いいえボタン")]
    [SerializeField] private Button resetConfirmNo;

    // =========================================================
    // オープニングボタン（追加）
    // =========================================================
    //
    // タイトル画面に「オープニング」ボタンを追加。
    // 押すと Talk シーンに遷移し、オープニングイベントを再生する。
    // Talk 終了後はタイトル画面（Title シーン）に戻る。
    //
    // オープニングイベントは何度でも繰り返し視聴可能。
    // MarkPlayed() は通常通り呼ばれるが、
    // オープニングイベントは塔内のフロア/ステップ条件を持たないため
    // 既読でも再生に影響しない。
    // =========================================================

    [Tooltip("オープニングイベントを Talk シーンで再生する")]
    [SerializeField] private Button openingButton;

    [Header("Opening Event")]
    [Tooltip("オープニングで再生する TalkEvent の ID（例: \"OP_Opening\"）")]
    [SerializeField] private string openingEventId = "OP_Opening";
    [Tooltip("Talk シーンの名前")]
    [SerializeField] private string talkSceneName = "Talk";
    [Tooltip("タイトルシーンの名前（Talk 終了後の戻り先）")]
    [SerializeField] private string titleSceneName = "Title";

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
            resetButton.onClick.AddListener(OnResetClicked);

        // 初期化確認ポップアップ
        if (resetConfirmYes != null)
            resetConfirmYes.onClick.AddListener(OnResetConfirmYes);
        if (resetConfirmNo != null)
            resetConfirmNo.onClick.AddListener(OnResetConfirmNo);
        if (resetConfirmPopup != null)
            resetConfirmPopup.SetActive(false);

        if (openingButton != null)
            openingButton.onClick.AddListener(OnOpening);
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

    // =========================================================
    // オープニングボタン（追加）
    // =========================================================

    /// <summary>
    /// オープニングボタン: Talk シーンに遷移してオープニングイベントを再生する。
    /// Talk 終了後はタイトル画面に戻る。
    /// 何度でも繰り返し視聴可能。
    ///
    /// ★修正: Talk シーン内で MarkPlayed() → SaveManager.Save() が走るため、
    /// セーブデータがある場合は事前にロードしておく。
    /// これにより、初期状態のデータでセーブが上書きされるのを防ぐ。
    /// </summary>
    private void OnOpening()
    {
        var gs = GameState.I;
        if (gs == null)
        {
            Debug.LogError("[Title] GameState が存在しません。オープニングを開始できません。");
            return;
        }

        // ★修正: セーブデータがあれば事前にロードしておく
        // Talk シーン内で MarkPlayed() → SaveManager.Save() が呼ばれた際に
        // 初期値で上書きされるのを防止する。
        if (SaveManager.HasSaveData())
        {
            SaveManager.Load();
            Debug.Log("[Title] オープニング前にセーブデータをロード");
        }

        // Talk シーンへ渡すパラメータをセット
        gs.pendingEventId = openingEventId;
        gs.talkReturnScene = titleSceneName; // Talk 終了後にタイトルへ戻る

        Debug.Log($"[Title] オープニングイベント開始: {openingEventId}");
        SceneManager.LoadScene(talkSceneName);
    }

    /// <summary>
    /// 初期化ボタン押下 → ポップアップ表示。
    /// ポップアップが未設定の場合は従来通り即実行。
    /// </summary>
    private void OnResetClicked()
    {
        if (resetConfirmPopup != null)
        {
            if (resetConfirmText != null)
                resetConfirmText.text = "セーブデータを初期化しますか？\n（元に戻せません）";
            resetConfirmPopup.SetActive(true);
        }
        else
        {
            // ポップアップ未設定: 従来通り即実行
            OnReset();
        }
    }

    private void OnResetConfirmYes()
    {
        if (resetConfirmPopup != null)
            resetConfirmPopup.SetActive(false);
        OnReset();
    }

    private void OnResetConfirmNo()
    {
        if (resetConfirmPopup != null)
            resetConfirmPopup.SetActive(false);
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