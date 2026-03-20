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