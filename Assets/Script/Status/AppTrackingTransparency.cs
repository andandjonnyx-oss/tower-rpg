using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// iOS の App Tracking Transparency（ATT）許諾ダイアログ。
///
/// 【なぜ必要か】
///   iOS 14 以降、IDFA を使った広告トラッキングにはこのダイアログでの許諾が要る。
///   ダイアログを一切出さないまま広告 SDK を動かすと App Store 審査でリジェクトされる。
///   ⚠️ Info.plist の NSUserTrackingUsageDescription が空だとダイアログ自体が
///   表示されないため、IosPostProcessBuild.cs が必ず文言を書き込んでいる。
///
/// 【使い方】
///   yield return AppTrackingTransparency.RequestIfNeeded();
///   を MobileAds.Initialize() の直前で待つ（AdManager が実施済み）。
///   iOS 実機以外（Android / Editor）では即座に完了するので分岐は不要。
///
/// 【ポーリングにしている理由】
///   ネイティブのコールバックを C# へ返すと IL2CPP でのマーシャリングが必要になり
///   AOT 制約を踏みやすい。状態取得は同期関数なので、コルーチンで監視する方が安全。
/// </summary>
public static class AppTrackingTransparency
{
    /// <summary>ATTrackingManagerAuthorizationStatus と同じ並び。</summary>
    public enum Status
    {
        NotDetermined = 0,
        Restricted    = 1,
        Denied        = 2,
        Authorized    = 3,
    }

    /// <summary>アプリがアクティブになるのを待つ上限（秒）。</summary>
    private const float FocusWaitSeconds = 5f;

    /// <summary>ダイアログが出ないまま経過した場合に再要求するまでの秒数。</summary>
    private const float RetryAfterSeconds = 3f;

    /// <summary>許諾結果を待つ上限（秒）。超えたら諦めて先へ進む。</summary>
    private const float TimeoutSeconds = 60f;

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern int _TowerRpgGetTrackingAuthorizationStatus();

    [DllImport("__Internal")]
    private static extern void _TowerRpgRequestTrackingAuthorization();
#endif

    /// <summary>現在の許諾状態。iOS 実機以外では常に Authorized を返す。</summary>
    public static Status CurrentStatus
    {
        get
        {
#if UNITY_IOS && !UNITY_EDITOR
            return (Status)_TowerRpgGetTrackingAuthorizationStatus();
#else
            return Status.Authorized;
#endif
        }
    }

    /// <summary>
    /// 未判定ならダイアログを出し、ユーザーが選ぶまで待つ。
    /// 判定済み・iOS 実機以外は即座に完了する。
    /// </summary>
    public static IEnumerator RequestIfNeeded()
    {
#if UNITY_IOS && !UNITY_EDITOR
        if (CurrentStatus != Status.NotDetermined)
        {
            Debug.Log($"[ATT] 判定済みのため要求しない: {CurrentStatus}");
            yield break;
        }

        // アプリがアクティブになる前に要求してもダイアログは表示されず、
        // NotDetermined のまま握り潰される。フォーカスを待ってから要求する。
        float waited = 0f;
        while (!Application.isFocused && waited < FocusWaitSeconds)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        _TowerRpgRequestTrackingAuthorization();

        float elapsed = 0f;
        bool retried = false;

        while (CurrentStatus == Status.NotDetermined && elapsed < TimeoutSeconds)
        {
            elapsed += Time.unscaledDeltaTime;

            // 要求が早すぎてダイアログが出なかった場合の保険。
            // 既に判定済みなら iOS 側が即座に返すだけなので二重表示にはならない。
            if (!retried && elapsed > RetryAfterSeconds && Application.isFocused)
            {
                retried = true;
                _TowerRpgRequestTrackingAuthorization();
                Debug.Log("[ATT] ダイアログが出なかったため再要求");
            }

            yield return null;
        }

        Debug.Log($"[ATT] 結果: {CurrentStatus}（待機 {elapsed:F1} 秒）");
#else
        // Android / Editor では ATT が存在しないので何もしない。
        yield break;
#endif
    }
}
