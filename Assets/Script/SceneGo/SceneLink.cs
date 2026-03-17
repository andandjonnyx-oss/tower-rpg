using UnityEngine;

public class SceneLink : MonoBehaviour
{
    [SerializeField] private string sceneName;

    public void Go()
    {
        SceneLoader.Instance.LoadScene(sceneName);
    }
}