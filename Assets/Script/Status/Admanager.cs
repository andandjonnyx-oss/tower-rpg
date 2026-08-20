using System;
using System.Collections;
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
    private const string IosRewardedAdUnitId = "ca-app-pub-7063976043351494/3825356010";

    // =========================================================
    // ⚠ TestFlight 配布用の一時設定（2026-08-21 追加）
    //
    // 外部テスターの iPhone は TestDeviceIds に登録できない。TestFlight 経由では
    // 端末ID がコンソールに出ず取得手段が無いため、本番ID のまま配布すると
    // 無効トラフィック判定 → AdMob アカウント停止のリスクがある。
    // そのため TestFlight 配布中は iOS だけテスト用ユニットID に差し替える。
    //
    // ⚠ App Store 公開前に必ず false へ戻すこと。true のままだと iOS の
    //    広告収益がゼロになる（テスト広告は収益が発生しない）。
    // ⚠ Android は本番稼働中のため、このフラグの対象外にしてある。
    // =========================================================
    private const bool UseIosTestAdUnitId = true;
    private const string IosTestRewardedAdUnitId = "ca-app-pub-3940256099942544/1712485313";

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
            return UseIosTestAdUnitId ? IosTestRewardedAdUnitId : IosRewardedAdUnitId;
#else
            return string.Empty;
#endif
        }
    }

    public static AdManager Instance { get; private set; }

    private RewardedAd rewardedAd;
    private bool adsInitialized;
    private bool isLoading;

    // UMP のコールバックはバックグラウンドスレッドで来ることがあり、
    // そこから StartCoroutine は呼べない。フラグを立てて Update() で拾う。
    private volatile bool pendingAdsInitialize;

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
                    NotifyConsentResolved();
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

    // =========================================================
    // 同意状態の共有（解析データ収集の可否判定に使う）
    // =========================================================

    /// <summary>
    /// UMP の同意状態が確定／変更されたときに発火する。
    /// AnalyticsManager.ApplyConsent() を繋いでおくと、同意結果に追従できる。
    /// static なので購読側も static メソッドを使うこと（シーン跨ぎのリーク防止）。
    /// </summary>
    public static event Action OnConsentResolved;

    private static void NotifyConsentResolved()
    {
        try
        {
            OnConsentResolved?.Invoke();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[AdManager] 同意確定通知で例外: {e.Message}");
        }
    }

    /// <summary>
    /// 解析データ（UGS Analytics）を収集してよいか。
    /// 同意が必要な地域かどうかの判定は UMP に一本化している（同意 UI を二重に出さないため）。
    /// </summary>
    public static bool IsAnalyticsConsentGranted
    {
        get
        {
            // ユーザーが設定で明示的に拒否していれば、地域に関わらず収集しない。
            if (GameSettings.AnalyticsOptOut) return false;

            try
            {
                var status = ConsentInformation.ConsentStatus;
                // NotRequired = 同意不要な地域（日本など） / Obtained = 同意取得済み
                return status == ConsentStatus.NotRequired || status == ConsentStatus.Obtained;
            }
            catch (Exception)
            {
                // UMP 未初期化などで判定できないときは、安全側（収集しない）に倒す。
                return false;
            }
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

        // 実際の初期化は ATT ダイアログを挟む必要があり、コルーチン＝メインスレッド
        // でしか回せない。ここは UMP のコールバック（別スレッド）から呼ばれ得るので、
        // フラグだけ立てて Update() に引き渡す。
        pendingAdsInitialize = true;
    }

    private void Update()
    {
        if (!pendingAdsInitialize) return;
        pendingAdsInitialize = false;
        StartCoroutine(RequestAttThenInitialize());
    }

    /// <summary>
    /// ATT（iOS）の許諾を取ってから Google Mobile Ads を初期化する。
    ///
    /// 順序は UMP 同意フォーム → ATT → MobileAds.Initialize() の3段。
    /// GDPR 側（UMP）を先に処理したうえで、広告 SDK が IDFA を掴む前に
    /// ATT を確定させる必要があるため、この順番を崩さないこと。
    /// iOS 実機以外では ATT の待機は即座に抜ける。
    /// </summary>
    private IEnumerator RequestAttThenInitialize()
    {
        yield return AppTrackingTransparency.RequestIfNeeded();

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
            {
                Debug.LogWarning($"[AdManager] プライバシー選択フォームの表示に失敗: {showError.Message}");
                return;
            }

            InitializeAdsIfAllowed();  // 同意に変わった可能性があるため再試行
            NotifyConsentResolved();   // 解析側の同意状態も追従させる
        });
    }

    // =========================================================
    // 広告のロード
    // =========================================================

    private void LoadRewardedAd()
    {
        // ロード中の重複要求を弾く。
        // これが無いと、ロード完了前にユーザーが再度広告を要求したときに
        // Load が二重に走り、後から完了した方が rewardedAd を上書きして
        // 先に完了した広告が Destroy されないまま漏れる。
        if (isLoading) return;

        DestroyRewardedAd();
        isLoading = true;

        Debug.Log("[AdManager] リワード広告のロード開始");
        RewardedAd.Load(RewardedAdUnitId, new AdRequest(), (RewardedAd ad, LoadAdError error) =>
        {
            isLoading = false;

            if (error != null || ad == null)
            {
                Debug.LogWarning($"[AdManager] リワード広告のロード失敗: {error}");
                return;
            }

            Debug.Log("[AdManager] リワード広告のロード完了");
            DestroyRewardedAd(); // 念のため、保持中の広告があれば先に破棄する
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
