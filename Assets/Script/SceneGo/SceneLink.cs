using UnityEngine;

public class SceneLink : MonoBehaviour
{
    [SerializeField] private string sceneName;

    // Exposed so TitleUIManager can locate specific buttons (e.g. credits)
    // at runtime for controller navigation wiring.
    public string SceneName => sceneName;

    public void Go()
    {
        SceneLoader.Instance.LoadScene(sceneName);
    }
}