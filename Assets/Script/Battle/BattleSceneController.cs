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

        // 魔法ボタン設定
        if (magicButton != null)
            magicButton.onClick.AddListener(OnMagicClicked);

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

                // クールダウンを進める（1ターン消費 — 全武器対象）
                TickAllWeaponCooldowns();

                // ボタン無効化して敵ターンへ
                SetButtonsInteractable(false);
                Invoke(nameof(EnemyTurn), 0.5f);

                // スキルボタン・魔法ドロップダウン更新
                RefreshSkillButton();
                RefreshMagicDropdown();
                return;
            }
        }

        // スキルボタン・魔法ドロップダウンの表示を更新
        RefreshSkillButton();
        RefreshMagicDropdown();
    }

    // =========================================================
    // プレイヤーターン
    // =========================================================

    /// <summary>
    /// プレイヤーが行動する直前に呼ぶ共通処理。
    /// インベントリ内の全武器のクールダウンを 1 進める。
    /// （装備していない武器もクールダウンが進む）
    /// </summary>
    private void TickAllWeaponCooldowns()
    {
        if (ItemBoxManager.Instance == null) return;

        var items = ItemBoxManager.Instance.GetItems();
        if (items == null) return;

        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] != null && items[i].data != null &&
                items[i].data.category == ItemCategory.Weapon)
            {
                items[i].TickCooldowns();
            }
        }
    }

    /// <summary>
    /// 攻撃ボタンが押された時の処理（プレイヤーターン・通常攻撃）。
    /// </summary>
    private void OnAttackClicked()
    {
        if (battleEnded) return;

        // ボタン連打防止
        SetButtonsInteractable(false);

        // ターン開始: 全武器のクールダウンを進める
        TickAllWeaponCooldowns();

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
    /// スキルボタンが押された時の処理（プレイヤーターン・武器スキル攻撃）。
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

        // ターン開始: 全武器のクールダウンを進める
        TickAllWeaponCooldowns();

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
    // 魔法スキル発動（ドロップダウン選択 → 魔法ボタン押下）
    // =========================================================

    /// <summary>
    /// 魔法ボタンが押された時の処理（プレイヤーターン・魔法スキル発動）。
    /// ドロップダウンで選択中の魔法スキルを MP 消費して発動する。
    /// </summary>
    private void OnMagicClicked()
    {
        if (battleEnded) return;

        // ドロップダウンから選択中のスキルを取得
        SkillData magic = GetSelectedMagicSkill();
        if (magic == null)
        {
            AddLog("魔法が選択されていない！");
            return;
        }

        // MP チェック
        int currentMp = (GameState.I != null) ? GameState.I.currentMp : 0;
        if (currentMp < magic.mpCost)
        {
            AddLog($"MPが足りない！（必要:{magic.mpCost} 現在:{currentMp}）");
            return;
        }

        // ボタン連打防止
        SetButtonsInteractable(false);

        // ターン開始: 全武器のクールダウンを進める（魔法使用もターン消費）
        TickAllWeaponCooldowns();

        // MP 消費
        if (GameState.I != null)
            GameState.I.currentMp -= magic.mpCost;

        // ダメージ計算
        int damage;
        if (magic.fixedDamage > 0)
        {
            // 固定ダメージ（ファイアボール等）
            damage = magic.fixedDamage;
        }
        else if (magic.damageMultiplier > 0)
        {
            // 倍率ベース（将来的な魔法スキル用）
            int intStat = (GameState.I != null) ? GameState.I.baseINT : 1;
            damage = Mathf.RoundToInt(intStat * magic.damageMultiplier);
        }
        else
        {
            damage = 1;
        }
        if (damage < 1) damage = 1;

        // ダメージ適用
        enemyCurrentHp -= damage;
        if (enemyCurrentHp < 0) enemyCurrentHp = 0;

        AddLog($"You は {magic.skillName}！（{magic.skillAttribute.ToJapanese()}属性） {damage}ダメージ！ MP-{magic.mpCost}");

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
    /// 敵の行動処理。
    /// Monster に actions 配列が設定されている場合は行動テーブルに従い、
    /// 未設定の場合は従来通り Attack 依存の通常攻撃を行う。
    /// </summary>
    private void EnemyTurn()
    {
        if (battleEnded) return;

        // actions 配列が未設定 or 空 → 従来の単純攻撃
        if (enemyMonster.actions == null || enemyMonster.actions.Length == 0)
        {
            ExecuteLegacyAttack();
            return;
        }

        // 行動テーブルから行動を決定
        EnemyActionEntry selectedAction = SelectEnemyAction();

        // 選択された行動を実行
        ExecuteEnemyAction(selectedAction);
    }

    /// <summary>
    /// 従来の敵攻撃処理（actions 未設定時のフォールバック）。
    /// Monster.Attack をそのままダメージとして使用する。
    /// </summary>
    private void ExecuteLegacyAttack()
    {
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

        // プレイヤーターンに戻す
        SetButtonsInteractable(true);
        RefreshSkillButton();
        RefreshMagicDropdown();
    }

    /// <summary>
    /// LUC 差に応じた乱数の上限値（actionRange）を計算し、
    /// 行動テーブルから行動を選択する。
    ///
    /// 計算式:
    ///   lucDiff = プレイヤーLUC - 敵Speed（※敵に LUC がないため Speed で代用）
    ///   actionRange = baseActionRange - lucDiff
    ///   actionRange は最小20に制限（あまりに小さいと行動が破綻するため）
    ///
    /// 乱数 0 ～ actionRange-1 を振り、
    /// actions[i].threshold を昇順に走査して、乱数値 < threshold の最初の行動を返す。
    /// </summary>
    private EnemyActionEntry SelectEnemyAction()
    {
        // プレイヤーの LUC（Luck プロパティは baseLUC * 5）
        int playerLuc = (GameState.I != null) ? GameState.I.Luck : 0;

        // 敵側の運の指標
        int enemyLuc = enemyMonster.Luck;

        // LUC 差を算出（プレイヤーが高いほど正の値）
        int lucDiff = playerLuc - enemyLuc;

        // actionRange を計算（LUC 差が大きいほど乱数範囲が狭まり、敵が弱体化）
        int actionRange = enemyMonster.baseActionRange - lucDiff;

        // 下限制限（最小20: これ以下だと行動テーブルが機能しなくなる）
        if (actionRange < 20) actionRange = 20;

        // 乱数を振る
        int roll = Random.Range(0, actionRange);

        Debug.Log($"[Battle] EnemyAction: playerLuc={playerLuc} enemyLuc={enemyLuc} lucDiff={lucDiff} " +
                  $"baseRange={enemyMonster.baseActionRange} actionRange={actionRange} roll={roll}");

        // 行動テーブルを走査（threshold 昇順前提）
        for (int i = 0; i < enemyMonster.actions.Length; i++)
        {
            if (roll < enemyMonster.actions[i].threshold)
            {
                return enemyMonster.actions[i];
            }
        }

        // テーブル外（actionRange が baseActionRange より大きくなった場合など）
        // → 最後の行動を返す（通常は「何もしない」を末尾に置く想定）
        return enemyMonster.actions[enemyMonster.actions.Length - 1];
    }

    /// <summary>
    /// 選択された敵行動を実行する。
    /// </summary>
    private void ExecuteEnemyAction(EnemyActionEntry action)
    {
        switch (action.actionType)
        {
            case EnemyActionType.Attack:
                ExecuteEnemyNormalAttack(action);
                break;

            case EnemyActionType.SpecialAttack:
                ExecuteEnemySpecialAttack(action);
                break;

            case EnemyActionType.Idle:
                ExecuteEnemyIdle(action);
                break;

            default:
                ExecuteEnemyIdle(action);
                break;
        }
    }

    /// <summary>
    /// 敵の通常攻撃。Monster.Attack 依存ダメージ。
    /// </summary>
    private void ExecuteEnemyNormalAttack(EnemyActionEntry action)
    {
        int enemyDamage = enemyMonster.Attack;
        if (enemyDamage < 1) enemyDamage = 1;

        // プレイヤーにダメージ
        if (GameState.I != null)
        {
            GameState.I.currentHp -= enemyDamage;
            if (GameState.I.currentHp < 0) GameState.I.currentHp = 0;
        }

        // ログ表示（カスタム行動名があればそれを使用）
        string actionName = !string.IsNullOrEmpty(action.actionName) ? action.actionName : "攻撃";
        AddLog($"{enemyMonster.Mname} の{actionName}！ {enemyDamage}ダメージ！");

        // 敗北判定 → プレイヤーターンへ
        AfterEnemyAction();
    }

    /// <summary>
    /// 敵の特殊攻撃。固定ダメージ + 属性耐性による軽減。
    ///
    /// ダメージ計算:
    ///   baseDamage = action.fixedDamage（0 なら Monster.Attack を使用）
    ///   resistance = PassiveCalculator.CalcAttributeResistance(action.attackAttribute)
    ///   finalDamage = baseDamage × (1 - resistance / 100)
    ///   小数点以下を四捨五入（Mathf.RoundToInt）
    /// </summary>
    private void ExecuteEnemySpecialAttack(EnemyActionEntry action)
    {
        // 基礎ダメージ
        int baseDamage = action.fixedDamage > 0 ? action.fixedDamage : enemyMonster.Attack;
        if (baseDamage < 1) baseDamage = 1;

        // 属性耐性を取得
        int resistance = PassiveCalculator.CalcAttributeResistance(action.attackAttribute);

        // 耐性によるダメージ軽減（耐性50 → 50%減少）
        float reductionRate = resistance / 100f;
        int finalDamage = Mathf.RoundToInt(baseDamage * (1f - reductionRate));
        if (finalDamage < 0) finalDamage = 0;

        // プレイヤーにダメージ
        if (GameState.I != null)
        {
            GameState.I.currentHp -= finalDamage;
            if (GameState.I.currentHp < 0) GameState.I.currentHp = 0;
        }

        // ログ表示
        string actionName = !string.IsNullOrEmpty(action.actionName)
            ? action.actionName
            : $"{action.attackAttribute.ToJapanese()}攻撃";

        if (resistance > 0)
        {
            AddLog($"{enemyMonster.Mname} の{actionName}！（{action.attackAttribute.ToJapanese()}属性） " +
                   $"{finalDamage}ダメージ！（耐性で軽減）");
        }
        else
        {
            AddLog($"{enemyMonster.Mname} の{actionName}！（{action.attackAttribute.ToJapanese()}属性） " +
                   $"{finalDamage}ダメージ！");
        }

        Debug.Log($"[Battle] SpecialAttack: base={baseDamage} resistance={resistance} final={finalDamage}");

        // 敗北判定 → プレイヤーターンへ
        AfterEnemyAction();
    }

    /// <summary>
    /// 敵が何もしない。
    /// </summary>
    private void ExecuteEnemyIdle(EnemyActionEntry action)
    {
        string actionName = !string.IsNullOrEmpty(action.actionName) ? action.actionName : "様子を見ている";
        AddLog($"{enemyMonster.Mname} は{actionName}…");

        // 敗北判定 → プレイヤーターンへ
        AfterEnemyAction();
    }

    /// <summary>
    /// 敵の行動後の共通処理。
    /// プレイヤー敗北判定を行い、生存していればプレイヤーターンに戻す。
    /// </summary>
    private void AfterEnemyAction()
    {
        // プレイヤー敗北判定
        if (GameState.I != null && GameState.I.currentHp <= 0)
        {
            OnDefeat();
            return;
        }

        // プレイヤーターンに戻す
        SetButtonsInteractable(true);
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

    private void ResetBattleStatics()
    {
        battleInitialized = false;
        persistentLogLines.Clear();
    }

    // =========================================================
    // 武器スキル関連ユーティリティ
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
    // 魔法ドロップダウン関連ユーティリティ
    // =========================================================

    /// <summary>
    /// 魔法ドロップダウンの選択肢を更新する。
    /// PassiveCalculator.CollectMagicSkills() でインベントリ内の
    /// 魔法スキル一覧を取得し、ドロップダウンに反映する。
    /// 魔法スキルが1つもなければドロップダウンと魔法ボタンを非表示にする。
    /// </summary>
    private void RefreshMagicDropdown()
    {
        // 魔法スキル一覧を収集（重複なし）
        magicSkillList = PassiveCalculator.CollectMagicSkills();

        // 魔法スキルがなければ非表示
        if (magicSkillList.Count == 0)
        {
            if (magicDropdown != null) magicDropdown.gameObject.SetActive(false);
            if (magicButton != null) magicButton.gameObject.SetActive(false);
            return;
        }

        // ドロップダウンに選択肢を設定
        if (magicDropdown != null)
        {
            magicDropdown.gameObject.SetActive(true);
            magicDropdown.ClearOptions();

            var options = new List<string>();
            for (int i = 0; i < magicSkillList.Count; i++)
            {
                var skill = magicSkillList[i];
                // 「スキル名 (MP:消費量)」の形式で表示
                options.Add($"{skill.skillName} (MP:{skill.mpCost})");
            }
            magicDropdown.AddOptions(options);

            // 選択をリセット
            magicDropdown.value = 0;
            magicDropdown.RefreshShownValue();
        }

        // 魔法ボタンを表示
        if (magicButton != null)
        {
            magicButton.gameObject.SetActive(true);
            magicButton.interactable = !battleEnded;
        }
    }

    /// <summary>
    /// ドロップダウンで現在選択中の魔法スキルを返す。
    /// 選択が無効な場合は null を返す。
    /// </summary>
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

        // 魔法ボタンも連動して無効化/有効化する
        if (magicButton != null)
        {
            // 有効化時は RefreshMagicDropdown で個別制御するが、
            // 無効化時は確実に無効にする
            if (!interactable)
                magicButton.interactable = false;
            else if (magicSkillList != null && magicSkillList.Count > 0)
                magicButton.interactable = true;
        }
    }

    private void AddLog(string message)
    {
        logLines.Add(message);

        while (logLines.Count > MaxLogLines)
        {
            logLines.RemoveAt(0);
        }

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