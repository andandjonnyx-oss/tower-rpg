using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 戦闘シーンのメインコントローラー。
/// ターン制（味方→敵→味方…）で戦闘を進行する。
/// </summary>
public class BattleSceneController : MonoBehaviour
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

    [Header("Scene Names")]
    [SerializeField] private string towerSceneName = "Tower";
    [SerializeField] private string mainSceneName = "Main";
    [SerializeField] private string itemboxSceneName = "Itembox";

    // 戦闘中の敵HP（シーン再読込でも維持するため static）
    private static int enemyCurrentHp;
    private static bool battleInitialized = false;
    private static List<string> persistentLogLines = new List<string>();

    private Monster enemyMonster;

    // 戦闘ログ（最大3行）
    private List<string> logLines = new List<string>();
    private const int MaxLogLines = 3;

    private bool battleEnded = false;

    // 装備中武器の InventoryItem キャッシュ（スキルクールダウン管理用）
    private InventoryItem equippedWeaponItem;

    private void Start()
    {
        enemyMonster = BattleContext.EnemyMonster;
        if (enemyMonster == null)
        {
            Debug.LogError("[Battle] EnemyMonster is null");
            return;
        }

        // 敵の表示
        if (enemyImage != null)
        {
            enemyImage.sprite = enemyMonster.Image;
            enemyImage.preserveAspect = true;
        }

        // 装備中武器の InventoryItem を取得してキャッシュ
        equippedWeaponItem = GetEquippedWeaponItem();

        // 攻撃ボタン設定
        if (attackButton != null)
            attackButton.onClick.AddListener(OnAttackClicked);

        // スキルボタン設定
        if (skillButton != null)
            skillButton.onClick.AddListener(OnSkillClicked);

        // アイテムボタン設定
        if (itemButton != null)
            itemButton.onClick.AddListener(OnItemClicked);

        // 初回 or Itemboxから戻り
        if (!battleInitialized)
        {
            // 初回: 敵HPを初期化
            enemyCurrentHp = enemyMonster.MaxHp;
            battleInitialized = true;
            persistentLogLines.Clear();
            AddLog($"{enemyMonster.Mname} が現れた！");
        }
        else
        {
            // Itembox から戻ってきた場合: ログを復元
            logLines = new List<string>(persistentLogLines);
            UpdateLogDisplay();

            // ターン消費チェック
            if (GameState.I != null && GameState.I.battleTurnConsumed)
            {
                GameState.I.battleTurnConsumed = false;

                // ログ表示
                if (!string.IsNullOrEmpty(GameState.I.battleItemActionLog))
                {
                    AddLog(GameState.I.battleItemActionLog);
                    GameState.I.battleItemActionLog = "";
                }

                // 装備が変わった可能性があるのでキャッシュ更新
                equippedWeaponItem = GetEquippedWeaponItem();

                // クールダウンを進める（1ターン消費）
                OnPlayerTurnStart();

                // ボタン無効化して敵ターンへ
                SetButtonsInteractable(false);
                Invoke(nameof(EnemyTurn), 0.5f);

                // スキルボタン更新
                RefreshSkillButton();
                return;
            }
        }

        // スキルボタンの表示を更新
        RefreshSkillButton();
    }

    // =========================================================
    // プレイヤーターン
    // =========================================================

    /// <summary>
    /// プレイヤーが行動する直前に呼ぶ共通処理。
    /// クールダウンを 1 進める（初回ターンはまだ何もセットされていないので空振り）。
    /// </summary>
    private void OnPlayerTurnStart()
    {
        if (equippedWeaponItem != null)
            equippedWeaponItem.TickCooldowns();
    }

    /// <summary>
    /// 攻撃ボタンが押された時の処理（プレイヤーターン・通常攻撃）。
    /// </summary>
    private void OnAttackClicked()
    {
        if (battleEnded) return;

        // ボタン連打防止
        SetButtonsInteractable(false);

        // ターン開始: クールダウンを進める
        OnPlayerTurnStart();

        // 装備中の武器を取得
        string weaponName;
        WeaponAttribute weaponAttribute;
        int weaponPower;
        GetEquippedWeaponInfo(out weaponName, out weaponAttribute, out weaponPower);

        // ダメージ計算: STR + 武器攻撃力
        int str = (GameState.I != null) ? GameState.I.baseSTR : 1;
        int damage = str + weaponPower;
        if (damage < 1) damage = 1;

        // ダメージ適用
        enemyCurrentHp -= damage;
        if (enemyCurrentHp < 0) enemyCurrentHp = 0;

        AddLog($"You は {weaponName} で攻撃！（{weaponAttribute.ToJapanese()}属性） {damage}ダメージ！");

        // 敵撃破判定
        if (enemyCurrentHp <= 0)
        {
            OnVictory();
            return;
        }

        // 敵ターンへ（少し待ってから）
        Invoke(nameof(EnemyTurn), 0.5f);
    }

    /// <summary>
    /// スキルボタンが押された時の処理（プレイヤーターン・スキル攻撃）。
    /// 装備中武器の最初のスキルを使用する。
    /// </summary>
    private void OnSkillClicked()
    {
        if (battleEnded) return;

        // 装備武器とスキルの存在チェック
        SkillData skill = GetFirstSkill();
        if (skill == null)
        {
            AddLog("使えるスキルがない！");
            return;
        }

        // ターン開始: クールダウンを進める（Tick後に判定する）
        OnPlayerTurnStart();

        // クールダウンチェック（Tick 後の値で判定）
        if (equippedWeaponItem == null || !equippedWeaponItem.CanUseSkill(skill.skillId))
        {
            AddLog($"{skill.skillName} はまだ使えない！");
            SetButtonsInteractable(true);
            RefreshSkillButton();
            return;
        }

        // ボタン連打防止
        SetButtonsInteractable(false);

        // 武器の基本情報を取得
        string weaponName;
        WeaponAttribute weaponAttribute;
        int weaponPower;
        GetEquippedWeaponInfo(out weaponName, out weaponAttribute, out weaponPower);

        // スキルのダメージ計算: (STR + 武器攻撃力) × スキル倍率
        int str = (GameState.I != null) ? GameState.I.baseSTR : 1;
        int baseDamage = str + weaponPower;
        int damage = Mathf.RoundToInt(baseDamage * skill.damageMultiplier);
        if (damage < 1) damage = 1;

        // スキルの属性で上書き（スキル固有の属性を使用）
        WeaponAttribute skillAttr = skill.skillAttribute;

        // ダメージ適用
        enemyCurrentHp -= damage;
        if (enemyCurrentHp < 0) enemyCurrentHp = 0;

        // クールダウンをセット
        equippedWeaponItem.UseSkill(skill);

        AddLog($"You は {skill.skillName}！（{skillAttr.ToJapanese()}属性） {damage}ダメージ！");

        // 敵撃破判定
        if (enemyCurrentHp <= 0)
        {
            OnVictory();
            return;
        }

        // 敵ターンへ（少し待ってから）
        Invoke(nameof(EnemyTurn), 0.5f);
    }

    // =========================================================
    // アイテム使用（Itembox シーンへ遷移）
    // =========================================================

    /// <summary>
    /// アイテムボタンが押された時の処理。
    /// Itembox シーンへ遷移する（ターン消費なし）。
    /// </summary>
    private void OnItemClicked()
    {
        if (battleEnded) return;

        if (GameState.I != null)
        {
            GameState.I.isInBattle = true;
            GameState.I.battleTurnConsumed = false;
            GameState.I.battleItemActionLog = "";
            GameState.I.previousSceneName = SceneManager.GetActiveScene().name;
        }

        // ログを保存（戻ってきた時に復元するため）
        persistentLogLines = new List<string>(logLines);

        SceneManager.LoadScene(itemboxSceneName);
    }

    // =========================================================
    // 敵ターン
    // =========================================================

    /// <summary>
    /// 敵の攻撃処理。
    /// </summary>
    private void EnemyTurn()
    {
        if (battleEnded) return;

        // 敵の攻撃力（Monster.Attack をそのまま使用）
        int enemyDamage = enemyMonster.Attack;
        if (enemyDamage < 1) enemyDamage = 1;

        // プレイヤーにダメージ
        if (GameState.I != null)
        {
            GameState.I.currentHp -= enemyDamage;
            if (GameState.I.currentHp < 0) GameState.I.currentHp = 0;
        }

        AddLog($"{enemyMonster.Mname} の攻撃！ {enemyDamage}ダメージ！");

        // プレイヤー敗北判定
        if (GameState.I != null && GameState.I.currentHp <= 0)
        {
            OnDefeat();
            return;
        }

        // プレイヤーターンに戻す（Tick はプレイヤー行動時に行う）
        SetButtonsInteractable(true);
        RefreshSkillButton();
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

        Invoke(nameof(ReturnToTower), 1.5f);
    }

    private void OnDefeat()
    {
        battleEnded = true;
        AddLog("You は倒れた…");
        SetButtonsInteractable(false);

        ResetAllWeaponCooldowns();
        ResetBattleStatics();

        Invoke(nameof(ReturnToMainWithFullRecover), 1.5f);
    }

    private void ReturnToTower()
    {
        SceneManager.LoadScene(towerSceneName);
    }

    private void ReturnToMainWithFullRecover()
    {
        FullRecover();
        SceneManager.LoadScene(mainSceneName);
    }

    private void FullRecover()
    {
        if (GameState.I == null) return;
        GameState.I.currentHp = GameState.I.maxHp;
        GameState.I.currentMp = GameState.I.maxMp;
        Debug.Log($"[Battle] 全回復: HP={GameState.I.currentHp}/{GameState.I.maxHp}");
    }

    /// <summary>
    /// 戦闘終了時に static 変数をクリアする。
    /// 次の戦闘が前回の状態を引き継がないようにする。
    /// </summary>
    private void ResetBattleStatics()
    {
        battleInitialized = false;
        persistentLogLines.Clear();
    }

    // =========================================================
    // スキル関連ユーティリティ
    // =========================================================

    private InventoryItem GetEquippedWeaponItem()
    {
        if (GameState.I == null || string.IsNullOrEmpty(GameState.I.equippedWeaponUid))
            return null;

        if (ItemBoxManager.Instance == null)
            return null;

        var items = ItemBoxManager.Instance.GetItems();
        if (items == null) return null;

        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] != null && items[i].uid == GameState.I.equippedWeaponUid)
            {
                if (items[i].data != null && items[i].data.category == ItemCategory.Weapon)
                    return items[i];
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

        if (skill == null)
        {
            skillButton.gameObject.SetActive(false);
            return;
        }

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
            if (equippedWeaponItem != null &&
                equippedWeaponItem.skillCooldowns.ContainsKey(skill.skillId))
            {
                remaining = equippedWeaponItem.skillCooldowns[skill.skillId];
            }
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
            if (items[i] != null && items[i].data != null &&
                items[i].data.category == ItemCategory.Weapon)
            {
                items[i].ResetAllCooldowns();
            }
        }
    }

    // =========================================================
    // ユーティリティ
    // =========================================================

    private void GetEquippedWeaponInfo(out string weaponName, out WeaponAttribute attribute, out int power)
    {
        weaponName = "素手";
        attribute = WeaponAttribute.Strike;
        power = 0;

        if (equippedWeaponItem != null && equippedWeaponItem.data != null)
        {
            weaponName = equippedWeaponItem.data.itemName;
            attribute = equippedWeaponItem.data.weaponAttribute;
            power = equippedWeaponItem.data.attackPower;
            return;
        }

        if (GameState.I != null)
            GameState.I.equippedWeaponUid = "";
    }

    private void SetButtonsInteractable(bool interactable)
    {
        if (attackButton != null)
            attackButton.interactable = interactable;

        if (itemButton != null)
            itemButton.interactable = interactable;

        if (!interactable && skillButton != null)
            skillButton.interactable = false;
    }

    private void AddLog(string message)
    {
        logLines.Add(message);

        while (logLines.Count > MaxLogLines)
        {
            logLines.RemoveAt(0);
        }

        // static にも保存（シーン遷移で失われないように）
        persistentLogLines = new List<string>(logLines);

        UpdateLogDisplay();
    }

    private void UpdateLogDisplay()
    {
        if (battleLogText != null)
        {
            battleLogText.text = string.Join("\n", logLines);
        }
    }
}