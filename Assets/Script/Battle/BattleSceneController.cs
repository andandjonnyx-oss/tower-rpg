using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 戦闘シーンのメインコントローラー。
/// ターン制（味方→敵→味方…）で戦闘を進行する。
///
/// partial class により以下のファイルに分割されている:
///   BattleSceneController.cs              … フィールド宣言、Start、ログ管理、UI制御、勝敗処理
///   BattleSceneController_PlayerAction.cs … プレイヤー行動（攻撃/スキル/魔法/アイテム）
///   BattleSceneController_EnemyAction.cs  … 敵行動（行動選択/LUC判定/各種攻撃/ターン終了処理）
///   BattleSceneController_CombatUtils.cs  … 命中判定/クリティカル/防御ダイス/ダメージ適用
/// </summary>
public partial class BattleSceneController : MonoBehaviour
{
    [Header("UI - Enemy")]
    [SerializeField] private Image enemyImage;

    [Header("UI - Battle Log")]
    [Tooltip("戦闘ログ表示用 TMP_Text（3行分）")]
    [SerializeField] private TMP_Text battleLogText;

    [Header("UI - Buttons")]
    [SerializeField] private Button attackButton;
    [SerializeField] private Button skillButton;
    [SerializeField] private Button itemButton;
    [SerializeField] private Button magicButton;

    [Header("UI - Magic Dropdown")]
    [Tooltip("所持中の魔法スキルを選択するドロップダウン")]
    [SerializeField] private TMP_Dropdown magicDropdown;

    [Header("Scene Names")]
    [SerializeField] private string towerSceneName = "Tower";
    [SerializeField] private string mainSceneName = "Main";
    [SerializeField] private string itemboxSceneName = "Itembox";

    // 戦闘中の敵HP（シーン再読込でも維持するため static）
    private static int enemyCurrentHp;
    private static bool battleInitialized = false;
    private static List<string> persistentLogLines = new List<string>();

    private Monster enemyMonster;

    // =========================================================
    // 敵の状態異常（追加）
    // =========================================================

    /// <summary>戦闘中の敵の毒状態。戦闘終了でリセット。</summary>
    private static bool enemyIsPoisoned = false;

    // 戦闘ログ（最大3行）
    private List<string> logLines = new List<string>();
    private const int MaxLogLines = 3;

    private bool battleEnded = false;

    // 装備中武器の InventoryItem キャッシュ（スキルクールダウン管理用）
    private InventoryItem equippedWeaponItem;

    // 魔法ドロップダウンに表示中のスキル一覧キャッシュ
    private List<SkillData> magicSkillList = new List<SkillData>();

    private void Start()
    {
        enemyMonster = BattleContext.EnemyMonster;
        if (enemyMonster == null)
        {
            Debug.LogError("[Battle] EnemyMonster is null");
            return;
        }

        if (enemyImage != null)
        {
            enemyImage.sprite = enemyMonster.Image;
            enemyImage.preserveAspect = true;
        }

        equippedWeaponItem = GetEquippedWeaponItem();

        if (attackButton != null) attackButton.onClick.AddListener(OnAttackClicked);
        if (skillButton != null) skillButton.onClick.AddListener(OnSkillClicked);
        if (itemButton != null) itemButton.onClick.AddListener(OnItemClicked);
        if (magicButton != null) magicButton.onClick.AddListener(OnMagicClicked);

        if (!battleInitialized)
        {
            enemyCurrentHp = enemyMonster.MaxHp;
            battleInitialized = true;
            persistentLogLines.Clear();
            AddLog($"{enemyMonster.Mname} が現れた！");
        }
        else
        {
            logLines = new List<string>(persistentLogLines);
            UpdateLogDisplay();

            if (GameState.I != null && GameState.I.battleTurnConsumed)
            {
                GameState.I.battleTurnConsumed = false;
                if (!string.IsNullOrEmpty(GameState.I.battleItemActionLog))
                {
                    AddLog(GameState.I.battleItemActionLog);
                    GameState.I.battleItemActionLog = "";
                }
                equippedWeaponItem = GetEquippedWeaponItem();
                TickAllWeaponCooldowns();
                SetButtonsInteractable(false);
                Invoke(nameof(EnemyTurn), 0.5f);
                RefreshSkillButton();
                RefreshMagicDropdown();
                return;
            }
        }

        RefreshSkillButton();
        RefreshMagicDropdown();
    }

    // =========================================================
    // 勝利 / 敗北
    // =========================================================

    private void OnVictory()
    {
        battleEnded = true;
        AddLog($"{enemyMonster.Mname} を倒した！");
        SetButtonsInteractable(false);
        ResetAllWeaponCooldowns();
        ResetBattleStatics();
        enemyIsPoisoned = false;
        Invoke(nameof(ReturnToTower), 1.5f);
    }

    private void OnDefeat()
    {
        battleEnded = true;
        AddLog("You は倒れた…");
        SetButtonsInteractable(false);
        ResetAllWeaponCooldowns();
        ResetBattleStatics();
        enemyIsPoisoned = false;
        Invoke(nameof(ReturnToMainWithFullRecover), 1.5f);
    }

    private void ReturnToTower() { SceneManager.LoadScene(towerSceneName); }

    private void ReturnToMainWithFullRecover()
    {
        FullRecover();
        SceneManager.LoadScene(mainSceneName);
    }

    /// <summary>
    /// HP/MP全回復＋全状態異常クリア。
    /// ★ブラッシュアップ: 街に戻る = 全回復（状態異常含む）で統一。
    /// 敗北時・帰還時・ロード復帰時にこのメソッドを呼ぶ。
    /// </summary>
    private void FullRecover()
    {
        if (GameState.I == null) return;
        GameState.I.currentHp = GameState.I.maxHp;
        GameState.I.currentMp = GameState.I.maxMp;
        GameState.I.ClearAllStatusEffects(); // ★追加: 状態異常も全クリア
        Debug.Log($"[Battle] 全回復: HP={GameState.I.currentHp}/{GameState.I.maxHp} 状態異常クリア");
    }

