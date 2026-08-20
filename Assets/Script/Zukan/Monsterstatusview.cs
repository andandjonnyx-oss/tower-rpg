using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Mstatus シーン（モンスター詳細画面）のコントローラー。
/// ZukanContext.SelectedMonster のデータを表示する。
/// StatusView と同様のパネル切替構成（3パネル）。
///
/// パネル1: 基本情報（画像・名前・ステータス・ドロップ・説明）
/// パネル2: 耐性情報（属性耐性・状態異常耐性）
/// パネル3: 行動パターン（actions テーブル）
///
/// ← → ボタン: パネル切替
/// ↑ ↓ ボタン: 遭遇済みモンスター間の切替（ループ）
/// </summary>
public class MonsterStatusView : MonoBehaviour
{
    // =========================================================
    // Inspector 参照
    // =========================================================

    [Header("Panels")]
    [SerializeField] private GameObject panel1;  // 基本情報
    [SerializeField] private GameObject panel2;  // 耐性情報
    [SerializeField] private GameObject panel3;  // 行動パターン

    [Header("Panel1 - Basic Info")]
    [SerializeField] private Image monsterImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text floorRangeText;
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private TMP_Text attackText;
    [SerializeField] private TMP_Text defenseText;
    [SerializeField] private TMP_Text magicDefenseText;
    [SerializeField] private TMP_Text evasionText;
    [SerializeField] private TMP_Text luckText;
    [SerializeField] private TMP_Text expText;
    [SerializeField] private TMP_Text dropText;
    [SerializeField] private TMP_Text helpText;

    [Header("Panel2 - Attribute Resistances")]
    [SerializeField] private TMP_Text resStrikeText;
    [SerializeField] private TMP_Text resSlashText;
    [SerializeField] private TMP_Text resPierceText;
    [SerializeField] private TMP_Text resFireText;
    [SerializeField] private TMP_Text resIceText;
    [SerializeField] private TMP_Text resThunderText;
    [SerializeField] private TMP_Text resHolyText;
    [SerializeField] private TMP_Text resDarkText;

    [Header("Panel2 - Status Effect Resistances")]
    [SerializeField] private TMP_Text resPoisonText;
    [SerializeField] private TMP_Text resStunText;
    [SerializeField] private TMP_Text resParalyzeText;
    [SerializeField] private TMP_Text resBlindText;
    [SerializeField] private TMP_Text resRageText;
    [SerializeField] private TMP_Text resSilenceText;
    [SerializeField] private TMP_Text resDebuffText;
    [SerializeField] private TMP_Text resPetrifyText;  // 石化耐性
    [SerializeField] private TMP_Text resCharmText;     // 魅了耐性
    [SerializeField] private TMP_Text resCurseText;     // 呪い耐性
    [SerializeField] private TMP_Text resGlassText;     // ガラス耐性
    [SerializeField] private TMP_Text immuneText;  // 全耐性フラグ表示

    [Header("Panel3 - Action Pattern")]
    [Tooltip("行動パターンを1行ずつ表示するテキスト（複数行）")]
    [SerializeField] private TMP_Text actionPatternText;

    [Header("Navigation Buttons")]
    [SerializeField] private Button prevPanelButton;    // ← パネル切替
    [SerializeField] private Button nextPanelButton;    // → パネル切替
    [SerializeField] private Button prevMonsterButton;  // ↑ 前のモンスター
    [SerializeField] private Button nextMonsterButton;  // ↓ 次のモンスター
    [SerializeField] private Button backButton;         // 戻る（ZukanM へ）

    [Header("Display")]
    [SerializeField] private TMP_Text pageText;         // パネルページ表示（例: 1/3）
    [SerializeField] private TMP_Text monsterIndexText; // モンスター番号表示（例: 3/15）

    [Header("Scene Names")]
    [SerializeField] private string zukanMSceneName = "ZukanM";

