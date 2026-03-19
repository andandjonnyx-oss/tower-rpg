using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TowerEntranceView : MonoBehaviour
{
    [Header("Back to Main")]
    [SerializeField] private Button backButton;
    [SerializeField] private string mainSceneName = "Main";

    private FloorButton[] floorButtons;

    private void Awake()
    {
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

        // ★ デバッグ: GameState の状態を確認
        Debug.Log($"[TowerEntranceView] GameState.I is {(gs != null ? "存在する" : "NULL")}");
        Debug.Log($"[TowerEntranceView] reachedFloor = {reached}");
        Debug.Log($"[TowerEntranceView] FloorButton 数 = {floorButtons.Length}");

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