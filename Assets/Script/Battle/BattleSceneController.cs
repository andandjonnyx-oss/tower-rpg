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

        // 敵の防御ダイスによる軽減（通常攻撃は物理扱い）
        int enemyDef = GetEnemyDefense(DamageCategory.Physical);
        int enemyBlocked = RollDefenseDice(enemyDef);
        int finalDamage = damage - enemyBlocked;
        if (finalDamage < 1) finalDamage = 1; // 最低保証1ダメージ

        // ダメージ適用
        enemyCurrentHp -= finalDamage;
        if (enemyCurrentHp < 0) enemyCurrentHp = 0;

        if (enemyBlocked > 0)
            AddLog($"You は {weaponName} で攻撃！（{weaponAttribute.ToJapanese()}属性） {finalDamage}ダメージ！（{enemyBlocked}軽減）");
        else
            AddLog($"You は {weaponName} で攻撃！（{weaponAttribute.ToJapanese()}属性） {finalDamage}ダメージ！");

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
    /// skill.damageCategory（Physical/Magical）に応じて敵の防御ダイスを選択する。
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
        int damage = Mathf.FloorToInt(baseDamage * skill.damageMultiplier + 0.5f);
        if (damage < 1) damage = 1;

        // スキルの属性で上書き（スキル固有の属性を使用）
        WeaponAttribute skillAttr = skill.skillAttribute;

        // 敵の防御ダイスによる軽減（skill.damageCategory で物理/魔法を判断）
        int enemyDef = GetEnemyDefense(skill.damageCategory);
        int enemyBlocked = RollDefenseDice(enemyDef);
        int finalDamage = damage - enemyBlocked;
        if (finalDamage < 1) finalDamage = 1; // 最低保証1ダメージ

        // ダメージ適用
        enemyCurrentHp -= finalDamage;
        if (enemyCurrentHp < 0) enemyCurrentHp = 0;

        // クールダウンをセット
        equippedWeaponItem.UseSkill(skill);

        if (enemyBlocked > 0)
            AddLog($"You は {skill.skillName}！（{skillAttr.ToJapanese()}属性） {finalDamage}ダメージ！（{enemyBlocked}軽減）");
        else
            AddLog($"You は {skill.skillName}！（{skillAttr.ToJapanese()}属性） {finalDamage}ダメージ！");

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
    /// skill.damageCategory（通常 Magical）に応じて敵の防御ダイスを選択する。
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
            // 倍率ベース（INTが基礎値）
            // SkillSource が Magic の場合は INT を、Weapon の場合は STR+武器を使う想定だが
            // 将来拡張のため magic.skillSource で分岐できるようにしておく
            int intStat = (GameState.I != null) ? GameState.I.baseINT : 1;
            damage = Mathf.FloorToInt(intStat * magic.damageMultiplier + 0.5f);
        }
        else
        {
            damage = 1;
        }
        if (damage < 1) damage = 1;

        // 敵の防御ダイスによる軽減（magic.damageCategory で物理/魔法を判断）
        // ファイアボール・ライトニングは通常 Magical を設定するので MagicDefense が適用される
        int enemyDef = GetEnemyDefense(magic.damageCategory);
        int enemyBlocked = RollDefenseDice(enemyDef);
        int finalDamage = damage - enemyBlocked;
        if (finalDamage < 1) finalDamage = 1; // 最低保証1ダメージ

        // ダメージ適用
        enemyCurrentHp -= finalDamage;
        if (enemyCurrentHp < 0) enemyCurrentHp = 0;

        if (enemyBlocked > 0)
            AddLog($"You は {magic.skillName}！（{magic.skillAttribute.ToJapanese()}属性） {finalDamage}ダメージ！（{enemyBlocked}軽減） MP-{magic.mpCost}");
        else
            AddLog($"You は {magic.skillName}！（{magic.skillAttribute.ToJapanese()}属性） {finalDamage}ダメージ！ MP-{magic.mpCost}");

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
    /// 防御ダイスによる軽減を適用する。
    /// </summary>
    private void ExecuteLegacyAttack()
    {
        // 敵の攻撃力（Monster.Attack をそのまま使用）
        int enemyDamage = enemyMonster.Attack;
        if (enemyDamage < 1) enemyDamage = 1;

        // 防御ダイスによる軽減（レガシー攻撃は物理扱い）
        int defense = GetPlayerDefense(DamageCategory.Physical);
        int blocked = RollDefenseDice(defense);
        int finalDamage = enemyDamage - blocked;
        if (finalDamage < 0) finalDamage = 0;

        // プレイヤーにダメージ
        ApplyDamageToPlayer(finalDamage);

        if (blocked > 0)
            AddLog($"{enemyMonster.Mname} の攻撃！ {finalDamage}ダメージ！（{blocked}軽減）");
        else
            AddLog($"{enemyMonster.Mname} の攻撃！ {finalDamage}ダメージ！");

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
    /// 比較対象:
    ///   プレイヤー側 = GameState.I.baseLUC（生のステータス値）
    ///   敵側         = Monster.Luck
    ///
    /// actionRange の決定ルール（baseActionRange=100 の場合）:
    ///   プレイヤー有利:
    ///     baseLUC が敵Luck より 10以上高い OR baseLUC が敵Luck の 1.5倍以上（四捨五入）
    ///     → actionRange = 80（敵が弱体化）
    ///     ※どちらか片方でも満たせば適用。両方満たす場合も 80。
    ///
    ///   敵有利:
    ///     baseLUC が敵Luck より 10以上低い OR baseLUC が敵Luck の半分未満（四捨五入）
    ///     → actionRange = 120（敵が強化）
    ///     ※どちらか片方でも満たせば適用。両方満たす場合も 120。
    ///
    ///   互角:
    ///     上記どちらの条件も満たさない場合
    ///     → actionRange = baseActionRange（通常100）
    ///
    ///   優先順位:
    ///     プレイヤー有利 と 敵有利 の両方が真になることはロジック上ないが、
    ///     万一の場合はプレイヤー有利（変動が大きい方）を優先する。
    ///
    /// 乱数 0 ～ actionRange-1 を振り、
    /// actions[i].threshold を昇順に走査して、乱数値 < threshold の最初の行動を返す。
    /// </summary>
    private EnemyActionEntry SelectEnemyAction()
    {
        // プレイヤーの baseLUC を直接参照（Luck プロパティ = baseLUC×5 は使わない）
        int playerLuc = (GameState.I != null) ? GameState.I.baseLUC : 1;

        // 敵の Luck を直接参照
        int enemyLuc = enemyMonster.Luck;

        // actionRange の決定
        int actionRange = CalcActionRange(playerLuc, enemyLuc, enemyMonster.baseActionRange);

        // 乱数を振る
        int roll = Random.Range(0, actionRange);

        Debug.Log($"[Battle] EnemyAction: playerLuc={playerLuc} enemyLuc={enemyLuc} " +
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
    /// プレイヤーと敵の Luck を比較し、行動判定の乱数上限値を返す。
    ///
    /// 閾値の決定:
    ///   「倍率による閾値」と「固定差（±10）による閾値」を両方計算し、
    ///   大きい方を採用する。
    ///
    ///   プレイヤー有利の閾値 = max( enemyLuc×1.5（四捨五入）, enemyLuc+10 )
    ///     → playerLuc がこの値以上なら、actionRange = baseRange × 0.8
    ///
    ///   敵有利の閾値       = max( enemyLuc×0.5（四捨五入）, enemyLuc-10 )
    ///     → playerLuc がこの値未満なら、actionRange = baseRange × 1.2
    ///
    /// 切り替わりの境界:
    ///   敵LUC  0～19 → +10 / -10 の固定差が優先（序盤安定）
    ///   敵LUC 20     → 同値
    ///   敵LUC 21以上  → ×1.5 / ×0.5 の倍率が優先（高LUC帯スケール）
    ///
    /// 矛盾（両条件同時成立）は発生しないことを検証済み。
    /// 万一の場合はプレイヤー有利を優先する。
    /// </summary>
    private int CalcActionRange(int playerLuc, int enemyLuc, int baseRange)
    {
        // --- プレイヤー有利の閾値 ---
        // 倍率閾値: 敵Luck の 1.5倍（四捨五入）
        int advByRatio = Mathf.FloorToInt(enemyLuc * 1.5f + 0.5f);
        // 固定差閾値: 敵Luck + 10
        int advByFixed = enemyLuc + 10;
        // 大きい方を採用
        int advThreshold = Mathf.Max(advByRatio, advByFixed);

        // --- 敵有利の閾値 ---
        // 倍率閾値: 敵Luck の半分（四捨五入）
        int disadvByRatio = Mathf.FloorToInt(enemyLuc * 0.5f + 0.5f);
        // 固定差閾値: 敵Luck - 10
        int disadvByFixed = enemyLuc - 10;
        // 大きい方を採用（-10 が負のケースでは ×0.5 が自動的に優先される）
        int disadvThreshold = Mathf.Max(disadvByRatio, disadvByFixed);

        // --- 判定（プレイヤー有利を優先） ---
        if (playerLuc >= advThreshold)
        {
            int range = Mathf.FloorToInt(baseRange * 0.8f + 0.5f);
            Debug.Log($"[Battle] LUC判定: プレイヤー有利 " +
                      $"playerLuc={playerLuc} >= {advThreshold}(ratio={advByRatio},fixed={advByFixed}) " +
                      $"actionRange={range}");
            return range;
        }

        if (playerLuc < disadvThreshold)
        {
            int range = Mathf.FloorToInt(baseRange * 1.2f + 0.5f);
            Debug.Log($"[Battle] LUC判定: 敵有利 " +
                      $"playerLuc={playerLuc} < {disadvThreshold}(ratio={disadvByRatio},fixed={disadvByFixed}) " +
                      $"actionRange={range}");
            return range;
        }

        Debug.Log($"[Battle] LUC判定: 互角 playerLuc={playerLuc} " +
                  $"advThreshold={advThreshold} disadvThreshold={disadvThreshold} " +
                  $"actionRange={baseRange}");
        return baseRange;
    }

    /// <summary>
    /// 選択された敵行動を実行する。
    /// EnemyActionEntry.skill が null の場合は通常攻撃（物理）にフォールバックする。
    /// </summary>
    private void ExecuteEnemyAction(EnemyActionEntry action)
    {
        // skill が未アサインの場合はフォールバック通常攻撃
        if (action.skill == null)
        {
            Debug.LogWarning("[Battle] EnemyActionEntry.skill が null です。通常攻撃で代替します。");
            ExecuteLegacyAttack();
            return;
        }

        switch (action.skill.actionType)
        {
            case MonsterActionType.NormalAttack:
                ExecuteEnemyNormalAttack(action.skill);
                break;

            case MonsterActionType.SkillAttack:
                ExecuteEnemySkillAttack(action.skill);
                break;

            case MonsterActionType.Idle:
                ExecuteEnemyIdle(action.skill);
                break;

            default:
                ExecuteEnemyIdle(action.skill);
                break;
        }
    }

    /// <summary>
    /// 敵の通常攻撃。Monster.Attack 依存ダメージ。
    /// skill.damageCategory に応じて物理防御 or 魔法防御のダイスを適用する。
    /// </summary>
    private void ExecuteEnemyNormalAttack(MonsterSkillData skill)
    {
        int enemyDamage = enemyMonster.Attack;
        if (enemyDamage < 1) enemyDamage = 1;

        // damageCategory に応じた防御力を取得
        int defense = GetPlayerDefense(skill.damageCategory);
        int blocked = RollDefenseDice(defense);
        int finalDamage = enemyDamage - blocked;
        if (finalDamage < 0) finalDamage = 0;

        // プレイヤーにダメージ
        ApplyDamageToPlayer(finalDamage);

        // ログ表示
        string actionName = !string.IsNullOrEmpty(skill.skillName) ? skill.skillName : "攻撃";
        if (blocked > 0)
            AddLog($"{enemyMonster.Mname} の{actionName}！ {finalDamage}ダメージ！（{blocked}軽減）");
        else
            AddLog($"{enemyMonster.Mname} の{actionName}！ {finalDamage}ダメージ！");

        // 敗北判定 → プレイヤーターンへ
        AfterEnemyAction();
    }

    /// <summary>
    /// 敵のスキル攻撃。MonsterSkillData のパラメータでダメージ計算する。
    ///
    /// ダメージ計算:
    ///   1. fixedDamage > 0 ならそれを使用
    ///      damageMultiplier > 0 なら Monster.Attack × damageMultiplier（四捨五入）
    ///      どちらも 0 なら Monster.Attack をそのまま使用
    ///   2. resistance = PassiveCalculator.CalcAttributeResistance(attackAttribute)
    ///      → afterResist = baseDamage × (1 - resistance / 100)
    ///      → 四捨五入（.5 は切り上げ）
    ///   3. 防御ダイスで軽減
    ///      → finalDamage = afterResist - blocked
    /// </summary>
    private void ExecuteEnemySkillAttack(MonsterSkillData skill)
    {
        // 基礎ダメージを決定
        int baseDamage;
        if (skill.fixedDamage > 0)
        {
            baseDamage = skill.fixedDamage;
        }
        else if (skill.damageMultiplier > 0f)
        {
            baseDamage = Mathf.FloorToInt(enemyMonster.Attack * skill.damageMultiplier + 0.5f);
        }
        else
        {
            baseDamage = enemyMonster.Attack;
        }
        if (baseDamage < 1) baseDamage = 1;

        // 属性耐性を取得
        int resistance = PassiveCalculator.CalcAttributeResistance(skill.attackAttribute);

        // 耐性によるダメージ増減（耐性50 → 50%減少、耐性-100 → 100%増加）
        // ※ Mathf.RoundToInt は銀行丸め（4.5→4）のため、
        //    FloorToInt(x + 0.5f) で通常の四捨五入（4.5→5）を行う
        float reductionRate = resistance / 100f;
        int afterResist = Mathf.FloorToInt(baseDamage * (1f - reductionRate) + 0.5f);
        if (afterResist < 0) afterResist = 0;

        // damageCategory に応じた防御ダイスによる軽減
        int defense = GetPlayerDefense(skill.damageCategory);
        int blocked = RollDefenseDice(defense);
        int finalDamage = afterResist - blocked;
        if (finalDamage < 0) finalDamage = 0;

        // プレイヤーにダメージ
        ApplyDamageToPlayer(finalDamage);

        // ログ表示
        string actionName = !string.IsNullOrEmpty(skill.skillName)
            ? skill.skillName
            : $"{skill.attackAttribute.ToJapanese()}攻撃";

        // ログにどの軽減が効いたかを表示
        string logSuffix = "";
        if (resistance > 0 && blocked > 0)
            logSuffix = $"（耐性で軽減+防御{blocked}軽減）";
        else if (resistance > 0)
            logSuffix = "（耐性で軽減）";
        else if (resistance < 0)
            logSuffix = "（弱点で増加）";
        else if (blocked > 0)
            logSuffix = $"（防御{blocked}軽減）";

        AddLog($"{enemyMonster.Mname} の{actionName}！（{skill.attackAttribute.ToJapanese()}属性） " +
               $"{finalDamage}ダメージ！{logSuffix}");

        Debug.Log($"[Battle] SkillAttack: base={baseDamage} resistance={resistance} " +
                  $"afterResist={afterResist} defense={defense} blocked={blocked} final={finalDamage}");

        // 敗北判定 → プレイヤーターンへ
        AfterEnemyAction();
    }

    /// <summary>
    /// 敵が何もしない。
    /// </summary>
    private void ExecuteEnemyIdle(MonsterSkillData skill)
    {
        string actionName = !string.IsNullOrEmpty(skill.skillName) ? skill.skillName : "様子を見ている";
        AddLog($"{enemyMonster.Mname} は{actionName}…");

        // 敗北判定 → プレイヤーターンへ
        AfterEnemyAction();
    }

    // =========================================================
    // 防御ダイス
    // =========================================================

    /// <summary>
    /// DamageCategory に応じたプレイヤーの防御力を返す。
    /// Physical → GameState.Defense（VIT ベース）
    /// Magical  → GameState.MagicDefense（INT ベース）
    /// </summary>
    private int GetPlayerDefense(DamageCategory category)
    {
        if (GameState.I == null) return 0;

        switch (category)
        {
            case DamageCategory.Physical:
                return GameState.I.Defense;
            case DamageCategory.Magical:
                return GameState.I.MagicDefense;
            default:
                return GameState.I.Defense;
        }
    }

    /// <summary>
    /// DamageCategory に応じた敵の防御力を返す。
    /// Physical → Monster.Defense
    /// Magical  → Monster.MagicDefense
    /// </summary>
    private int GetEnemyDefense(DamageCategory category)
    {
        if (enemyMonster == null) return 0;

        switch (category)
        {
            case DamageCategory.Physical:
                return enemyMonster.Defense;
            case DamageCategory.Magical:
                return enemyMonster.MagicDefense;
            default:
                return enemyMonster.Defense;
        }
    }

    /// <summary>
    /// 防御ダイスの基準乱数範囲（通常時）。
    /// 0 ～ この値の乱数を振り、1未満が出た数の合計がダメージ軽減値。
    /// 通常時: 2.0f → 成功率 50%
    /// 強化時: 1.5f → 成功率 66.7%
    /// 弱体時: 3.0f → 成功率 33.3%
    /// </summary>
    private const float DefaultDefenseDiceRange = 2.0f;

    /// <summary>
    /// 防御ダイスを振り、ダメージ軽減値を返す。
    ///
    /// ルール:
    ///   防御力の数だけ乱数（0 ～ diceRange）を振り、
    ///   1未満が出た回数の合計がダメージ軽減値。
    ///
    /// diceRange パラメータで防御強化/弱体を表現する:
    ///   通常 = 2.0f（成功率50%）
    ///   強化 = 1.5f（成功率67%）
    ///   弱体 = 3.0f（成功率33%）
    ///
    /// 将来的にスキルやバフで diceRange を変化させる場合は、
    /// このメソッドの diceRange 引数を変えるだけで対応可能。
    /// </summary>
    /// <param name="defense">プレイヤーの防御力。</param>
    /// <param name="diceRange">
    /// 防御ダイスの乱数上限。省略時は DefaultDefenseDiceRange（通常時）。
    /// 下限は 1.0f にクランプ（1.0f 未満だと常に成功＝全防御になるため）。
    /// </param>
    /// <returns>ダメージ軽減値。</returns>
    private int RollDefenseDice(int defense, float diceRange = DefaultDefenseDiceRange)
    {
        if (defense <= 0) return 0;

        // 下限クランプ: diceRange < 1.0f だと 0~diceRange の全域が 1 未満で常に成功
        if (diceRange < 1.0f) diceRange = 1.0f;

        int blocked = 0;
        for (int i = 0; i < defense; i++)
        {
            if (Random.Range(0f, diceRange) < 1f)
            {
                blocked++;
            }
        }

        Debug.Log($"[Battle] DefenseDice: defense={defense} diceRange={diceRange} blocked={blocked}");
        return blocked;
    }

    /// <summary>
    /// プレイヤーにダメージを適用する共通処理。
    /// </summary>
    private void ApplyDamageToPlayer(int damage)
    {
        if (GameState.I == null) return;
        GameState.I.currentHp -= damage;
        if (GameState.I.currentHp < 0) GameState.I.currentHp = 0;
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