    // =========================================================
    // 内部状態
    // =========================================================
    private Monster monster;
    private int currentPanel = 0;     // 0=基本, 1=耐性, 2=行動
    private const int PanelCount = 3;

    // =========================================================
    // 初期化
    // =========================================================

    private void Start()
    {
        monster = ZukanContext.SelectedMonster;
        if (monster == null)
        {
            Debug.LogError("[MonsterStatusView] ZukanContext.SelectedMonster is null");
            return;
        }

        // ボタン登録
        if (prevPanelButton != null) prevPanelButton.onClick.AddListener(OnPrevPanelClicked);
        if (nextPanelButton != null) nextPanelButton.onClick.AddListener(OnNextPanelClicked);
        if (prevMonsterButton != null) prevMonsterButton.onClick.AddListener(OnPrevMonsterClicked);
        if (nextMonsterButton != null) nextMonsterButton.onClick.AddListener(OnNextMonsterClicked);
        if (backButton != null) backButton.onClick.AddListener(OnBackClicked);

        // 初期表示: パネル1
        currentPanel = 0;
        RefreshAll();
        ApplyPanelVisibility();
        UpdateMonsterNavButtons();
    }

    // =========================================================
    // 表示更新
    // =========================================================

    private void RefreshAll()
    {
        if (monster == null) return;

        RefreshPanel1();
        RefreshPanel2();
        RefreshPanel3();
    }

    /// <summary>パネル1: 基本情報</summary>
    private void RefreshPanel1()
    {
        if (monsterImage != null)
        {
            monsterImage.sprite = monster.Image;
            monsterImage.preserveAspect = true;
        }
        if (nameText != null) nameText.text = monster.Mname;
        if (floorRangeText != null)
        {
            if (monster.IsBoss)
                floorRangeText.text = "出現: ボス";
            else
                floorRangeText.text = $"出現: {monster.Minfloor}F-{monster.Maxfloor}F";
        }
        if (hpText != null) hpText.text = $"HP: {monster.MaxHp}";
        if (attackText != null) attackText.text = $"ATK: {monster.Attack}";
        if (defenseText != null) defenseText.text = $"DEF: {monster.Defense}";
        if (magicDefenseText != null) magicDefenseText.text = $"MDEF: {monster.MagicDefense}";
        if (evasionText != null) evasionText.text = $"EVA: {monster.Evasion}";
        if (luckText != null) luckText.text = $"LUC: {monster.Luck}";
        if (expText != null) expText.text = $"EXP: {monster.Exp}";

        // ドロップアイテム（1行目: ドロップ: / 2行目: アイテム名 (%)）
        if (dropText != null)
        {
            if (monster.dropItem != null && monster.dropRate > 0f)
            {
                int percent = Mathf.RoundToInt(monster.dropRate * 100f);
                dropText.text = $"ドロップ:\n{monster.dropItem.itemName} ({percent}%)";
            }
            else
            {
                dropText.text = "ドロップ:\nなし";
            }
        }

        // 説明テキスト
        if (helpText != null)
        {
            helpText.text = !string.IsNullOrEmpty(monster.Help) ? monster.Help : "";
        }
    }

