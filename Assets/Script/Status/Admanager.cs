using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 広告表示を抽象化するシングルトン。
/// 現在はダミー実装（即成功を返す）。
/// 将来 Unity Ads / AdMob 等の SDK を導入したら
/// ShowRewardedAd() の中身だけ差し替えればよい。
///
/// 【方針A】
///   リワード広告の結果は「復活してよいか(bool)」で返す。
///   - 報酬獲得         → true（復活）
///   - ユーザー途中スキップ → true（復活）※方針Aのため見た扱い
///   - ロード失敗/表示失敗/オフライン/在庫切れ → true（復活）
///   実質、コールバックが返ってくる限り常に true。
///   コールバックが返らないフリーズ対策として呼び出し側でタイムアウト救済する。
/// </summary>
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

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // =========================================================
    // リワード広告を表示する
    // =========================================================
    // onResult: true = 復活してよい
    //
    // 【方針A】報酬獲得・スキップ・各種失敗のいずれでも true を返す。
    //   呼び出し側はこの bool が true なら復活処理を行う。
    // =========================================================

    /// <summary>
    /// リワード広告を表示し、結果をコールバックで返す。
    /// </summary>
    /// <param name="onResult">true = 復活OK</param>
    public void ShowRewardedAd(Action<bool> onResult)
    {
        // --- ダミー実装: 広告SDK未導入のため即成功を返す ---
        Debug.Log("[AdManager] (ダミー) リワード広告を表示 → 即成功");
        onResult?.Invoke(true);

        // =========================================================
        // 【将来の AdMob 実装イメージ（方針A）】
        //
        // bool rewardEarned = false;
        //
        // // 広告がロードできていない（オフライン/在庫切れ/ネットワークエラー）
        // if (rewardedAd == null)
        // {
        //     Debug.LogWarning("[AdManager] 広告未ロード → 見た扱いで復活(true)");
        //     onResult?.Invoke(true);   // 方針A: 失敗でも復活
        //     LoadRewardedAd();         // 次回のために再ロードを試みる
        //     return;
        // }
        //
        // // 報酬獲得イベント
        // rewardedAd.OnUserEarnedReward += (s, e) => { rewardEarned = true; };
        //
        // // 広告が閉じられた時（報酬獲得・スキップ問わずここに来る）
        // rewardedAd.OnAdFullScreenContentClosed += () =>
        // {
        //     // 方針A: rewardEarned に関わらず常に復活
        //     onResult?.Invoke(true);
        //     LoadRewardedAd(); // 次回分を再ロード
        // };
        //
        // // 表示失敗（再生中のエラー等）
        // rewardedAd.OnAdFullScreenContentFailed += (error) =>
        // {
        //     Debug.LogWarning($"[AdManager] 表示失敗 → 見た扱いで復活(true): {error}");
        //     onResult?.Invoke(true); // 方針A: 失敗でも復活
        //     LoadRewardedAd();
        // };
        //
        // rewardedAd.Show(...);
        // =========================================================
    }
}