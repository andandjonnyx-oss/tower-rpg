using UnityEngine;

/// <summary>
/// SceneLoaderAutoCreate と同じパターン。
/// どのシーンから起動しても GameState が必ず存在するようにする。
/// これにより Main シーンを直接再生しても GameState.I が null にならない。
/// </summary>
public static class GameStateAutoCreate
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void CreateIfNeeded()
    {
        if (GameState.I != null) return;

        var go = new GameObject("GameState");
        go.AddComponent<GameState>();
        // Awake() 内で DontDestroyOnLoad が呼ばれるので、ここでは不要
    }
}