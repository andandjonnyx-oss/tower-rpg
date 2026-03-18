using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Tower シーンや Main シーンのボタンにアタッチ。
/// クリックで Status シーンを開き、現在のシーン名を GameState に記録する。
/// </summary>
public class OpenStatusButton : MonoBehaviour
{
    [SerializeField] private string statusSceneName = "Status";

    public void OnClick()
    {
        // 戻り先を記録
        if (GameState.I != null)
            GameState.I.previousSceneName = SceneManager.GetActiveScene().name;

        SceneManager.LoadScene(statusSceneName);
    }
}