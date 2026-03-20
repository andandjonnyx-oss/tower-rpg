using UnityEngine;

/// <summary>
/// GameStateAutoCreate と同じパターン。
/// どのシーンから起動しても StorageManager が必ず存在するようにする。
/// </summary>
public static class StorageManagerAutoCreate
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void CreateIfNeeded()
    {
        if (StorageManager.Instance != null) return;

        var go = new GameObject("StorageManager");
        go.AddComponent<StorageManager>();
    }
}