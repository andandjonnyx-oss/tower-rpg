using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

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

    // ===== ↓ここから追加 =====
    [Header("Debug - Bulk Allocate")]
    [SerializeField] private Button strPlus10Button;
    [SerializeField] private Button vitPlus10Button;
    [SerializeField] private Button intPlus10Button;
    [SerializeField] private Button dexPlus10Button;
    [SerializeField] private Button lucPlus10Button;
    [SerializeField] private Button strPlus100Button;
    [SerializeField] private Button vitPlus100Button;
    [SerializeField] private Button intPlus100Button;
    [SerializeField] private Button dexPlus100Button;
    [SerializeField] private Button lucPlus100Button;

    [Header("Status2 - Derived Stats (Left Column)")]
    [SerializeField] private TMP_Text attackText;       // 攻撃力
    [SerializeField] private TMP_Text defenseText;      // 防御力
    [SerializeField] private TMP_Text magicAttackText;  // 魔法攻撃力
    [SerializeField] private TMP_Text magicDefenseText; // 魔法防御力
    [SerializeField] private TMP_Text accuracyText;     // 命中力
    [SerializeField] private TMP_Text evasionText;      // 回避率
    [SerializeField] private TMP_Text criticalText;     // クリティカル率
    [SerializeField] private TMP_Text luckText;         // 運の良さ

    [Header("Status2 - Attribute Resistances (Center Column)")]
    [SerializeField] private TMP_Text resStrikeText;    // 殴耐性
    [SerializeField] private TMP_Text resSlashText;     // 斬耐性
    [SerializeField] private TMP_Text resPierceText;    // 突耐性
    [SerializeField] private TMP_Text resFireText;      // 火耐性
    [SerializeField] private TMP_Text resIceText;       // 氷耐性
    [SerializeField] private TMP_Text resThunderText;   // 雷耐性
    [SerializeField] private TMP_Text resHolyText;      // 聖耐性
    [SerializeField] private TMP_Text resDarkText;      // 闇耐性

    [Header("Status2 - Status Effect Resistances (Right Column)")]
    [SerializeField] private TMP_Text resPoisonText;    // 毒耐性
    [SerializeField] private TMP_Text resParalyzeText;  // 麻痺耐性
    [SerializeField] private TMP_Text resBlindText;     // 暗闇耐性
    [SerializeField] private TMP_Text resSilenceText;   // 沈黙耐性
    [SerializeField] private TMP_Text resDebuffText;    // デバフ耐性

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

        if (strPlus10Button != null) strPlus10Button.onClick.AddListener(() => OnBulkAllocate(StatType.STR, 10));
        if (vitPlus10Button != null) vitPlus10Button.onClick.AddListener(() => OnBulkAllocate(StatType.VIT, 10));
        if (intPlus10Button != null) intPlus10Button.onClick.AddListener(() => OnBulkAllocate(StatType.INT, 10));
        if (dexPlus10Button != null) dexPlus10Button.onClick.AddListener(() => OnBulkAllocate(StatType.DEX, 10));
        if (lucPlus10Button != null) lucPlus10Button.onClick.AddListener(() => OnBulkAllocate(StatType.LUC, 10));

        // デバッグ用 +100 ボタン
        if (strPlus100Button != null) strPlus100Button.onClick.AddListener(() => OnBulkAllocate(StatType.STR, 100));
        if (vitPlus100Button != null) vitPlus100Button.onClick.AddListener(() => OnBulkAllocate(StatType.VIT, 100));
        if (intPlus100Button != null) intPlus100Button.onClick.AddListener(() => OnBulkAllocate(StatType.INT, 100));
        if (dexPlus100Button != null) dexPlus100Button.onClick.AddListener(() => OnBulkAllocate(StatType.DEX, 100));
        if (lucPlus100Button != null) lucPlus100Button.onClick.AddListener(() => OnBulkAllocate(StatType.LUC, 100));

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

        // +10/+100 ボタンの有効/無効（ポイントが1以上あれば有効）
        if (strPlus10Button != null) strPlus10Button.interactable = canAllocate;
        if (vitPlus10Button != null) vitPlus10Button.interactable = canAllocate;
        if (intPlus10Button != null) intPlus10Button.interactable = canAllocate;
        if (dexPlus10Button != null) dexPlus10Button.interactable = canAllocate;
        if (lucPlus10Button != null) lucPlus10Button.interactable = canAllocate;
        if (strPlus100Button != null) strPlus100Button.interactable = canAllocate;
        if (vitPlus100Button != null) vitPlus100Button.interactable = canAllocate;
        if (intPlus100Button != null) intPlus100Button.interactable = canAllocate;
        if (dexPlus100Button != null) dexPlus100Button.interactable = canAllocate;
        if (lucPlus100Button != null) lucPlus100Button.interactable = canAllocate;

        // =====================================================
        // Status2 — 左列: 戦闘ステータス
        // =====================================================
        if (attackText != null) attackText.text = $"攻撃力：{gs.Attack}";
        if (defenseText != null) defenseText.text = $"防御力：{gs.Defense}";
        if (magicAttackText != null) magicAttackText.text = $"魔法攻撃力：{gs.MagicAttack}";
        if (magicDefenseText != null) magicDefenseText.text = $"魔法防御力：{gs.MagicDefense}";
        if (accuracyText != null) accuracyText.text = $"命中力：{gs.Accuracy}";
        if (evasionText != null) evasionText.text = $"回避率：{gs.Evasion:F2}%";
        if (criticalText != null) criticalText.text = $"クリティカル率：{gs.CriticalRate:F2}%";
        if (luckText != null) luckText.text = $"運の良さ：{gs.Luck}";

        // =====================================================
        // Status2 — 中列: 属性耐性（装備＋パッシブ合算）
        // =====================================================
        if (resStrikeText != null) resStrikeText.text = $"殴：{PassiveCalculator.CalcTotalAttributeResistance(WeaponAttribute.Strike)}";
        if (resSlashText != null) resSlashText.text = $"斬：{PassiveCalculator.CalcTotalAttributeResistance(WeaponAttribute.Slash)}";
        if (resPierceText != null) resPierceText.text = $"突：{PassiveCalculator.CalcTotalAttributeResistance(WeaponAttribute.Pierce)}";
        if (resFireText != null) resFireText.text = $"火：{PassiveCalculator.CalcTotalAttributeResistance(WeaponAttribute.Fire)}";
        if (resIceText != null) resIceText.text = $"氷：{PassiveCalculator.CalcTotalAttributeResistance(WeaponAttribute.Ice)}";
        if (resThunderText != null) resThunderText.text = $"雷：{PassiveCalculator.CalcTotalAttributeResistance(WeaponAttribute.Thunder)}";
        if (resHolyText != null) resHolyText.text = $"聖：{PassiveCalculator.CalcTotalAttributeResistance(WeaponAttribute.Holy)}";
        if (resDarkText != null) resDarkText.text = $"闇：{PassiveCalculator.CalcTotalAttributeResistance(WeaponAttribute.Dark)}";

        // =====================================================
        // Status2 — 右列: 状態異常耐性（装備＋パッシブ合算）
        // =====================================================
        if (resPoisonText != null) resPoisonText.text = $"毒：{CalcTotalStatusEffectResistance(StatusEffect.Poison)}";
        if (resParalyzeText != null) resParalyzeText.text = $"麻痺：{CalcTotalStatusEffectResistance(StatusEffect.Paralyze)}";
        if (resBlindText != null) resBlindText.text = $"暗闇：{CalcTotalStatusEffectResistance(StatusEffect.Blind)}";
        if (resSilenceText != null) resSilenceText.text = $"沈黙：{CalcTotalStatusEffectResistance(StatusEffect.Silence)}";
        if (resDebuffText != null) resDebuffText.text = $"デバフ：{CalcTotalStatusEffectResistance(StatusEffect.Debuff)}";
    }

    /// <summary>
    /// 指定状態異常の耐性合計値を返す（装備＋パッシブ合算）。
    /// PassiveCalculator.CalcTotalAttributeResistance と同じパターン。
    /// </summary>
    private int CalcTotalStatusEffectResistance(StatusEffect effect)
    {
        int equipRes = EquipmentCalculator.GetStatusEffectResistance(effect);
        int passiveRes = PassiveCalculator.CalcStatusEffectResistance(effect);
        return equipRes + passiveRes;
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

    /// <summary>
    /// 指定ステータスに amount ポイントを一括振り分けする。
    /// ポイントが amount 未満の場合は残り全ポイントを振る。
    /// </summary>
    private void OnBulkAllocate(StatType stat, int amount)
    {
        if (GameState.I == null) return;
        if (GameState.I.statusPoint <= 0) return;

        // 振る量 = 要求量と残ポイントの小さい方
        int toAllocate = Mathf.Min(amount, GameState.I.statusPoint);

        for (int i = 0; i < toAllocate; i++)
        {
            if (!GameState.I.AllocatePoint(stat))
                break;
        }

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
        bool success = GameState.I.ResetStatAllocation();
        if (!success)
        {
            // 容量超過で失敗 → ポップアップを閉じて警告表示
            if (resetConfirmPopup != null)
                resetConfirmPopup.SetActive(false);
            Debug.Log("[StatusView] リセット不可: アイテムを先に整理してください");
            // pointText を流用して警告を表示（1秒後に戻す）
            if (pointText != null)
            {
                string original = pointText.text;
                pointText.text = "アイテムを整理してからリセットしてください";
                StartCoroutine(RestoreTextAfterDelay(pointText, original, 2f));
            }
            return;
        }
        RefreshAll();
    }

    private System.Collections.IEnumerator RestoreTextAfterDelay(
        TMPro.TMP_Text text, string original, float delay)
    {
        yield return new UnityEngine.WaitForSeconds(delay);
        if (text != null) text.text = original;
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