    /// <summary>パネル2: 耐性情報</summary>
    private void RefreshPanel2()
    {
        // 属性耐性
        if (resStrikeText != null) resStrikeText.text = $"殴耐性: {monster.GetAttributeResistance(WeaponAttribute.Strike)}";
        if (resSlashText != null) resSlashText.text = $"斬耐性: {monster.GetAttributeResistance(WeaponAttribute.Slash)}";
        if (resPierceText != null) resPierceText.text = $"突耐性: {monster.GetAttributeResistance(WeaponAttribute.Pierce)}";
        if (resFireText != null) resFireText.text = $"火耐性: {monster.GetAttributeResistance(WeaponAttribute.Fire)}";
        if (resIceText != null) resIceText.text = $"氷耐性: {monster.GetAttributeResistance(WeaponAttribute.Ice)}";
        if (resThunderText != null) resThunderText.text = $"雷耐性: {monster.GetAttributeResistance(WeaponAttribute.Thunder)}";
        if (resHolyText != null) resHolyText.text = $"聖耐性: {monster.GetAttributeResistance(WeaponAttribute.Holy)}";
        if (resDarkText != null) resDarkText.text = $"闇耐性: {monster.GetAttributeResistance(WeaponAttribute.Dark)}";

        // 状態異常耐性
        if (resPoisonText != null) resPoisonText.text = $"毒耐性: {monster.GetStatusEffectResistance(StatusEffect.Poison)}";
        if (resStunText != null) resStunText.text = $"気絶耐性: {monster.GetStatusEffectResistance(StatusEffect.Stun)}";
        if (resParalyzeText != null) resParalyzeText.text = $"麻痺耐性: {monster.GetStatusEffectResistance(StatusEffect.Paralyze)}";
        if (resBlindText != null) resBlindText.text = $"暗闇耐性: {monster.GetStatusEffectResistance(StatusEffect.Blind)}";
        if (resRageText != null) resRageText.text = $"怒り耐性: {monster.GetStatusEffectResistance(StatusEffect.Rage)}";
        if (resSilenceText != null) resSilenceText.text = $"沈黙耐性: {monster.GetStatusEffectResistance(StatusEffect.Silence)}";
        if (resDebuffText != null) resDebuffText.text = $"デバフ耐性: {monster.GetStatusEffectResistance(StatusEffect.Debuff)}";
        if (resPetrifyText != null) resPetrifyText.text = $"石化耐性: {monster.GetStatusEffectResistance(StatusEffect.Petrify)}";
        if (resCharmText != null) resCharmText.text = $"魅了耐性: {monster.GetStatusEffectResistance(StatusEffect.Charm)}";
        if (resCurseText != null) resCurseText.text = $"呪い耐性: {monster.GetStatusEffectResistance(StatusEffect.Curse)}";
        if (resGlassText != null) resGlassText.text = $"ガラス耐性: {monster.GetStatusEffectResistance(StatusEffect.Glass)}";

        // 全耐性フラグ
        if (immuneText != null)
        {
            immuneText.text = monster.immuneToAllAilments ? "★ 状態異常完全耐性" : "";
        }
    }

    /// <summary>パネル3: 行動パターン</summary>
    private void RefreshPanel3()
    {
        if (actionPatternText == null) return;

        if (monster.actions == null || monster.actions.Length == 0)
        {
            actionPatternText.text = "通常攻撃のみ";
            return;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"行動範囲: {monster.baseActionRange}");
        sb.AppendLine("---");

        for (int i = 0; i < monster.actions.Length; i++)
        {
            var entry = monster.actions[i];
            if (entry == null) continue;

            string skillName = entry.skill != null ? entry.skill.skillName : "通常攻撃";
            string actionTypeName = "";
            if (entry.skill != null)
            {
                switch (entry.skill.actionType)
                {
                    case MonsterActionType.Idle: actionTypeName = "[待機]"; break;
                    case MonsterActionType.SkillAttack: actionTypeName = "[スキル]"; break;
                    case MonsterActionType.Preemptive: actionTypeName = "[先制]"; break;
                    case MonsterActionType.FoodRaid: actionTypeName = "[食い荒らし]"; break;
                    default: actionTypeName = ""; break;
                }
            }

            sb.AppendLine($"{entry.threshold}: {skillName} {actionTypeName}");
        }

        actionPatternText.text = sb.ToString();
    }

    // =========================================================
    // パネル切替（← →）
    // =========================================================

