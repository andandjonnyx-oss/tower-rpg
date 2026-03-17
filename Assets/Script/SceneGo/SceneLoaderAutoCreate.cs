using UnityEngine;

public static class SceneLoaderAutoCreate
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void CreateIfNeeded()
    {
        if (SceneLoader.Instance != null) return;

        var go = new GameObject("SceneLoader");
        go.AddComponent<SceneLoader>();
    }
}