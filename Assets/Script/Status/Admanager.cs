using System;
using UnityEngine;

/// <summary>
/// 広告表示を抽象化するシングルトン。
/// 現在はダミー実装（即成功を返す）。
/// 将来 Unity Ads / AdMob 等の SDK を導入したら
/// ShowRewardedAd() の中身だけ差し替えればよい。
/// </summary>
/// 
// AdManager も自動生成したい場合（GameStateAutoCreate と同じパターン）
public static class AdManagerAutoCreate
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void CreateIfNeeded()
    {
        if (AdManager.Instance != null) return;
        var go = new GameObject("AdManager");
        go.AddComponent<AdManager>();
    }
}
public class AdManager : MonoBehaviour
{
    public static AdManager Instance { get; private set; }

    // AdManager も自動生成したい場合（GameStateAutoCreate と同じパターン）
public static class AdManagerAutoCreate
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void CreateIfNeeded()
    {
        if (AdManager.Instance != null) return;
        var go = new GameObject("AdManager");
        go.AddComponent<AdManager>();
    }
}

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // =========================================================
    // リワード広告を表示する
    // =========================================================
    // onResult: true = 広告視聴完了（報酬付与OK）, false = 失敗/スキップ
    //
    // 【将来の差し替え例 (Unity Ads)】
    // Advertisement.Show(placementId, new ShowOptions {
    //     resultCallback = result => {
    //         onResult?.Invoke(result == ShowResult.Finished);
    //     }
    // });
    // =========================================================

    /// <summary>
    /// リワード広告を表示し、結果をコールバックで返す。
    /// </summary>
    /// <param name="onResult">true = 視聴完了, false = 失敗/キャンセル</param>
    public void ShowRewardedAd(Action<bool> onResult)
    {
        // --- ダミー実装: 広告SDK未導入のため即成功を返す ---
        Debug.Log("[AdManager] (ダミー) リワード広告を表示 → 即成功");
        onResult?.Invoke(true);

        // --- 将来の実装イメージ ---
        // if (!Advertisement.IsReady(rewardedPlacementId))
        // {
        //     Debug.LogWarning("[AdManager] 広告の準備ができていません");
        //     onResult?.Invoke(false);
        //     return;
        // }
        // Advertisement.Show(rewardedPlacementId, new ShowOptions {
        //     resultCallback = result => onResult?.Invoke(result == ShowResult.Finished)
        // });
    }
}