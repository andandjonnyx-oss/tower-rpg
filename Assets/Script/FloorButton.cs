using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class FloorButton : MonoBehaviour
{
    [Header("設定")]
    [Tooltip("このボタンが表示される条件となる到達階。例: 11 → 11階に到達済みなら表示")]
    [SerializeField] private int requiredFloor = 1;

    [Tooltip("このボタンを押した時にスタートする階。通常は requiredFloor と同じ")]
    [SerializeField] private int startFloor = 1;

    [Header("遷移先")]
    [SerializeField] private string towerSceneName = "Tower";

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(OnClick);
    }

    public void Refresh(int reachedFloor)
    {
        bool show = reachedFloor >= requiredFloor;

        // ★ デバッグ: 各ボタンの判定を確認
        Debug.Log($"[FloorButton] {gameObject.name}: requiredFloor={requiredFloor}, reachedFloor={reachedFloor}, show={show}");

        gameObject.SetActive(show);
    }

    private void OnClick()
    {
        var gs = GameState.I;
        if (gs == null) return;

        gs.floor = startFloor;
        gs.step = 1;

        Debug.Log($"[FloorButton] {startFloor}階の1STEPからスタート");
        SceneManager.LoadScene(towerSceneName);
    }
}