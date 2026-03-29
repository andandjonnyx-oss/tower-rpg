using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Status シーンの Canvas にアタッチする。
/// Status1（基礎）と Status2（詳細）の表示切替、ポイント振り分け、
/// ×ボタンで元のシーンへ戻る処理を担当する。
/// </summary>
public class StatusView : MonoBehaviour
{
    // =========================================================
    // Inspector 参照
    // =========================================================

    [Header("Panels")]
    [SerializeField] private GameObject status1Panel;   // 基礎ステータスパネル
    [SerializeField] private GameObject status2Panel;   // 詳細ステータスパネル

    [Header("Common Info")]
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text expToNextText;
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private TMP_Text mpText;

    [Header("Status1 - Base Stats")]
    [SerializeField] private TMP_Text strText;
    [SerializeField] private TMP_Text vitText;
    [SerializeField] private TMP_Text intText;
    [SerializeField] private TMP_Text dexText;
    [SerializeField] private TMP_Text lucText;
    [SerializeField] private TMP_Text pointText;        // ステータスポイント表示

    [Header("Status1 - Plus Buttons")]
    [SerializeField] private Button strPlusButton;
    [SerializeField] private Button vitPlusButton;
    [SerializeField] private Button intPlusButton;
    [SerializeField] private Button dexPlusButton;
    [SerializeField] private Button lucPlusButton;

    [Header("Status1 - Reset Button")]
    [SerializeField] private Button resetButton;

    [Header("Status2 - Derived Stats")]
    [SerializeField] private TMP_Text attackText;       // 攻撃力
    [SerializeField] private TMP_Text defenseText;      // 防御力
    [SerializeField] private TMP_Text magicAttackText;  // 魔法攻撃力
    [SerializeField] private TMP_Text magicDefenseText; // 魔法防御力
    [SerializeField] private TMP_Text luckText;         // 運の良さ

    [Header("Buttons")]
    [SerializeField] private Button closeButton;        // ×ボタン
    [SerializeField] private Button toggleButton;       // 詳細/基礎 切替ボタン
    [SerializeField] private TMP_Text toggleButtonText; // ボタンのラベル

    [Header("Reset Confirm Popup")]
    [SerializeField] private GameObject resetConfirmPopup;  // 確認ポップアップのルート
    [SerializeField] private Button resetConfirmYes;        // 「はい（広告を見る）」ボタン
    [SerializeField] private Button resetConfirmNo;         // 「いいえ」ボタン

    // =========================================================
    // 内部状態
    // =========================================================
    private bool showingStatus1 = true;

    // =========================================================
    // 初期化
    // =========================================================
    private void Awake()
    {
        // ＋ボタン登録
        if (strPlusButton != null) strPlusButton.onClick.AddListener(() => OnPlusClicked(StatType.STR));
        if (vitPlusButton != null) vitPlusButton.onClick.AddListener(() => OnPlusClicked(StatType.VIT));
        if (intPlusButton != null) intPlusButton.onClick.AddListener(() => OnPlusClicked(StatType.INT));
        if (dexPlusButton != null) dexPlusButton.onClick.AddListener(() => OnPlusClicked(StatType.DEX));
        if (lucPlusButton != null) lucPlusButton.onClick.AddListener(() => OnPlusClicked(StatType.LUC));

        // リセットボタン → 確認ポップアップを開く
        if (resetButton != null) resetButton.onClick.AddListener(OnResetClicked);

        // ×ボタン
        if (closeButton != null) closeButton.onClick.AddListener(OnCloseClicked);

        // 詳細/基礎 切替
        if (toggleButton != null) toggleButton.onClick.AddListener(OnToggleClicked);

        // リセット確認ポップアップのボタン
        if (resetConfirmYes != null) resetConfirmYes.onClick.AddListener(OnResetConfirmYes);
        if (resetConfirmNo != null) resetConfirmNo.onClick.AddListener(OnResetConfirmNo);
    }

    private void Start()
    {
        // ポイント振り分けのスナップショットを取る
        if (GameState.I != null)
            GameState.I.TakeStatSnapshot();

        // 初期表示は Status1、ポップアップは閉じておく
        showingStatus1 = true;
        ApplyPanelVisibility();
        if (resetConfirmPopup != null) resetConfirmPopup.SetActive(false);
        RefreshAll();
    }

