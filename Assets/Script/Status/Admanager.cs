using System;
using System.Collections.Generic;
using UnityEngine;
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;
using GoogleMobileAds.Ump.Api;

/// <summary>
/// 広告表示を抽象化するシングルトン（AdMob / Google Mobile Ads SDK 実装）。
///
/// 【呼び出し側から見た契約（ダミー実装時代から不変）】
///   ShowRewardedAd(Action&lt;bool&gt; onResult) を呼ぶと、結果が bool で返る。
///   true = 報酬を与えてよい。
///
/// 【方針A（既定）】
///   リワード広告の結果は「報酬を与えてよいか(bool)」で返す。
///   - 報酬獲得              → true
///   - ユーザー途中スキップ  → true ※方針Aのため見た扱い
///   - ロード失敗/表示失敗/オフライン/在庫切れ → true
///   実質、コールバックが返ってくる限り常に true。
///   コールバックが返らないフリーズ対策として呼び出し側でタイムアウト救済する
///   （BattleSceneController の AdTimeoutFallback）。
///
/// 【方針Aを使わない場合】
///   ShowRewardedAd(onResult, grantOnFailure: false) を使うと
///   「実際に報酬イベントが発火したときだけ true」になる。
///   コンティニュー復活は方針A(true)、倉庫解放/ステータス振り直しは厳格(false)、
///   といった使い分けが必要ならこちらを呼ぶこと。
///
/// 【初期化順序（重要）】
///   UMP で同意を取得 → ConsentInformation.CanRequestAds() が true
///   → MobileAds.Initialize() → RewardedAd.Load()
///   の順に進む。EEA/UK/スイス向けは Google 認定 CMP（＝UMP）の経由が必須。
///   同意取得前に MobileAds.Initialize() を呼ばないこと。
///
/// 【広告ユニットID】
///   下記 AndroidRewardedAdUnitId / IosRewardedAdUnitId は Google 公式の
///   「テスト用」ID。本番リリース前に AdMob 管理画面で発行した実IDへ差し替えること。
///   併せて Assets &gt; Google Mobile Ads &gt; Settings に AdMob アプリID を入力する
///   （こちらを入れないと起動時にクラッシュする）。
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
    // =========================================================
    // 広告ユニットID（本番）
    //
    // ⚠️ 自分の端末でこのIDの広告をタップすると「無効なトラフィック」と判定され、
    //    AdMob アカウント停止のリスクがある。実機で動作確認するときは
    //    必ず下記 TestDeviceIds に自端末を登録するか、テストIDに差し替えること。
    //
    //    Google 公式テストID:
    //      Android : ca-app-pub-3940256099942544/5224354917
    //      iOS     : ca-app-pub-3940256099942544/1712485313
    // =========================================================
    private const string AndroidRewardedAdUnitId = "ca-app-pub-7063976043351494/7011853210";
    private const string IosRewardedAdUnitId = "";

    /// <summary>
    /// テスト広告を配信する端末のID。
    /// 実機を1度起動すると logcat / Unity Console に
    /// 「Use RequestConfiguration.Builder().setTestDeviceIds(Arrays.asList("XXXX"))」
    /// の形で自端末のIDが出るので、それをここに追加する。
    /// 登録した端末には本番ユニットIDのままテスト広告が配信され、タップしても安全。
    /// </summary>
    private static readonly List<string> TestDeviceIds = new List<string>
    {
        "51CE05C781594046217C163F2B82FBB2", // Pixel 7a（開発機）
    };

    private static string RewardedAdUnitId
    {
        get
        {
#if UNITY_ANDROID
            return AndroidRewardedAdUnitId;
#elif UNITY_IPHONE
            return IosRewardedAdUnitId;
#else
            return string.Empty;
#endif
        }
    }

    public static AdManager Instance { get; private set; }

    private RewardedAd rewardedAd;
    private bool adsInitialized;

    // 表示中の1回分の状態
    private bool isShowing;
    private Action<bool> pendingCallback;
    private bool grantOnFailureForCurrent;
    private bool rewardEarnedForCurrent;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // ※ SDK のコールバックはバックグラウンドスレッドで来ることがある。
        //   Unity 側（SceneManager / UI）を触る処理は
        //   MobileAdsEventExecutor.ExecuteInUpdate() でメインスレッドへ回すこと。
        //   （旧 MobileAds.RaiseAdEventsOnUnityMainThread は v11 で非推奨）
        RequestConsentThenInitialize();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        DestroyRewardedAd();
    }

    // =========================================================
    // 同意取得（UMP）→ SDK 初期化
    // =========================================================

    private void RequestConsentThenInitialize()
    {
        try
        {
            var request = new ConsentRequestParameters();

            ConsentInformation.Update(request, updateError =>
            {
                if (updateError != null)
                    Debug.LogWarning($"[AdManager] 同意情報の更新に失敗: {updateError.Message}");

                ConsentForm.LoadAndShowConsentFormIfRequired(formError =>
                {
                    if (formError != null)
                        Debug.LogWarning($"[AdManager] 同意フォームの表示に失敗: {formError.Message}");

                    InitializeAdsIfAllowed();
                });
            });

            // 2回目以降の起動は同意がキャッシュされているため、
            // 上のコールバックを待たずに初期化できる。
            InitializeAdsIfAllowed();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[AdManager] 同意フローで例外（広告なしで継続）: {e.Message}");
        }
    }

    private void InitializeAdsIfAllowed()
    {
        if (adsInitialized) return;

        if (!ConsentInformation.CanRequestAds())
        {
            Debug.Log("[AdManager] 同意が未取得のため広告を初期化しない");
            return;
        }

        adsInitialized = true;

        // テスト端末を登録しておくと、本番ユニットIDのままテスト広告が配信される。
        if (TestDeviceIds.Count > 0)
        {
            MobileAds.SetRequestConfiguration(new RequestConfiguration
            {
                TestDeviceIds = TestDeviceIds,
            });
            Debug.Log($"[AdManager] テスト端末を{TestDeviceIds.Count}件登録");
        }

        MobileAds.Initialize(status =>
        {
            MobileAdsEventExecutor.ExecuteInUpdate(() =>
            {
                Debug.Log("[AdManager] Google Mobile Ads 初期化完了");
                LoadRewardedAd();
            });
        });
    }

    // =========================================================
    // プライバシー選択の再表示（Option シーンのボタンから呼ぶ想定）
    // UMP のポリシー上、Required のときは再表示の導線が必須。
    // =========================================================

    /// <summary>プライバシー選択フォームの導線を出す必要があるか。</summary>
    public bool IsPrivacyOptionsRequired
    {
        get
        {
            try
            {
                return ConsentInformation.PrivacyOptionsRequirementStatus
                       == PrivacyOptionsRequirementStatus.Required;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    /// <summary>プライバシー選択フォームを表示する。</summary>
    public void ShowPrivacyOptionsForm()
    {
        ConsentForm.ShowPrivacyOptionsForm(showError =>
        {
            if (showError != null)
                Debug.LogWarning($"[AdManager] プライバシー選択フォームの表示に失敗: {showError.Message}");
            else
                InitializeAdsIfAllowed(); // 同意に変わった可能性があるため再試行
        });
    }

    // =========================================================
    // 広告のロード
    // =========================================================

    private void LoadRewardedAd()
    {
        DestroyRewardedAd();

        Debug.Log("[AdManager] リワード広告のロード開始");
        RewardedAd.Load(RewardedAdUnitId, new AdRequest(), (RewardedAd ad, LoadAdError error) =>
        {
            if (error != null || ad == null)
            {
                Debug.LogWarning($"[AdManager] リワード広告のロード失敗: {error}");
                return;
            }

            Debug.Log("[AdManager] リワード広告のロード完了");
            rewardedAd = ad;
            RegisterEventHandlers(ad);
        });
    }

    private void DestroyRewardedAd()
    {
        if (rewardedAd == null) return;
        rewardedAd.Destroy();
        rewardedAd = null;
    }

    private void RegisterEventHandlers(RewardedAd ad)
    {
        // 広告が閉じられた（報酬獲得・スキップ問わずここに来る）
        ad.OnAdFullScreenContentClosed += () =>
        {
            // 呼び出し側は SceneManager.LoadScene 等を実行するため、必ずメインスレッドへ。
            MobileAdsEventExecutor.ExecuteInUpdate(() =>
            {
                // 方針A: grantOnFailure=true なら報酬有無に関わらず true。
                // 厳格モード(false)なら報酬イベントが発火したときだけ true。
                bool result = rewardEarnedForCurrent || grantOnFailureForCurrent;
                Debug.Log($"[AdManager] 広告が閉じられた（報酬={rewardEarnedForCurrent}） → {result}");
                FinishShow(result);
                LoadRewardedAd(); // 次回分を再ロード
            });
        };

        // 表示失敗（再生中のエラー等）
        ad.OnAdFullScreenContentFailed += (AdError adError) =>
        {
            MobileAdsEventExecutor.ExecuteInUpdate(() =>
            {
                Debug.LogWarning($"[AdManager] 広告の表示失敗: {adError} → {grantOnFailureForCurrent}");
                FinishShow(grantOnFailureForCurrent);
                LoadRewardedAd();
            });
        };
    }

    // =========================================================
    // リワード広告を表示する
    // =========================================================

    /// <summary>
    /// リワード広告を表示し、結果をコールバックで返す（方針A: 失敗でも true）。
    /// </summary>
    /// <param name="onResult">true = 報酬を与えてよい</param>
    public void ShowRewardedAd(Action<bool> onResult)
    {
        ShowRewardedAd(onResult, true);
    }

    /// <summary>
    /// リワード広告を表示し、結果をコールバックで返す。
    /// </summary>
    /// <param name="onResult">true = 報酬を与えてよい</param>
    /// <param name="grantOnFailure">
    /// true  = 方針A。ロード失敗・表示失敗・途中スキップでも true を返す。
    /// false = 厳格。実際に報酬イベントが発火したときだけ true を返す。
    /// </param>
    public void ShowRewardedAd(Action<bool> onResult, bool grantOnFailure)
    {
        if (isShowing)
        {
            // 二重呼び出し。前回の表示が処理中なので、こちらは即決着させる。
            Debug.LogWarning("[AdManager] 広告表示中に再度呼ばれた → 即結果を返す");
            onResult?.Invoke(grantOnFailure);
            return;
        }

        // エディタ／未対応プラットフォームでは実広告を出せないので即成功扱い。
        bool canShowRealAd = Application.platform == RuntimePlatform.Android
                             || Application.platform == RuntimePlatform.IPhonePlayer;

        if (!canShowRealAd)
        {
            Debug.Log("[AdManager] 実機以外のため広告をスキップ → 成功扱い");
            onResult?.Invoke(true);
            return;
        }

        if (rewardedAd == null || !rewardedAd.CanShowAd())
        {
            Debug.LogWarning($"[AdManager] 広告が未ロード（オフライン/在庫切れ等） → {grantOnFailure}");
            LoadRewardedAd(); // 次回のために再ロードを試みる
            onResult?.Invoke(grantOnFailure);
            return;
        }

        isShowing = true;
        pendingCallback = onResult;
        grantOnFailureForCurrent = grantOnFailure;
        rewardEarnedForCurrent = false;

        rewardedAd.Show(reward =>
        {
            rewardEarnedForCurrent = true;
            Debug.Log($"[AdManager] 報酬獲得: {reward.Amount} {reward.Type}");
        });
    }

    /// <summary>
    /// 表示1回分の決着をつける。多重コールバックでも onResult は1回だけ呼ばれる。
    /// </summary>
    private void FinishShow(bool result)
    {
        isShowing = false;

        var callback = pendingCallback;
        pendingCallback = null;
        if (callback == null) return;

        try
        {
            callback.Invoke(result);
        }
        catch (Exception e)
        {
            Debug.LogError($"[AdManager] 結果コールバックで例外: {e}");
        }
    }
}
