using UnityEngine;
using UnityEngine.SceneManagement;

public class TitleUIManager : MonoBehaviour
{
    // ゲームスタートボタン
    public void OnClickStartGame()
    {
        SceneManager.LoadScene("Main");
    }
}