    // =========================================================
    // 表示更新
    // =========================================================
    private void RefreshAll()
    {
        var gs = GameState.I;
        if (gs == null) return;

        // 共通
        if (levelText != null) levelText.text = $"レベル：{gs.level}";
        if (expToNextText != null) expToNextText.text = $"必要経験値：{gs.expToNext - gs.currentExp}";

        if (hpText != null) hpText.text = $"HP：{gs.currentHp}/{gs.maxHp}";
        if (mpText != null) mpText.text = $"MP：{gs.currentMp}/{gs.maxMp}";

        // Status1
        if (strText != null) strText.text = $"STR：{gs.baseSTR}";
        if (vitText != null) vitText.text = $"VIT：{gs.baseVIT}";
        if (intText != null) intText.text = $"INT：{gs.baseINT}";
        if (dexText != null) dexText.text = $"DEX：{gs.baseDEX}";
        if (lucText != null) lucText.text = $"LUC：{gs.baseLUC}";
        if (pointText != null) pointText.text = $"ステータスポイント：{gs.statusPoint}";

        // ＋ボタンの有効/無効
        bool canAllocate = gs.statusPoint > 0;
        if (strPlusButton != null) strPlusButton.interactable = canAllocate;
        if (vitPlusButton != null) vitPlusButton.interactable = canAllocate;
        if (intPlusButton != null) intPlusButton.interactable = canAllocate;
        if (dexPlusButton != null) dexPlusButton.interactable = canAllocate;
        if (lucPlusButton != null) lucPlusButton.interactable = canAllocate;

        // Status2
        if (attackText != null) attackText.text = $"攻撃力：{gs.Attack}";
        if (defenseText != null) defenseText.text = $"防御力：{gs.Defense}";
        if (magicAttackText != null) magicAttackText.text = $"魔法攻撃力：{gs.MagicAttack}";
        if (magicDefenseText != null) magicDefenseText.text = $"魔法防御力：{gs.MagicDefense}";
        if (luckText != null) luckText.text = $"運の良さ：{gs.Luck}";
    }

    private void ApplyPanelVisibility()
    {
        if (status1Panel != null) status1Panel.SetActive(showingStatus1);
        if (status2Panel != null) status2Panel.SetActive(!showingStatus1);

        if (toggleButtonText != null)
            toggleButtonText.text = showingStatus1 ? "詳細" : "基礎";
    }

    // =========================================================
    // ボタンハンドラ
    // =========================================================

    private void OnPlusClicked(StatType stat)
    {
        if (GameState.I == null) return;
        if (GameState.I.AllocatePoint(stat))
            RefreshAll();
    }

    /// リセットボタン → 確認ポップアップを表示
    private void OnResetClicked()
    {
        if (resetConfirmPopup != null)
        {
            resetConfirmPopup.SetActive(true);
        }
        else
        {
            // ポップアップ未設定の場合はそのままリセット（従来動作）
            ExecuteReset();
        }
    }

    /// 確認ポップアップ「はい（広告を見る）」
    private void OnResetConfirmYes()
    {
        if (resetConfirmPopup != null)
            resetConfirmPopup.SetActive(false);

        // 広告を表示し、完了後にリセットを実行
        if (AdManager.Instance != null)
        {
            AdManager.Instance.ShowRewardedAd(success =>
            {
                if (success)
                {
                    Debug.Log("[StatusView] 広告視聴完了 → ステータスリセット実行");
                    ExecuteReset();
                }
                else
                {
                    Debug.Log("[StatusView] 広告視聴失敗/キャンセル → リセットしない");
                }
            });
        }
        else
        {
            // AdManager 未設定の場合はそのままリセット
            Debug.LogWarning("[StatusView] AdManager が見つかりません。直接リセットします。");
            ExecuteReset();
        }
    }

    /// 確認ポップアップ「いいえ」
    private void OnResetConfirmNo()
    {
        if (resetConfirmPopup != null)
            resetConfirmPopup.SetActive(false);
    }

    /// 実際のリセット処理
    private void ExecuteReset()
    {
        if (GameState.I == null) return;
        GameState.I.ResetStatAllocation();
        RefreshAll();
    }

    private void OnToggleClicked()
    {
        showingStatus1 = !showingStatus1;
        ApplyPanelVisibility();
        RefreshAll();
    }

    private void OnCloseClicked()
    {
        var gs = GameState.I;
        if (gs != null && !string.IsNullOrEmpty(gs.previousSceneName))
        {
            string dest = gs.previousSceneName;
            gs.previousSceneName = "";
            SceneManager.LoadScene(dest);
        }
        else
        {
            SceneManager.LoadScene("Tower");
        }
    }
}