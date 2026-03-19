using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Towerin シーンの各階ボタンにアタッチ。
/// インスペクターで「何階到達すれば表示するか」「何階からスタートするか」を設定する。
/// </summary>
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

    /// TowerEntranceView から呼ばれる。到達階に応じて表示/非表示を切り替え。
    public void Refresh(int reachedFloor)
    {
        gameObject.SetActive(reachedFloor >= requiredFloor);
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