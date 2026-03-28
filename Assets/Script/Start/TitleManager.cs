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

        Debug.Log("[Title] セーブデータを初期化しました");
    }
}