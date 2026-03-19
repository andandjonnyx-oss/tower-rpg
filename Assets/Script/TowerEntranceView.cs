using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Towerin シーンの Canvas にアタッチ。
/// 子階層にある FloorButton を自動で検索し、到達階に応じて表示/非表示を一括制御する。
/// </summary>
public class TowerEntranceView : MonoBehaviour
{
    [Header("Back to Main")]
    [SerializeField] private Button backButton;
    [SerializeField] private string mainSceneName = "Main";

    private FloorButton[] floorButtons;

    private void Awake()
    {
        // 子階層から FloorButton を全て取得
        floorButtons = GetComponentsInChildren<FloorButton>(includeInactive: true);

        if (backButton != null)
            backButton.onClick.AddListener(OnBackClicked);
    }

    private void Start()
    {
        RefreshButtons();
    }

    private void RefreshButtons()
    {
        var gs = GameState.I;
        int reached = (gs != null) ? gs.reachedFloor : 1;

        foreach (var fb in floorButtons)
        {
            if (fb != null)
                fb.Refresh(reached);
        }
    }

    private void OnBackClicked()
    {
        SceneManager.LoadScene(mainSceneName);
    }
}