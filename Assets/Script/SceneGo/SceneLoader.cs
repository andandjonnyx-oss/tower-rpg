using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance { get; private set; }

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // Å© Ç±ÇÍÇ™èdóv
    public void LoadScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }
}