    private void ResetBattleStatics()
    {
        battleInitialized = false;
        persistentLogLines.Clear();
        enemyIsPoisoned = false;
    }

    // =========================================================
    // 武器スキル関連ユーティリティ
    // =========================================================

    private InventoryItem GetEquippedWeaponItem()
    {
        if (GameState.I == null || string.IsNullOrEmpty(GameState.I.equippedWeaponUid)) return null;
        if (ItemBoxManager.Instance == null) return null;
        var items = ItemBoxManager.Instance.GetItems();
        if (items == null) return null;
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] != null && items[i].uid == GameState.I.equippedWeaponUid)
            {
                if (items[i].data != null && items[i].data.category == ItemCategory.Weapon) return items[i];
                return null;
            }
        }
        return null;
    }

    private SkillData GetFirstSkill()
    {
        if (equippedWeaponItem == null) return null;
        if (equippedWeaponItem.data == null) return null;
        if (equippedWeaponItem.data.skills == null) return null;
        if (equippedWeaponItem.data.skills.Length == 0) return null;
        return equippedWeaponItem.data.skills[0];
    }

    private void RefreshSkillButton()
    {
        if (skillButton == null) return;
        SkillData skill = GetFirstSkill();
        if (skill == null) { skillButton.gameObject.SetActive(false); return; }
        skillButton.gameObject.SetActive(true);
        var label = skillButton.GetComponentInChildren<TMP_Text>();
        if (label == null) return;
        if (equippedWeaponItem != null && equippedWeaponItem.CanUseSkill(skill.skillId))
        {
            label.text = skill.skillName;
            skillButton.interactable = !battleEnded;
        }
        else
        {
            int remaining = 0;
            if (equippedWeaponItem != null && equippedWeaponItem.skillCooldowns.ContainsKey(skill.skillId))
                remaining = equippedWeaponItem.skillCooldowns[skill.skillId];
            label.text = $"{skill.skillName} (CT:{remaining})";
            skillButton.interactable = false;
        }
    }

    private void ResetAllWeaponCooldowns()
    {
        if (ItemBoxManager.Instance == null) return;
        var items = ItemBoxManager.Instance.GetItems();
        if (items == null) return;
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] != null && items[i].data != null && items[i].data.category == ItemCategory.Weapon)
                items[i].ResetAllCooldowns();
        }
    }

    /// <summary>装備中武器の通常攻撃の基礎命中率を返す。未装備（素手）の場合は 95。</summary>
    private int GetEquippedWeaponBaseHitRate()
    {
        if (equippedWeaponItem != null && equippedWeaponItem.data != null)
            return equippedWeaponItem.data.baseHitRate;
        return 95;
    }

    // =========================================================
    // 魔法ドロップダウン関連ユーティリティ
    // =========================================================

    private void RefreshMagicDropdown()
    {
        magicSkillList = PassiveCalculator.CollectMagicSkills();
        if (magicSkillList.Count == 0)
        {
            if (magicDropdown != null) magicDropdown.gameObject.SetActive(false);
            if (magicButton != null) magicButton.gameObject.SetActive(false);
            return;
        }
        if (magicDropdown != null)
        {
            magicDropdown.gameObject.SetActive(true);
            magicDropdown.ClearOptions();
            var options = new List<string>();
            for (int i = 0; i < magicSkillList.Count; i++)
                options.Add($"{magicSkillList[i].skillName} (MP:{magicSkillList[i].mpCost})");
            magicDropdown.AddOptions(options);
            magicDropdown.value = 0;
            magicDropdown.RefreshShownValue();
        }
        if (magicButton != null)
        {
            magicButton.gameObject.SetActive(true);
            magicButton.interactable = !battleEnded;
        }
    }

    private SkillData GetSelectedMagicSkill()
    {
        if (magicDropdown == null) return null;
        if (magicSkillList == null || magicSkillList.Count == 0) return null;
        int index = magicDropdown.value;
        if (index < 0 || index >= magicSkillList.Count) return null;
        return magicSkillList[index];
    }

    // =========================================================
    // ユーティリティ
    // =========================================================

    private void GetEquippedWeaponInfo(out string weaponName, out WeaponAttribute attribute, out int power)
    {
        weaponName = "素手"; attribute = WeaponAttribute.Strike; power = 0;
        if (equippedWeaponItem != null && equippedWeaponItem.data != null)
        {
            weaponName = equippedWeaponItem.data.itemName;
            attribute = equippedWeaponItem.data.weaponAttribute;
            power = equippedWeaponItem.data.attackPower;
            return;
        }
        if (GameState.I != null) GameState.I.equippedWeaponUid = "";
    }

    private void SetButtonsInteractable(bool interactable)
    {
        if (attackButton != null) attackButton.interactable = interactable;
        if (itemButton != null) itemButton.interactable = interactable;
        if (!interactable && skillButton != null) skillButton.interactable = false;
        if (magicButton != null)
        {
            if (!interactable) magicButton.interactable = false;
            else if (magicSkillList != null && magicSkillList.Count > 0) magicButton.interactable = true;
        }
    }

    private void AddLog(string message)
    {
        logLines.Add(message);
        while (logLines.Count > MaxLogLines) logLines.RemoveAt(0);
        persistentLogLines = new List<string>(logLines);
        UpdateLogDisplay();
    }

    private void UpdateLogDisplay()
    {
        if (battleLogText != null) battleLogText.text = string.Join("\n", logLines);
    }
}