    private void ApplyPanelVisibility()
    {
        if (panel1 != null) panel1.SetActive(currentPanel == 0);
        if (panel2 != null) panel2.SetActive(currentPanel == 1);
        if (panel3 != null) panel3.SetActive(currentPanel == 2);

        // ページ表示
        if (pageText != null) pageText.text = $"{currentPanel + 1}/{PanelCount}";

        // ← → ボタンの有効/無効
        if (prevPanelButton != null) prevPanelButton.interactable = (currentPanel > 0);
        if (nextPanelButton != null) nextPanelButton.interactable = (currentPanel < PanelCount - 1);
    }

    private void OnPrevPanelClicked()
    {
        if (currentPanel > 0)
        {
            currentPanel--;
            ApplyPanelVisibility();
        }
    }

    private void OnNextPanelClicked()
    {
        if (currentPanel < PanelCount - 1)
        {
            currentPanel++;
            ApplyPanelVisibility();
        }
    }

    // =========================================================
    // モンスター切替（↑ ↓）
    // =========================================================
    //
    // ZukanContext.EncounteredList 内を前後に移動する。
    // 端に達したらループする（末尾→先頭、先頭→末尾）。
    // シーン遷移なしで monster を差し替えて RefreshAll() するだけなので軽い。
    // モンスター切替時はパネルを1/3（基本情報）にリセットする。
    // =========================================================

    /// <summary>↑ 前のモンスターへ切替</summary>
    private void OnPrevMonsterClicked()
    {
        if (ZukanContext.EncounteredList == null || ZukanContext.EncounteredList.Count <= 1) return;

        int idx = ZukanContext.CurrentIndex - 1;
        if (idx < 0) idx = ZukanContext.EncounteredList.Count - 1; // ループ

        SwitchToMonster(idx);
    }

    /// <summary>↓ 次のモンスターへ切替</summary>
    private void OnNextMonsterClicked()
    {
        if (ZukanContext.EncounteredList == null || ZukanContext.EncounteredList.Count <= 1) return;

        int idx = ZukanContext.CurrentIndex + 1;
        if (idx >= ZukanContext.EncounteredList.Count) idx = 0; // ループ

        SwitchToMonster(idx);
    }

    /// <summary>
    /// 指定インデックスのモンスターに切り替える。
    /// シーン遷移なし。表示内容を即座に更新する。
    /// モンスター切替時はパネルを1/3（基本情報）にリセットする。
    /// </summary>
    private void SwitchToMonster(int newIndex)
    {
        ZukanContext.CurrentIndex = newIndex;
        monster = ZukanContext.EncounteredList[newIndex];
        ZukanContext.SelectedMonster = monster;

        currentPanel = 0;   // モンスター切替時は必ず基本情報(1/3)に戻す

        RefreshAll();
        ApplyPanelVisibility();
        UpdateMonsterNavButtons();
    }

    /// <summary>
    /// ↑↓ ボタンの有効/無効とモンスター番号表示を更新する。
    /// 閲覧可能モンスターが1体以下なら無効にする。
    /// </summary>
    private void UpdateMonsterNavButtons()
    {
        bool canNavigate = ZukanContext.EncounteredList != null
                           && ZukanContext.EncounteredList.Count > 1;

        if (prevMonsterButton != null) prevMonsterButton.interactable = canNavigate;
        if (nextMonsterButton != null) nextMonsterButton.interactable = canNavigate;

        // モンスター番号表示（例: 3/15）
        if (monsterIndexText != null)
        {
            if (ZukanContext.EncounteredList != null && ZukanContext.EncounteredList.Count > 0)
            {
                monsterIndexText.text = $"{ZukanContext.CurrentIndex + 1}/{ZukanContext.EncounteredList.Count}";
            }
            else
            {
                monsterIndexText.text = "";
            }
        }
    }

    // =========================================================
    // 戻るボタン
    // =========================================================

    private void OnBackClicked()
    {
        ZukanContext.ReturningFromDetail = true;
        ZukanContext.ReturnTargetMonster = monster;

        SceneManager.LoadScene(zukanMSceneName);
    }
}