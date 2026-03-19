using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Towerin シーンの Canvas にアタッチ。
/// 到達済みの階に応じてスタート地点ボタンを表示/非表示し、
/// 選択した階の1STEPから Tower シーンへ遷移する。
/// </summary>
public class TowerEntranceView : MonoBehaviour
{
    [Header("Floor Buttons (配列の0番目 = 1階ボタン, 1番目 = 2階ボタン, ...)")]
    [SerializeField] private Button[] floorButtons;

    [Header("Back to Main")]
    [SerializeField] private Button backButton;
    [SerializeField] private string mainSceneName = "Main";
    [SerializeField] private string towerSceneName = "Tower";

    private void Awake()
    {
        // 各階ボタンにクリックイベントを登録
        for (int i = 0; i < floorButtons.Length; i++)
        {
            if (floorButtons[i] == null) continue;
            int floor = i + 1; // 0番目 → 1階
            floorButtons[i].onClick.AddListener(() => OnFloorSelected(floor));
        }

        // メインへ戻るボタン
        if (backButton != null)
            backButton.onClick.AddListener(OnBackClicked);
    }

    private void Start()
    {
        RefreshButtons();
    }

    /// 到達階に応じてボタンを表示/非表示
    private void RefreshButtons()
    {
        var gs = GameState.I;
        int reached = (gs != null) ? gs.reachedFloor : 1;

        for (int i = 0; i < floorButtons.Length; i++)
        {
            if (floorButtons[i] == null) continue;
            int floor = i + 1;

            // 到達済みの階のボタンだけ表示
            floorButtons[i].gameObject.SetActive(floor <= reached);
        }
    }

    /// 階ボタンをクリック → その階の1STEPから Tower へ
    private void OnFloorSelected(int floor)
    {
        var gs = GameState.I;
        if (gs == null) return;

        gs.floor = floor;
        gs.step = 1;

        Debug.Log($"[TowerEntranceView] {floor}階の1STEPからスタート");
        SceneManager.LoadScene(towerSceneName);
    }

    /// メインへ戻るボタン
    private void OnBackClicked()
    {
        SceneManager.LoadScene(mainSceneName);
    }
}