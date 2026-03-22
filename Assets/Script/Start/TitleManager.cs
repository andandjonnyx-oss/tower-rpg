using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// タイトル画面のUIコントローラー。
/// 「つづきから」でセーブデータをロードして Main へ遷移。
/// 「はじめから」でセーブデータを削除し、初期状態で Main へ遷移。
/// </summary>
public class TitleUIManager : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button continueButton;
    [SerializeField] private Button newGameButton;

    private void Start()
    {
        // セーブデータがなければ「つづきから」ボタンを無効化
        if (continueButton != null)
        {
            continueButton.interactable = SaveManager.HasSaveData();
            continueButton.onClick.AddListener(OnContinue);
        }

        if (newGameButton != null)
            newGameButton.onClick.AddListener(OnNewGame);
    }

    /// <summary>
    /// つづきから: セーブデータをロードして Main シーンへ遷移する。
    /// </summary>
    private void OnContinue()
    {
        SaveManager.Load();
        SceneManager.LoadScene("Main");
    }

    /// <summary>
    /// はじめから: セーブデータを削除し、初期状態で Main シーンへ遷移する。
    /// GameState と ItemBoxManager を初期化する。
    /// </summary>
    private void OnNewGame()
    {
        // セーブデータを削除
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

            GameState.I.maxHp = 50;
            GameState.I.maxMp = 20;
            GameState.I.currentHp = 50;
            GameState.I.currentMp = 20;

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
        }

        // 所持品をクリア
        if (ItemBoxManager.Instance != null)
            ItemBoxManager.Instance.ClearAll();

        SceneManager.LoadScene("Main");
    }
}