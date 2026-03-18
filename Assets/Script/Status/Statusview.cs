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
    [SerializeField] private TMP_Text powerText;        // 力
    [SerializeField] private TMP_Text staminaText;      // 体力
    [SerializeField] private TMP_Text dexterityText;    // 器用
    [SerializeField] private TMP_Text magicPowerText;   // 魔力
    [SerializeField] private TMP_Text luckText;         // 運の良さ

    [Header("Buttons")]
    [SerializeField] private Button closeButton;        // ×ボタン
    [SerializeField] private Button toggleButton;       // 詳細/基礎 切替ボタン
    [SerializeField] private TMP_Text toggleButtonText; // ボタンのラベル

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

        // リセットボタン
        if (resetButton != null) resetButton.onClick.AddListener(OnResetClicked);

        // ×ボタン
        if (closeButton != null) closeButton.onClick.AddListener(OnCloseClicked);

        // 詳細/基礎 切替
        if (toggleButton != null) toggleButton.onClick.AddListener(OnToggleClicked);
    }

    private void Start()
    {
        // ポイント振り分けのスナップショットを取る
        if (GameState.I != null)
            GameState.I.TakeStatSnapshot();

        // 初期表示は Status1
        showingStatus1 = true;
        ApplyPanelVisibility();
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
        if (expToNextText != null) expToNextText.text = $"必要経験値：{gs.expToNext}";
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
        if (powerText != null) powerText.text = $"力：{gs.Power}";
        if (staminaText != null) staminaText.text = $"体力：{gs.Stamina}";
        if (dexterityText != null) dexterityText.text = $"器用：{gs.Dexterity}";
        if (magicPowerText != null) magicPowerText.text = $"魔力：{gs.MagicPower}";
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

    private void OnResetClicked()
    {
        if (GameState.I == null) return;
        GameState.I.ResetStatAllocation();
        RefreshAll();
    }

    private void OnToggleClicked()
    {
        showingStatus1 = !showingStatus1;
        ApplyPanelVisibility();
        RefreshAll();   // 詳細に切り替えた際に最新値を反映
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
            // フォールバック: 戻り先不明なら Tower へ
            SceneManager.LoadScene("Tower");
        }
    }
}