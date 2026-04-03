using System;
using System.Collections;
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
///   BattleSceneController_PlayerAction.cs … プレイヤー行動（攻撃/スキル/魔法/防御/アイテム）
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

    [Header("UI - Battle Log Popup")]
    [Tooltip("戦闘ログ詳細ポップアップのルートパネル。初期状態は非表示。")]
    [SerializeField] private GameObject fullLogPanel;

    [Tooltip("ポップアップ内の ScrollView 配下にある TMP_Text（全ログ表示用）")]
    [SerializeField] private TMP_Text fullLogText;

    [Tooltip("ScrollView の Content（RectTransform）。コードから高さを制御する。")]
    [SerializeField] private RectTransform fullLogContent;

    [Tooltip("ポップアップを閉じる×ボタン")]
    [SerializeField] private Button fullLogCloseButton;

    [Tooltip("戦闘画面右上に配置するログ詳細ボタン")]
    [SerializeField] private Button fullLogOpenButton;

    [Header("UI - Buttons")]
    [SerializeField] private Button attackButton;
    [SerializeField] private Button skillButton;
    [SerializeField] private Button itemButton;
    [SerializeField] private Button magicButton;

    // =========================================================
    // 防御ボタン（追加）
    // =========================================================
    [Tooltip("防御コマンドボタン。防御中は物理・魔法防御力2倍、ダイス成功率UP。")]
    [SerializeField] private Button defendButton;

    [Header("UI - Magic Dropdown")]
    [Tooltip("所持中の魔法スキルを選択するドロップダウン")]
    [SerializeField] private TMP_Dropdown magicDropdown;

    // =========================================================
    // コンティニューポップアップ UI（追加）
    // =========================================================
    [Header("UI - Continue Popup")]
    [Tooltip("コンティニュー確認ポップアップのルートオブジェクト（ContineConfirmPopup）")]
    [SerializeField] private GameObject continuePopup;

    [Tooltip("ポップアップのメッセージテキスト")]
    [SerializeField] private TMP_Text continuePopupText;

    [Tooltip("はいボタン（広告視聴して復活）")]
    [SerializeField] private Button continueYesButton;

    [Tooltip("いいえボタン（街に帰還）")]
    [SerializeField] private Button continueNoButton;

    [Header("Scene Names")]
    [SerializeField] private string towerSceneName = "Tower";
    [SerializeField] private string mainSceneName = "Main";
    [SerializeField] private string itemboxSceneName = "Itembox";
    [SerializeField] private string talkSceneName = "Talk";

    // 戦闘中の敵HP（シーン再読込でも維持するため static）
    private static int enemyCurrentHp;
    private static bool battleInitialized = false;
    private static List<string> persistentLogLines = new List<string>();

    // =========================================================
    // 敵HP 読み取り用プロパティ（追加）
    // EnemyHpBar から参照する。
    // =========================================================
    /// <summary>戦闘中の敵の現在HP。EnemyHpBar から参照する。</summary>
    public static int EnemyCurrentHp => enemyCurrentHp;
    /// <summary>戦闘中の敵の最大HP。EnemyHpBar から参照する。</summary>
    public static int EnemyMaxHp => battleInitialized && BattleContext.EnemyMonster != null
        ? BattleContext.EnemyMonster.MaxHp : 0;

    // =========================================================
    // ターンカウンター（追加）
    // =========================================================
    /// <summary>現在のターン数。戦闘開始時に 0、プレイヤーターン開始時に +1。</summary>
    private static int currentTurnNumber = 0;

    private Monster enemyMonster;

    // =========================================================
    // 敵の状態異常（追加）
    // =========================================================

    /// <summary>戦闘中の敵の毒状態。戦闘終了でリセット。</summary>
    private static bool enemyIsPoisoned = false;

    // =========================================================
    // 防御フラグ（追加）
    // =========================================================
    //
    // プレイヤーが「防御」コマンドを選択したターンのみ true になる。
    // 敵ターンの防御ダイス計算で参照し、以下の効果を適用する:
    //   1. 物理防御力・魔法防御力が 2倍 になる
    //   2. 防御ダイスの diceRange が 1.5f になる（通常2.0f → 成功率50%→67%）
    // 次のプレイヤーターン開始時（BeginPlayerTurn）で false にリセットされる。
    // =========================================================

    /// <summary>プレイヤーが防御中かどうか。敵ターンの防御ダイス計算に影響する。</summary>
    private static bool isDefending = false;

    // =========================================================
    // 先制攻撃システム（追加）
    // =========================================================
    //
    // 毎ターン開始時（BeginPlayerTurn）に敵の行動を事前抽選する。
    // 先制技が選ばれた場合:
    //   プレイヤーの行動選択後、プレイヤー行動の前に敵先制技が割り込む。
    // 通常技の場合:
    //   従来通りプレイヤー→敵の順。
    //
    // pendingEnemyAction: 事前抽選された敵行動。null = 未抽選。
    // isEnemyPreemptive: 事前抽選の結果が先制技だったかどうか。
    // =========================================================

    /// <summary>事前抽選された敵行動。BeginPlayerTurn で設定される。</summary>
    private static EnemyActionEntry pendingEnemyAction = null;

    /// <summary>事前抽選された行動が先制技かどうか。</summary>
    private static bool isEnemyPreemptive = false;

    // =========================================================
    // 戦闘ログ（改修: 全件保持）
    // =========================================================
    // ログは戦闘開始から終了まで全件を保持する。
    // 通常画面には末尾 DisplayLogLines 行のみ表示し、
    // ポップアップで全履歴を確認できる。
    // =========================================================
    private List<string> logLines = new List<string>();
    private Queue<string> logQueue = new Queue<string>();
    private const int DisplayLogLines = 5;

    /// <summary>ログ1行あたりの表示間隔（秒）</summary>
    private const float LogDisplayInterval = 0.5f;

    /// <summary>全ログ表示後、次のフェーズに移るまでの待機時間（秒）</summary>
    private const float LogFlushPostDelay = 0.5f;

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
        if (defendButton != null) defendButton.onClick.AddListener(OnDefendClicked);

        // =========================================================
        // ログポップアップ ボタン登録（追加）
        // =========================================================
        if (fullLogOpenButton != null) fullLogOpenButton.onClick.AddListener(OpenFullLog);
        if (fullLogCloseButton != null) fullLogCloseButton.onClick.AddListener(CloseFullLog);
        if (fullLogPanel != null) fullLogPanel.SetActive(false);

        // =========================================================
        // コンティニューポップアップ ボタン登録（追加）
        // =========================================================
        if (continueYesButton != null) continueYesButton.onClick.AddListener(OnContinueYes);
        if (continueNoButton != null) continueNoButton.onClick.AddListener(OnContinueNo);
        if (continuePopup != null) continuePopup.SetActive(false);

        // =========================================================
        // ボス戦アイテムスナップショット（追加）
        // 戦闘開始時（初回のみ）にアイテムの状態を保存する。
        // コンティニュー時にこのスナップショットから復元する。
        // =========================================================
        if (!battleInitialized && BattleContext.IsBossBattle)
        {
            if (ItemBoxManager.Instance != null)
            {
                BattleContext.ItemSnapshot = ItemBoxManager.Instance.CreateSnapshot();
                Debug.Log($"[Battle] ボス戦アイテムスナップショット保存: {BattleContext.ItemSnapshot.Count} 個");
            }
        }

        if (!battleInitialized)
        {
            enemyCurrentHp = enemyMonster.MaxHp;
            battleInitialized = true;
            persistentLogLines.Clear();
            currentTurnNumber = 0; // ターンカウンターリセット
            isDefending = false; // 防御フラグリセット
            pendingEnemyAction = null; // 先制攻撃リセット
            isEnemyPreemptive = false;
            AddLogImmediate($"{enemyMonster.Mname} が現れた！");
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
                    AddLogImmediate(GameState.I.battleItemActionLog);
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
    // ターン開始ログ（追加）
    // =========================================================

    /// <summary>
    /// プレイヤーターンの開始時に呼ぶ。ターンカウンターを +1 し、ログに記録する。
    /// 防御フラグをリセットする（前ターンの防御効果を解除）。
    /// 敵の行動を事前抽選する（先制攻撃システム）。
    /// プレイヤー行動（OnAttackClicked 等）の冒頭から呼び出す。
    /// </summary>
    public void BeginPlayerTurn()
    {
        currentTurnNumber++;
        isDefending = false; // 前ターンの防御を解除
        AddLogImmediate($"―――（{currentTurnNumber}ターン目）―――");

        // =========================================================
        // 先制攻撃: 敵の行動を事前抽選（追加）
        // =========================================================
        PreRollEnemyAction();
    }

    /// <summary>
    /// 敵の行動を事前抽選する。
    /// actions 配列が未設定の場合は先制なし（従来通り）。
    /// 抽選結果を pendingEnemyAction / isEnemyPreemptive に保持する。
    /// </summary>
    private void PreRollEnemyAction()
    {
        pendingEnemyAction = null;
        isEnemyPreemptive = false;

        if (enemyMonster == null) return;
        if (enemyMonster.actions == null || enemyMonster.actions.Length == 0) return;

        pendingEnemyAction = SelectEnemyAction();

        if (pendingEnemyAction != null && pendingEnemyAction.skill != null
            && pendingEnemyAction.skill.actionType == MonsterActionType.Preemptive)
        {
            isEnemyPreemptive = true;
            Debug.Log($"[Battle] 先制攻撃抽選: {pendingEnemyAction.skill.skillName}");
        }
        else
        {
            Debug.Log($"[Battle] 通常行動抽選: " +
                      (pendingEnemyAction?.skill != null ? pendingEnemyAction.skill.skillName : "Legacy"));
        }
    }

    // =========================================================
    // 勝利 / 敗北
    // =========================================================

    /// <summary>
    /// 戦闘勝利時の処理。
    /// 経験値を付与し、レベルアップがあればログを表示する。
    /// ボス戦の場合は撃破フラグを記録し、勝利会話があれば Talk シーンへ遷移する。
    /// デバッグ戦闘の場合はデバッグシーンへ戻る。
    /// </summary>
    private void OnVictory()
    {
        battleEnded = true;
        AddLog($"{enemyMonster.Mname} を倒した！");
        SetButtonsInteractable(false);
        ResetAllWeaponCooldowns();
        ResetBattleStatics();
        enemyIsPoisoned = false;

        // ボス戦アイテムスナップショットをクリア（勝利したので不要）
        BattleContext.ItemSnapshot = null;

        // =========================================================
        // 経験値付与・レベルアップ処理（追加）
        // =========================================================
        if (GameState.I != null && enemyMonster.Exp > 0)
        {
            int expGained = enemyMonster.Exp;
            int levelUps = GameState.I.GainExp(expGained);
            AddLog($"{expGained} EXP を獲得！");

            if (levelUps > 0)
            {
                int pointGainTotal = 0;
                // レベルアップ分のポイント合計を計算（表示用）
                for (int i = 0; i < levelUps; i++)
                {
                    int lv = GameState.I.level - levelUps + 1 + i;
                    pointGainTotal += GameState.CalcStatusPointGain(lv);
                }
                AddLog($"レベルアップ！ Lv{GameState.I.level}（+{pointGainTotal}ステータスポイント）");
            }
        }

        // ログを全部表示してからシーン遷移
        FlushLogsAndThen(() =>
        {
            // =========================================================
            // デバッグ戦闘の場合はデバッグシーンへ戻る（追加）
            // ボス戦処理より前に判定する。デバッグではボス扱いしないため。
            // =========================================================
            if (BattleContext.IsDebugBattle)
            {
                string returnScene = BattleContext.DebugReturnScene;
                BattleContext.IsDebugBattle = false;
                BattleContext.DebugReturnScene = "Debug";
                Invoke(nameof(ReturnToDebug), 1.0f);
                return;
            }

            // =========================================================
            // ボス戦勝利処理（追加）
            // =========================================================
            if (BattleContext.IsBossBattle)
            {
                int bossFloor = BattleContext.BossFloor;
                string defeatedId = BossEncounterSystem.GetBossDefeatedId(bossFloor);

                // 撃破フラグを記録（セーブも自動で行われる）
                if (GameState.I != null)
                {
                    GameState.I.MarkPlayed(defeatedId);
                    Debug.Log($"[Battle] ボス撃破フラグ記録: {defeatedId}");
                }

                // ボス勝利会話があれば Talk シーンへ遷移
                // BossEncounterSystem から勝利会話イベントを取得
                string victoryTalkId = BossEncounterSystem.GetBossVictoryTalkId(bossFloor);

                // 勝利会話が未再生なら Talk シーンへ遷移
                if (GameState.I != null && !GameState.I.IsPlayed(victoryTalkId))
                {
                    GameState.I.pendingEventId = victoryTalkId;
                    BattleContext.IsBossBattle = false;
                    BattleContext.BossFloor = 0;
                    Invoke(nameof(ReturnToTalk), 1.0f);
                    return;
                }

                // 勝利会話が無い or 再生済みなら通常通り Tower へ
                BattleContext.IsBossBattle = false;
                BattleContext.BossFloor = 0;
                Invoke(nameof(ReturnToTower), 1.0f);
                return;
            }

            Invoke(nameof(ReturnToTower), 1.0f);
        });
    }

    /// <summary>
    /// 戦闘敗北時の処理。
    /// コンティニューポップアップを表示する。
    /// デバッグ戦闘の場合はポップアップを出さずにデバッグシーンへ戻る。
    /// </summary>
    private void OnDefeat()
    {
        battleEnded = true;
        AddLog("You は倒れた…");
        SetButtonsInteractable(false);
        ResetAllWeaponCooldowns();
        enemyIsPoisoned = false;

        // ログを全部表示してからポップアップ表示
        FlushLogsAndThen(() =>
        {
            // =========================================================
            // デバッグ戦闘の場合はデバッグシーンへ戻る（ポップアップ無し）
            // =========================================================
            if (BattleContext.IsDebugBattle)
            {
                ResetBattleStatics();
                BattleContext.ItemSnapshot = null;
                BattleContext.IsDebugBattle = false;
                BattleContext.DebugReturnScene = "Debug";
                Invoke(nameof(ReturnToDebug), 1.0f);
                return;
            }

            // =========================================================
            // コンティニューポップアップを表示（追加）
            // =========================================================
            ShowContinuePopup();
        });
    }

    // =========================================================
    // コンティニューポップアップ処理（追加）
    // =========================================================

    /// <summary>
    /// 敗北時にコンティニューポップアップを表示する。
    /// ボス戦と通常戦闘でメッセージを切り替える。
    /// </summary>
    private void ShowContinuePopup()
    {
        if (continuePopup == null)
        {
            // ポップアップUIが未設定の場合は従来通り街へ帰還
            Debug.LogWarning("[Battle] continuePopup が未設定のため従来の敗北処理を実行");
            FallbackDefeat();
            return;
        }

        // メッセージを設定
        if (continuePopupText != null)
        {
            if (BattleContext.IsBossBattle)
            {
                continuePopupText.text = "広告を視聴して戦闘をやり直しますか？\n（全回復、アイテム復活）";
            }
            else
            {
                continuePopupText.text = "広告を視聴してこのSTEPから続けますか？";
            }
        }

        continuePopup.SetActive(true);
    }

    /// <summary>
    /// コンティニュー「はい」ボタン押下時の処理。
    /// AdManager でリワード広告を表示し、成功なら復活する。
    /// </summary>
    private void OnContinueYes()
    {
        if (continuePopup != null) continuePopup.SetActive(false);

        if (AdManager.Instance != null)
        {
            AdManager.Instance.ShowRewardedAd(OnAdResult);
        }
        else
        {
            Debug.LogWarning("[Battle] AdManager.Instance が null — 広告なしで復活");
            OnAdResult(true);
        }
    }

    /// <summary>
    /// 広告視聴結果のコールバック。
    /// </summary>
    /// <param name="success">true = 視聴完了, false = 失敗/キャンセル</param>
    private void OnAdResult(bool success)
    {
        if (!success)
        {
            // 広告失敗 → 街に帰還（いいえと同じ扱い）
            Debug.Log("[Battle] 広告視聴失敗/キャンセル → 街に帰還");
            FallbackDefeat();
            return;
        }

        // 広告視聴成功 → 復活処理
        Debug.Log("[Battle] 広告視聴完了 → コンティニュー");

        if (BattleContext.IsBossBattle)
        {
            // ボス戦: 全回復 + アイテム復元 → 戦闘を最初からやり直し
            FullRecover();

            // アイテムスナップショットから復元
            if (BattleContext.ItemSnapshot != null && ItemBoxManager.Instance != null)
            {
                ItemBoxManager.Instance.RestoreFromSnapshot(BattleContext.ItemSnapshot);
                Debug.Log("[Battle] ボス戦コンティニュー: アイテムスナップショットから復元完了");
            }

            // 戦闘ステートをリセットして Battle シーンを再読込（再戦）
            ResetBattleStatics();
            // IsBossBattle と BossFloor はそのまま維持（再戦なので）
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
        else
        {
            // 通常戦闘: 全回復 → 現STEPから Tower シーンに戻る（戦闘回避扱い）
            FullRecover();
            ResetBattleStatics();
            BattleContext.ItemSnapshot = null;
            SceneManager.LoadScene(towerSceneName);
        }
    }

    /// <summary>
    /// コンティニュー「いいえ」ボタン押下時の処理。
    /// 従来通り街に帰還する。
    /// </summary>
    private void OnContinueNo()
    {
        if (continuePopup != null) continuePopup.SetActive(false);
        FallbackDefeat();
    }

    /// <summary>
    /// コンティニューしない場合の従来の敗北処理。
    /// ボス戦の場合は STEP を維持して街に戻る。
    /// </summary>
    private void FallbackDefeat()
    {
        ResetBattleStatics();
        BattleContext.ItemSnapshot = null;

        // ボス戦敗北処理（STEP を維持）
        if (BattleContext.IsBossBattle)
        {
            Debug.Log($"[Battle] ボス戦敗北。STEP={GameState.I?.step} を維持して街へ帰還。");
            BattleContext.IsBossBattle = false;
            BattleContext.BossFloor = 0;
        }

        Invoke(nameof(ReturnToMainWithFullRecover), 1.5f);
    }

    private void ReturnToTower() { SceneManager.LoadScene(towerSceneName); }

    /// <summary>
    /// ボス勝利後に Talk シーンへ遷移する。（追加）
    /// pendingEventId は OnVictory() で既にセット済み。
    /// </summary>
    private void ReturnToTalk()
    {
        SceneManager.LoadScene(talkSceneName);
    }

    private void ReturnToMainWithFullRecover()
    {
        FullRecover();
        SceneManager.LoadScene(mainSceneName);
    }

    /// <summary>
    /// デバッグ戦闘終了後にデバッグシーンへ戻る。（追加）
    /// 勝利・敗北どちらでもこのメソッドを使う。
    /// 全回復は行わない（デバッグシーンの全回復ボタンで手動操作する想定）。
    /// </summary>
    private void ReturnToDebug()
    {
        SceneManager.LoadScene(BattleContext.DebugReturnScene);
    }

    /// <summary>
    /// HP/MP全回復＋全状態異常クリア。
    /// ★ブラッシュアップ: 街に戻る = 全回復（状態異常含む）で統一。
    /// 敗北時・帰還時・ロード復帰時にこのメソッドを呼ぶ。
    /// ※ メインシーンの MainSceneRecovery.Start() でも全回復が走るため、
    ///   ここでの呼び出しは二重保険として残す。
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
        currentTurnNumber = 0; // ターンカウンターもリセット
        enemyIsPoisoned = false;
        isDefending = false; // 防御フラグもリセット
        pendingEnemyAction = null; // 先制攻撃もリセット
        isEnemyPreemptive = false;
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
        if (defendButton != null) defendButton.interactable = interactable;
        if (!interactable && skillButton != null) skillButton.interactable = false;
        if (magicButton != null)
        {
            if (!interactable) magicButton.interactable = false;
            else if (magicSkillList != null && magicSkillList.Count > 0) magicButton.interactable = true;
        }
    }

    // =========================================================
    // ログ管理（改修: ログキューシステム + 全件保持 + ポップアップ対応）
    // =========================================================
    //
    // 【ログキューシステム】
    //   AddLog() はログをキュー（logQueue）に追加するだけで、画面更新しない。
    //   FlushLogsAndThen(callback) でキュー内のログを LogDisplayInterval 秒間隔で
    //   1 行ずつ画面に表示し、全ログ表示完了から LogFlushPostDelay 秒後に
    //   callback を実行する。
    //   これにより自爆等の複数行ログもプレイヤーが読める。
    //
    //   AddLogImmediate() は従来通り即座に画面更新する。
    //   ターン区切り線や戦闘開始メッセージなど、待ち不要なものに使う。
    // =========================================================

    /// <summary>
    /// ログをキューに追加する（画面更新しない）。
    /// 実際の表示は FlushLogsAndThen() で行う。
    /// ログは全件保持し、永続ストアにも同期する。
    /// </summary>
    private void AddLog(string message)
    {
        logLines.Add(message);
        // 全件を永続ストアに同期（Itemboxシーン遷移時のリロード対応）
        persistentLogLines = new List<string>(logLines);
        logQueue.Enqueue(message);
    }

    /// <summary>
    /// ログを即座に画面に表示する（キューを経由しない）。
    /// ターン区切り線や戦闘開始メッセージなど、待ち不要なものに使う。
    /// </summary>
    private void AddLogImmediate(string message)
    {
        logLines.Add(message);
        // 全件を永続ストアに同期（Itemboxシーン遷移時のリロード対応）
        persistentLogLines = new List<string>(logLines);
        UpdateLogDisplay();
    }

    /// <summary>
    /// キュー内のログを LogDisplayInterval 秒間隔で 1 行ずつ画面に表示し、
    /// 全ログ表示完了から LogFlushPostDelay 秒後に callback を実行する。
    /// キューが空の場合は LogFlushPostDelay 秒後に即 callback 実行。
    /// </summary>
    private void FlushLogsAndThen(Action callback)
    {
        StartCoroutine(FlushLogsCoroutine(callback));
    }

    private IEnumerator FlushLogsCoroutine(Action callback)
    {
        while (logQueue.Count > 0)
        {
            logQueue.Dequeue(); // キューから取り出す（logLines には追加済み）
            UpdateLogDisplay(); // 画面を更新（logLines の末尾から表示済み分まで）
            yield return new WaitForSeconds(LogDisplayInterval);
        }

        // 全ログ表示後の待機
        yield return new WaitForSeconds(LogFlushPostDelay);

        callback?.Invoke();
    }

    /// <summary>
    /// 通常画面のログ表示を更新する。
    /// キュー内のログはまだ表示しない（表示済み分 = logLines.Count - logQueue.Count）。
    /// 末尾 DisplayLogLines 行のみ表示する。
    /// </summary>
    private void UpdateLogDisplay()
    {
        if (battleLogText == null) return;

        // 表示済みログ数（キュー内のログはまだ表示しない）
        int displayUpTo = logLines.Count - logQueue.Count;
        if (displayUpTo < 0) displayUpTo = 0;

        int displayStart = displayUpTo - DisplayLogLines;
        if (displayStart < 0) displayStart = 0;

        var displayLines = new List<string>();
        for (int i = displayStart; i < displayUpTo; i++)
        {
            displayLines.Add(logLines[i]);
        }
        battleLogText.text = string.Join("\n", displayLines);
    }

    // =========================================================
    // ログポップアップ UI（追加）
    // =========================================================

    /// <summary>
    /// ログ詳細ポップアップを開く。全ログを表示する。
    /// ContentSizeFitter に頼らず、コードから Content の高さを
    /// fullLogText の preferredHeight に合わせて強制セットする。
    /// これにより ScrollRect が正しくスクロール可能になる。
    /// </summary>
    private void OpenFullLog()
    {
        if (fullLogPanel == null || fullLogText == null) return;

        // テキストをセット
        fullLogText.text = string.Join("\n", logLines);
        fullLogPanel.SetActive(true);

        // テキストのレイアウトを強制更新して preferredHeight を取得
        fullLogText.ForceMeshUpdate();
        float preferredHeight = fullLogText.preferredHeight;

        // Content の高さをテキストの高さ + 余白に合わせる
        // （余白10ずつ = 上下合計20を加算）
        if (fullLogContent != null)
        {
            Vector2 size = fullLogContent.sizeDelta;
            size.y = preferredHeight + 20f;
            fullLogContent.sizeDelta = size;
        }
    }

    /// <summary>
    /// ログ詳細ポップアップを閉じる。
    /// </summary>
    private void CloseFullLog()
    {
        if (fullLogPanel == null) return;
        fullLogPanel.SetActive(false);
    }
}