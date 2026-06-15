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
///   BattleSceneController_BuffDebuff.cs   … バフ/デバフ5種ペアの管理
///   BattleSceneController_Petrify.cs      … 石化の管理（Phase A で追加）
/// </summary>
public partial class BattleSceneController : MonoBehaviour
{
    [Header("UI - Background")]
    [Tooltip("背景表示用 Image。Canvas 最背面（enemyImage より後ろ）に配置する。\n"
       + "Anchor=stretch / Left,Right,Top,Bottom=0 で全画面表示推奨。\n"
       + "未設定の場合は背景切り替えを行わない。")]
    [SerializeField] private Image backgroundImage;

    [Tooltip("塔内部の背景（通常ステップ用）。")]
    [SerializeField] private Sprite bgInterior;

    [Tooltip("塔内部・階段が見える背景（各階20STEP / 100Fは19STEP用）。")]
    [SerializeField] private Sprite bgStairs;

    [Tooltip("頂上の背景（100Fの20STEP用）。")]
    [SerializeField] private Sprite bgSummit;

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


    // =========================================================
    // 状態異常ランプ UI（追加）
    // =========================================================
    [Header("UI - Status Effect Lamp")]
    [Tooltip("味方の状態異常ランプ（joutaiijoujoumikata にアタッチ）")]
    [SerializeField] private StatusEffectLamp playerStatusLamp;

    [Tooltip("敵の状態異常ランプ（joutaiijoujouteki にアタッチ）")]
    [SerializeField] private StatusEffectLamp enemyStatusLamp;


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

    [Header("UI - Magic Selector")]
    [Tooltip("所持中の魔法スキルを選択する自作ドロップダウン（MagicSelector）")]
    [SerializeField] private MagicSelector magicSelector;

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

    // =========================================================
    // ギブアップポップアップ UI（追加）
    // =========================================================
    [Header("UI - Give Up")]
    [Tooltip("ギブアップボタン。戦闘中に押すと敗北扱いになる。")]
    [SerializeField] private Button giveUpButton;

    [Tooltip("ギブアップ確認ポップアップのルートオブジェクト")]
    [SerializeField] private GameObject giveUpPopup;

    [Tooltip("ギブアップ確認メッセージテキスト")]
    [SerializeField] private TMP_Text giveUpPopupText;

    [Tooltip("ギブアップ確認「はい」ボタン")]
    [SerializeField] private Button giveUpYesButton;

    [Tooltip("ギブアップ確認「いいえ」ボタン")]
    [SerializeField] private Button giveUpNoButton;

    // =========================================================
    // ドロップアイテム UI（追加）
    // =========================================================
    [Header("UI - Item Drop")]
    [Tooltip("勝利時のアイテムドロップ表示用ポップアップ。\n"
           + "Tower シーンの ItemPickupWindow と同じ Prefab を Battle シーンにも配置する。\n"
           + "未設定の場合、ドロップアイテムは自動入手（満杯時は拾えない）。")]
    [SerializeField] private ItemPickupWindow dropItemPickupWindow;

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
    /// <summary>戦闘中の敵の気絶状態。1ターン限定で自動解除。</summary>
    private static bool enemyIsStunned = false;

    // =========================================================
    // 敵の新状態異常（Phase2 追加）
    // =========================================================

    /// <summary>戦闘中の敵の麻痺状態。戦闘終了でリセット。</summary>
    private static bool enemyIsParalyzed = false;
    /// <summary>戦闘中の敵の暗闇状態。戦闘終了でリセット。</summary>
    private static bool enemyIsBlind = false;
    /// <summary>戦闘中の敵の沈黙状態。戦闘終了でリセット。</summary>
    private static bool enemyIsSilenced = false;

    /// <summary>敵の怒り残りターン数。0 = 通常。戦闘終了でリセット。</summary>
    private static int enemyRageTurn = 0;
    /// <summary>プレイヤーの怒り残りターン数。0 = 通常。戦闘終了でリセット。</summary>
    private static int playerRageTurn = 0;

    /// <summary> 力溜め→攻撃のようなターンをまたがった行動用　 </summary>
    private SkillData enemyForcedNextSkill;





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
    // 行動受付ガード（多重入力防止）
    // =========================================================
    //
    // プレイヤー行動ボタン（攻撃/スキル/魔法/防御）の押下から、
    // 敵ターンが完了してプレイヤーターンに戻るまでの間 true にする。
    // SetButtonsInteractable(false) はボタンの interactable を切るだけで、
    // 同一フレーム内に既にキューされた押下や連打の二度目を取りこぼすことが
    // あるため、コード側でも明示的にガードする。
    // OnXxxClicked の冒頭で true をチェックして二度目以降を弾き、
    // プレイヤーターン復帰時（AfterEnemyAction 末尾）に false へ戻す。
    private bool isPlayerActing = false;

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
    // ログ表示同期SE（追加）
    // =========================================================
    /// <summary>ログ行に紐づくSE種別。</summary>
    private enum BattleSeKind { None, Attack, Miss, Ailment, Heal, QuizCorrect, QuizWrong, Victory, LevelUp, Defeat }

    /// <summary>表示待ちログ1行分。テキストとSE情報を持つ。</summary>
    private struct LogEntry
    {
        public string text;
        public BattleSeKind kind;
        public WeaponAttribute attr; // kind == Attack の時のみ使用
    }

    // =========================================================
    // 戦闘ログ（改修: 全件保持）
    // =========================================================
    // ログは戦闘開始から終了まで全件を保持する。
    // 通常画面には末尾 DisplayLogLines 行のみ表示し、
    // ポップアップで全履歴を確認できる。
    // =========================================================
    private List<string> logLines = new List<string>();
    private Queue<LogEntry> logQueue = new Queue<LogEntry>();
    private const int DisplayLogLines = 5;

    /// <summary>ログ1行あたりの表示間隔（秒）</summary>
    private const float LogDisplayInterval = 0.5f;

    /// <summary>全ログ表示後、次のフェーズに移るまでの待機時間（秒）</summary>
    private const float LogFlushPostDelay = 0.5f;

    private bool battleEnded = false;

    // 装備中武器の InventoryItem キャッシュ（スキルクールダウン管理用）
    private InventoryItem equippedWeaponItem;

    // 魔法セレクターに表示中のスキル一覧キャッシュ
    private List<SkillData> magicSkillList = new List<SkillData>();

    // =========================================================
    // ドロップアイテム: 勝利後のシーン遷移先を保持（追加）
    // =========================================================
    // OnVictory の FlushLogsAndThen 内で決定されたシーン遷移処理を
    // ドロップアイテムポップアップの後に実行するために保持する。
    // =========================================================

    /// <summary>ドロップアイテム処理後に実行するシーン遷移アクション。</summary>
    private Action pendingVictoryTransition = null;

    /// <summary>ドロップ判定で選ばれたアイテム。ポップアップ結果のコールバックで使用。</summary>
    private ItemData droppedItemData = null;

    private Coroutine adTimeoutCoroutine;
    private bool adResultHandled = false;

    private void Start()
    {
        enemyMonster = BattleContext.EnemyMonster;
        if (enemyMonster == null)
        {
            Debug.LogError("[Battle] EnemyMonster is null");
            return;
        }


        // 現在地に応じた背景を適用（Tower と同じ判定）
        var gsBg = GameState.I;
        if (gsBg != null)
            DungeonBackground.Apply(backgroundImage, gsBg.floor, gsBg.step, bgInterior, bgStairs, bgSummit);

        // バトル BGM 再生（敵ごとに異なる。メイン曲=塔曲などを退避して上から流す）
        // Start() は Itembox 復帰時にも走るが、PlayOverlay は「同じclipなら鳴らし直さない」ため継続する。
        if (AudioManager.I != null && enemyMonster.battleBgm != null)
            AudioManager.I.PlayOverlay(enemyMonster.battleBgm);

        if (enemyImage != null)
        {
            enemyImage.sprite = enemyMonster.Image;
            enemyImage.preserveAspect = true;

            // =========================================================
            // バトル中の素材アスペクト比対応（追加）
            // =========================================================
            // 素材のアスペクト比に応じて RectTransform の Width を自動調整する。
            // これにより、横長素材（例: 1200×600）と正方形素材（600×600）が
            // 「同じ縮尺（1pxあたり同じ表示サイズ）」で表示される。
            //
            //   600×600  → Width=600,  Height=600 （正方形、変化なし）
            //   1200×600 → Width=1200, Height=600 （横長、左右に広がる）
            //   1800×600 → Width=1800, Height=600 （超横長）
            //
            // 図鑑等では Sprite 単体表示で preserveAspect=true により
            // 正方形枠内に縦横比保持で収まる（既存挙動を維持）。
            // =========================================================
            if (enemyMonster.Image != null)
            {
                RectTransform rt = enemyImage.rectTransform;
                float baseHeight = rt.sizeDelta.y;

                // テクスチャの実ピクセルサイズを使う（トリミングの影響を受けない）
                float spriteW = enemyMonster.Image.texture.width;
                float spriteH = enemyMonster.Image.texture.height;

                if (spriteH > 0f)
                {
                    float aspectRatio = spriteW / spriteH;
                    float newWidth = baseHeight * aspectRatio;
                    rt.sizeDelta = new Vector2(newWidth, baseHeight);
                }
            }
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
        // ギブアップポップアップ ボタン登録・初期化（追加）
        // =========================================================
        if (giveUpButton != null) giveUpButton.onClick.AddListener(OnGiveUpClicked);
        if (giveUpYesButton != null) giveUpYesButton.onClick.AddListener(OnGiveUpYes);
        if (giveUpNoButton != null) giveUpNoButton.onClick.AddListener(OnGiveUpNo);
        if (giveUpPopup != null) giveUpPopup.SetActive(false);

        // =========================================================
        // ドロップアイテムポップアップ 初期化（追加）
        // =========================================================
        if (dropItemPickupWindow != null) dropItemPickupWindow.HideImmediate();

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
            SkillEffectProcessor.ResetEnemyAilmentDummies();
            enemyCurrentHp = enemyMonster.MaxHp;
            battleInitialized = true;
            persistentLogLines.Clear();
            currentTurnNumber = 0; // ターンカウンターリセット
            isDefending = false; // 防御フラグリセット
            enemyIsStunned = false;
            enemyIsParalyzed = false; // Phase2: 麻痺リセット
            enemyIsBlind = false;     // Phase2: 暗闇リセット
            enemyRageTurn = 0;        // Phase2: 敵怒りリセット
            enemyIsSilenced = false;

            playerRageTurn = 0;       // Phase2: プレイヤー怒りリセット
            pendingEnemyAction = null; // 先制攻撃リセット
            isEnemyPreemptive = false;
            enemyForcedNextSkill = null; // 強制行動リセット
            turnLowHpMode = false;
            turnActionCount = 1;

            // Phase4: バフ/デバフリセット（構造体ベース）
            InitBuffDebuffFields();

            // Phase A: 敵側の石化リセット（プレイヤー側は戦闘終了後も継続）
            ResetEnemyPetrifyFields();

            ResetQuizBossStatics();

            // 魔法選択保持: 新しい戦闘の開始でリセット（一区切り）。
            // 同時に塔側の記憶もクリアする（「戦闘に入るまでの塔内部」が一区切りのため）。
            MagicSelectionMemory.ClearBattle();
            MagicSelectionMemory.ClearField();

            // モンスター図鑑: 遭遇記録
            if (GameState.I != null && enemyMonster != null)
            {
                GameState.I.MarkEncountered(enemyMonster.ID);
            }

            AddLogImmediate($"{enemyMonster.Mname} が現れた！");
        }
        else
        {
            logLines = new List<string>(persistentLogLines);
            UpdateLogDisplay();


            if (GameState.I != null && GameState.I.battleTurnConsumed)
            {
                GameState.I.battleTurnConsumed = false;

                // =========================================================
                // ターン消費行動（アイテム使用・装備変更）の共通ターン処理
                // 通常のプレイヤー行動は OnXxxClicked → BeginPlayerTurn() で
                // ターンカウンタ加算・ターン区切りログ・敵行動の事前抽選を行うが、
                // アイテム/装備は ItemBox シーンを経由するためそれを通らない。
                // ここで同等の処理を行い、ログのターン数ズレと
                // 敵行動の抽選状態不定（pendingEnemyAction の持ち越し）を防ぐ。
                // 表示順を通常行動と揃えるため、まずターン区切りを出してから
                // アクションログ（装備した／アイテム使用）を出す。
                // ※防御フラグ isDefending はここで必ずリセットする。
                //   アイテム/装備は ItemBox シーンを経由するため BeginPlayerTurn() を
                //   通らず、前ターンに張った防御が解除されないまま残ってしまう。
                //   （防御→アイテムの順で操作すると、アイテムターンの敵攻撃に
                //   防御2倍＋ダイス優遇が誤適用される不具合の修正）
                // =========================================================
                currentTurnNumber++;
                isDefending = false; // ★前ターンの防御を解除（BeginPlayerTurn 相当）
                AddLogImmediate($"―――（{currentTurnNumber}ターン目）―――");

                if (!string.IsNullOrEmpty(GameState.I.battleItemActionLog))
                {
                    AddLogImmediate(GameState.I.battleItemActionLog);
                    GameState.I.battleItemActionLog = "";
                }

                // =========================================================
                // ボス餌付け即勝利判定（追加）
                // =========================================================
                if (GameState.I.pendingBattleItemInstantWin)
                {
                    GameState.I.pendingBattleItemInstantWin = false;
                    BattleContext.IsBossEventWin = true;

                    // 餌付け専用ログ（敵名 + アイテム名）
                    string feedEnemyName = enemyMonster != null ? enemyMonster.Mname : "敵";
                    string feedItemName = GameState.I.pendingBattleItemName;
                    AddLog($"{feedEnemyName} は{feedItemName}を貪っている……");
                    AddLog($"{feedEnemyName} は満足した！");

                    GameState.I.pendingBattleItemName = "";
                    Debug.Log("[Battle] ボス餌付けアイテムによる即勝利！");
                    FlushLogsAndThen(() => OnVictory());
                    RefreshSkillButton();
                    RefreshMagicSelector();
                    RefreshBattleStatusEffectUI();
                    return;
                }

                // =========================================================
                // 攻撃アイテムのダメージ処理（追加）
                // =========================================================
                if (GameState.I.pendingBattleItemDamage > 0)
                {
                    ApplyBattleItemDamage();
                }

                equippedWeaponItem = GetEquippedWeaponItem();
                TickAllWeaponCooldowns();
                SetButtonsInteractable(false);

                // 攻撃アイテムで敵を倒した場合
                if (enemyCurrentHp <= 0)
                {
                    FlushLogsAndThen(() => OnVictory());
                    RefreshSkillButton();
                    RefreshMagicSelector();
                    RefreshBattleStatusEffectUI();  // ★追加: ここでも呼ぶ
                    return;
                }

                // ターン消費行動でも敵の行動を事前抽選してから敵ターンへ。
                PreRollEnemyAction();

                // =========================================================
                // 先制攻撃の割り込み実行（アイテム/装備フロー用）
                // 通常行動は OnXxxClicked 内で ExecutePreemptiveIfNeeded() を
                // 呼ぶが、アイテム/装備は ItemBox シーンを経由するためそこを
                // 通らない。ここで明示的に先制を実行しないと、EnemyTurn() が
                // isEnemyPreemptive==true を「先制済み」と誤判定して敵の行動を
                // 丸ごとスキップしてしまう（全行動が先制技の敵で発生）。
                //
                // ※方針B: アイテム効果は ItemBox で適用済みのため、
                //   「アイテム使用 → 敵の先制」の順になる。先制で敗北しても
                //   アイテムは消費される。
                // =========================================================
                if (ExecutePreemptiveIfNeeded())
                {
                    // 先制でプレイヤーが倒された（or 敵が自爆で倒れた）→
                    // ExecutePreemptiveIfNeeded 内で OnDefeat/OnVictory が
                    // 予約済みなので、ここでは何もせず終了する。
                    return;
                }

                // 敵ターンが Invoke で 0.5 秒後に走るまでの間、プレイヤー入力を
                // 受け付けないようにする。RefreshSkillButton / RefreshMagicSelector は
                // スキル・魔法ボタンを再有効化してしまうため、先に呼んでから
                // 最後に SetButtonsInteractable(false) で確実に無効化する。
                // さらに isPlayerActing を立てて、待機中の入力をコード側でも弾く。
                isPlayerActing = true;
                RefreshSkillButton();
                RefreshMagicSelector();
                RefreshBattleStatusEffectUI();
                SetButtonsInteractable(false);

                Invoke(nameof(EnemyTurn), 0.5f);
                return;
            }
        }

        RefreshSkillButton();
        RefreshMagicSelector();
        RefreshBattleStatusEffectUI();
        isPlayerActing = false; // プレイヤーターン開始: 行動ガードを解除
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

        // ターンスナップショット: この時点のHP割合で行動テーブル・行動回数を固定
        SnapshotTurnActionMode();

        EnemyActionEntry[] table = GetTurnActionTable();
        if (table == null || table.Length == 0) return;

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
    /// ドロップアイテムがあれば ItemPickupWindow を表示する。
    /// ボス戦の場合は撃破フラグを記録し、勝利会話があれば Talk シーンへ遷移する。
    /// デバッグ戦闘の場合はデバッグシーンへ戻る。
    /// </summary>
    private void OnVictory()
    {
        // 二重実行ガード（演出中の再呼び出し防止）
        if (battleEnded) return;
        battleEnded = true;
        SetButtonsInteractable(false);

        // =========================================================
        // 撃破演出の出し分け（BattleContext の状態は OnVictoryCore で
        // 書き換わる前にここで確定させる）
        // =========================================================
        bool feedWin = BattleContext.IsBossEventWin;                 // 餌付け勝利 → 画像不変
        bool toPhase2 = BattleContext.Phase2Monster != null          // 第二形態への連戦 → 既存ロジックで差し替え
                        && !BattleContext.IsPhase2Transition
                        && BattleContext.IsBossBattle;

        // ボス撃破（沈降演出）。本番ボス戦、またはモンスター自身が IsBoss フラグを
        // 持つ場合（デバッグ戦闘で IsBoss モンスターを呼んだ時もこれで沈降演出になる）。
        bool isBoss = BattleContext.IsBossBattle
                      || (enemyMonster != null && enemyMonster.IsBoss);

        // 餌付け勝利は演出なしで即本体処理（画像不変）
        if (feedWin)
        {
            // 勝敗確定 → BGM 停止
            if (AudioManager.I != null) AudioManager.I.StopOverlayKeepSilent();
            OnVictoryCore();
            return;
        }

        // 連戦（第二形態へ移行）: 第一形態を消してから本体処理（→ 再読込で第二形態出現）
        // ※ ここでは BGM を止めない。第二形態の Start() で次の BGM が鳴る（同じなら継続）。
        if (toPhase2)
        {
            StartCoroutine(Phase1VanishThenContinue());
            return;
        }

        // 勝敗確定 → BGM 停止（連戦でない通常勝利／ボス撃破）
        if (AudioManager.I != null) AudioManager.I.StopOverlayKeepSilent();


        // 通常モンスター（飛散）/ ボス（点滅→沈降）の演出を再生してから本体処理
        StartCoroutine(PlayDefeatThenVictory(isBoss));
    }

    /// <summary>
    /// 勝利時の本体処理。撃破演出の完了後（または演出不要時）に呼ばれる。
    /// </summary>
    private void OnVictoryCore()
    {
        battleEnded = true;

        // 第二形態への連戦時は勝利ファンファーレを鳴らさない（戦闘継続のため）
        bool toPhase2Next = BattleContext.Phase2Monster != null
                            && !BattleContext.IsPhase2Transition
                            && BattleContext.IsBossBattle;
        if (toPhase2Next)
            AddLog($"{enemyMonster.Mname} を倒した！");
        else
            AddLogEntry($"{enemyMonster.Mname} を倒した！", BattleSeKind.Victory, default);


        SetButtonsInteractable(false);
        ResetAllWeaponCooldowns();
        ResetBattleStatics();

        // =========================================================
        // 第二形態連戦チェック（追加）
        // =========================================================
        if (BattleContext.Phase2Monster != null && !BattleContext.IsPhase2Transition
            && BattleContext.IsBossBattle)
        {
            // 第一形態撃破 → 第二形態への連戦
            AddLog("……しかし、真の姿が現れた！");

            // フェーズフラグを1に更新してセーブ（アイテム状態も保存）
            // F70のフィールド名はBossEntryのphase2StateFieldから取得すべきだが、
            // ここではBossFloorから判定する
            // フェーズフラグを1に更新してセーブ（アイテム状態も保存）
            if (BattleContext.BossFloor == 70)
            {
                GameState.I.bossPhaseF70 = 1;
            }
            else if (BattleContext.BossFloor == 90)
            {
                GameState.I.bossPhaseF90 = 1;
            }
            else if (BattleContext.BossFloor == 100)
            {
                GameState.I.bossPhaseF100 = 1;
            }
            SaveManager.Save();

            // 第二形態の戦闘を開始（HP/MPそのまま）
            BattleContext.EnemyMonster = BattleContext.Phase2Monster;
            BattleContext.Phase2Monster = null;
            BattleContext.IsPhase2Transition = true;

            // 戦闘ステートをリセット（HP/MPはそのまま）
            ResetBattleStatics();
            // IsBossBattle, BossFloor はそのまま維持

            FlushLogsAndThen(() =>
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            });
            return;
        }

        // 第二形態連戦後のフラグクリア
        BattleContext.Phase2Monster = null;
        BattleContext.IsPhase2Transition = false;


        // ボス戦アイテムスナップショットをクリア（勝利したので不要）
        BattleContext.ItemSnapshot = null;

        // =========================================================
        // GP（がんばりポイント）加算（追加）
        // =========================================================
        if (GameState.I != null)
        {
            GameState.I.gp++;
            Debug.Log($"[Battle] GP+1 → 合計{GameState.I.gp}");
        }

        // 自爆や勝利後毒でHPが0の時は1にする
        if (GameState.I != null && GameState.I.currentHp <= 0)
        {
            GameState.I.currentHp = 1;
        }


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
                AddLogEntry($"レベルアップ！ Lv{GameState.I.level}（+{pointGainTotal}ステータスポイント）", BattleSeKind.LevelUp, default);
            }
        }

        // =========================================================
        // ドロップアイテム判定（追加）
        // =========================================================
        ItemData dropItem = TryRollDropItem();
        if (dropItem != null)
        {
            AddLog($"★ {dropItem.itemName} を見つけた！");
        }

        // ログを全部表示してからシーン遷移（またはドロップポップアップ）
        FlushLogsAndThen(() =>
        {
            // =========================================================
            // シーン遷移先の決定（ドロップアイテムがある場合は遷移を遅延する）
            // =========================================================
            Action transitionAction = DetermineVictoryTransition();

            // ドロップアイテムがあればポップアップを表示
            if (dropItem != null)
            {
                ShowDropItemPopup(dropItem, transitionAction);
            }
            else
            {
                // ドロップなし → 即遷移
                transitionAction?.Invoke();
            }
        });
    }

    /// <summary>
    /// ドロップアイテムの抽選を行う。
    /// dropItem が設定されていて、dropRate の確率判定を通過した場合にアイテムを返す。
    /// </summary>
    private ItemData TryRollDropItem()
    {
        if (enemyMonster == null) return null;
        if (enemyMonster.dropItem == null) return null;
        if (enemyMonster.dropRate <= 0f) return null;

        float roll = UnityEngine.Random.value;
        if (roll < enemyMonster.dropRate)
        {
            Debug.Log($"[Battle] ドロップ成功: {enemyMonster.dropItem.itemName} (roll={roll:F3} < rate={enemyMonster.dropRate:F3})");
            return enemyMonster.dropItem;
        }

        Debug.Log($"[Battle] ドロップ失敗 (roll={roll:F3} >= rate={enemyMonster.dropRate:F3})");
        return null;
    }

    /// <summary>
    /// 勝利後のシーン遷移先を決定し、Action として返す。
    /// OnVictory の FlushLogsAndThen 内で呼ばれる。
    /// ドロップアイテムポップアップがある場合、この Action はポップアップ終了後に実行される。
    /// </summary>
    private Action DetermineVictoryTransition()
    {
        // デバッグ戦闘
        if (BattleContext.IsDebugBattle)
        {
            BattleContext.IsDebugBattle = false;
            BattleContext.DebugReturnScene = "Debug";
            return () => Invoke(nameof(ReturnToDebug), 1.0f);
        }

        // ボス戦勝利処理
        if (BattleContext.IsBossBattle)
        {
            int bossFloor = BattleContext.BossFloor;
            string defeatedId = BossEncounterSystem.GetBossDefeatedId(bossFloor);

            if (GameState.I != null)
            {
                GameState.I.MarkPlayed(defeatedId);
                Debug.Log($"[Battle] ボス撃破フラグ記録: {defeatedId}");
                // 第二形態クリアフラグ（bossPhaseを2に）
                if (bossFloor == 70)
                {
                    GameState.I.bossPhaseF70 = 2;
                }
                else if (bossFloor == 90)
                {
                    GameState.I.bossPhaseF90 = 2;
                }
                else if (bossFloor == 100)
                {
                    GameState.I.bossPhaseF100 = 2;
                }
            }

            // イベント勝利（餌付け等）と通常勝利で会話IDを分岐
            string baseVictoryTalkId = BossEncounterSystem.GetBossVictoryTalkId(bossFloor);
            string victoryTalkId;
            if (BattleContext.IsBossEventWin)
            {
                victoryTalkId = baseVictoryTalkId + "_EVENT";
            }
            else
            {
                victoryTalkId = baseVictoryTalkId;
            }

            // フラグリセット
            BattleContext.IsBossEventWin = false;

            if (GameState.I != null && !GameState.I.IsPlayed(victoryTalkId))
            {
                return () =>
                {
                    // 会話遷移が決定してから既読にする
                    GameState.I.MarkPlayed(baseVictoryTalkId);
                    GameState.I.MarkPlayed(baseVictoryTalkId + "_EVENT");

                    GameState.I.pendingEventId = victoryTalkId;
                    BattleContext.IsBossBattle = false;
                    BattleContext.BossFloor = 0;
                    Invoke(nameof(ReturnToTalk), 1.0f);
                };
            }

            // 会話が既に再生済みの場合もマーク（安全のため）
            if (GameState.I != null)
            {
                GameState.I.MarkPlayed(baseVictoryTalkId);
                GameState.I.MarkPlayed(baseVictoryTalkId + "_EVENT");
            }

            BattleContext.IsBossBattle = false;
            BattleContext.BossFloor = 0;
            return () => Invoke(nameof(ReturnToTower), 1.0f);
        }

        // 通常戦闘
        // 餌付け（pendingBattleItemInstantWin）即勝利は acceptsFeedItem を持つ
        // 通常モンスターでも発火し、Start の即勝利パスで IsBossEventWin=true を立てる。
        // ボス戦ルートと違い通常戦闘ルートはこのフラグをリセットしないため、
        // 残留して次戦以降の通常モンスター撃破で飛散演出がスキップされる。
        // ここで確実にクリアする。
        BattleContext.IsBossEventWin = false;
        return () => Invoke(nameof(ReturnToTower), 1.0f);
    }

    /// <summary>
    /// ドロップアイテムの ItemPickupWindow を表示する。
    /// Tower シーンの TowerItemTrigger と同じ UX（拾う/捨てる/整理する）。
    ///
    /// dropItemPickupWindow が未設定の場合は自動入手を試みる（フォールバック）。
    /// </summary>
    private void ShowDropItemPopup(ItemData item, Action afterTransition)
    {
        droppedItemData = item;
        // 図鑑記録（入手・諦め問わずドロップ確定時点で登録）
        if (GameState.I != null) GameState.I.MarkItemDiscovered(item.itemId);
        pendingVictoryTransition = afterTransition;

        // ポップアップが未設定の場合: 自動入手フォールバック
        if (dropItemPickupWindow == null)
        {
            Debug.LogWarning("[Battle] dropItemPickupWindow が未設定。自動入手を試みます。");
            if (ItemBoxManager.Instance != null && ItemBoxManager.Instance.CanAddItem(item))
            {
                ItemBoxManager.Instance.AddItem(item);
                Debug.Log($"[Battle] ドロップアイテム自動入手: {item.itemName}");
            }
            else
            {
                Debug.Log($"[Battle] アイテムBOXが満杯のため {item.itemName} を入手できなかった");
            }
            droppedItemData = null;
            afterTransition?.Invoke();
            return;
        }

        // ポップアップ表示
        bool isFull = ItemBoxManager.Instance != null && ItemBoxManager.Instance.IsFull;
        bool canGet = ItemBoxManager.Instance != null && ItemBoxManager.Instance.CanAddItem(item);

        dropItemPickupWindow.Show(
            item.itemName, item.description, item.icon,
            canGet, isFull, OnDropItemResult);
    }

    /// <summary>
    /// ドロップアイテムポップアップの結果コールバック。
    /// TowerItemTrigger.OnItemResult と同じパターン。
    /// </summary>
    private void OnDropItemResult(ItemPickupResult result)
    {
        Debug.Log($"[Battle] OnDropItemResult: {result}");

        switch (result)
        {
            case ItemPickupResult.Get:
                if (droppedItemData != null && ItemBoxManager.Instance != null)
                {
                    bool added = ItemBoxManager.Instance.AddItem(droppedItemData);
                    Debug.Log(added
                        ? $"[Battle] ドロップアイテム入手: {droppedItemData.itemName}"
                        : "[Battle] アイテムBOXが満杯のため入手できなかった");
                }
                droppedItemData = null;
                // シーン遷移を実行
                pendingVictoryTransition?.Invoke();
                pendingVictoryTransition = null;
                break;

            case ItemPickupResult.Exchange:
                // 整理フロー: pendingItemData に記録して Itembox へ遷移
                // Itembox から戻る先は Tower（Battle は終了しているため）
                if (GameState.I != null)
                {
                    GameState.I.pendingItemData = droppedItemData;
                    GameState.I.isInBattle = false; // バトル中フラグ解除
                    GameState.I.previousSceneName = towerSceneName; // Itembox からの戻り先を Tower に
                }
                droppedItemData = null;
                pendingVictoryTransition = null; // Itembox → Tower の流れになるため不要
                SceneManager.LoadScene(itemboxSceneName);
                break;

            case ItemPickupResult.Ignore:
                Debug.Log("[Battle] ドロップアイテムを諦めた");
                droppedItemData = null;
                // シーン遷移を実行
                pendingVictoryTransition?.Invoke();
                pendingVictoryTransition = null;
                break;
        }
    }

    /// <summary>
    /// 戦闘敗北時の処理。
    /// コンティニューポップアップを表示する。
    /// デバッグ戦闘の場合はポップアップを出さずにデバッグシーンへ戻る。
    /// </summary>
    private void OnDefeat()
    {
        battleEnded = true;
        AddLogEntry("You は倒れた…", BattleSeKind.Defeat, default);
        SetButtonsInteractable(false);
        ResetAllWeaponCooldowns();

        // 勝敗確定 → BGM 停止
        if (AudioManager.I != null) AudioManager.I.StopOverlayKeepSilent();


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
    /// 広告視聴結果のコールバック。
    /// </summary>
    /// <param name="success">true = 視聴完了, false = 失敗/キャンセル</param>
    private void OnAdResult(bool success)
    {

        if (adResultHandled) return;   // タイムアウトと本来のコールバックの二重発火防止
        adResultHandled = true;
        if (adTimeoutCoroutine != null) { StopCoroutine(adTimeoutCoroutine); adTimeoutCoroutine = null; }


        // =========================================================
        // 方針A: success は常に true 想定（報酬獲得・スキップ・各種失敗の
        // いずれでも復活）。万一 false が来た場合も、ここでは復活させる。
        // 「いいえ」を選んだ場合は OnContinueNo → FallbackDefeat を通るため、
        // この OnAdResult には来ない。
        // =========================================================
        if (!success)
        {
            Debug.Log("[Battle] 広告結果 false だが方針Aのため復活扱いとする");
        }
        else
        {
            Debug.Log("[Battle] 広告視聴完了 → コンティニュー");
        }

        // --- 以下、復活処理（従来の success==true ブロックをそのまま） ---
        if (BattleContext.IsBossBattle)
        {
            FullRecover();

            if (BattleContext.ItemSnapshot != null && ItemBoxManager.Instance != null)
            {
                ItemBoxManager.Instance.RestoreFromSnapshot(BattleContext.ItemSnapshot);
                Debug.Log("[Battle] ボス戦コンティニュー: アイテムスナップショットから復元完了");
            }

            BattleContext.Phase2Monster = null;
            BattleContext.IsPhase2Transition = false;

            ResetBattleStatics();
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
        else
        {
            FullRecover();
            ResetBattleStatics();
            BattleContext.ItemSnapshot = null;
            SceneManager.LoadScene(towerSceneName);
        }
    }

    private void OnContinueYes()
    {
        if (continuePopup != null) continuePopup.SetActive(false);

        adResultHandled = false;

        if (AdManager.Instance != null)
        {
            // タイムアウト保険（10秒返ってこなければ見た扱いで復活）
            adTimeoutCoroutine = StartCoroutine(AdTimeoutFallback(10f));
            AdManager.Instance.ShowRewardedAd(OnAdResult);
        }
        else
        {
            Debug.LogWarning("[Battle] AdManager.Instance が null — 広告なしで復活");
            OnAdResult(true);
        }
    }

    private IEnumerator AdTimeoutFallback(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        if (!adResultHandled)
        {
            Debug.LogWarning("[Battle] 広告コールバックがタイムアウト → 方針Aで復活");
            OnAdResult(true);
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

        // =========================================================
        // 統計: 全滅帰還回数を加算（追加）
        // コンティニュー「いいえ」・広告失敗・ギブアップ後の帰還が
        // すべてここを通る。広告コンティニューで復活した場合は通らない。
        // =========================================================
        if (GameState.I != null)
        {
            GameState.I.statDefeatCount++;
            SaveManager.Save();
        }

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
        enemyIsStunned = false;
        enemyIsParalyzed = false; // Phase2: 麻痺リセット
        enemyIsBlind = false;     // Phase2: 暗闇リセット
        enemyIsSilenced = false;
        enemyRageTurn = 0;        // Phase2: 敵怒りリセット
        playerRageTurn = 0;       // Phase2: プレイヤー怒りリセット
        isDefending = false; // 防御フラグもリセット
        pendingEnemyAction = null; // 先制攻撃もリセット
        isEnemyPreemptive = false;
        turnLowHpMode = false;
        turnActionCount = 1;

        // Phase4: バフ/デバフリセット（構造体ベース）
        ResetBuffDebuffFields();

        // Phase A: 敵側の石化リセット（プレイヤー側は戦闘終了後も継続するので触らない）
        ResetEnemyPetrifyFields();

        ResetQuizBossStatics();

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
    // 魔法セレクター関連ユーティリティ（MagicSelector 版に変更）
    // =========================================================

    private void RefreshMagicSelector()
    {

        // 沈黙チェック: 味方が沈黙中は魔法UI全体を非表示
        if (GameState.I != null && GameState.I.isSilenced)
        {
            if (magicSelector != null) magicSelector.SetVisible(false);
            if (magicButton != null) magicButton.gameObject.SetActive(false);
            return;
        }

        magicSkillList = PassiveCalculator.CollectMagicSkills();
        if (magicSkillList.Count == 0)
        {
            if (magicSelector != null) magicSelector.SetVisible(false);
            if (magicButton != null) magicButton.gameObject.SetActive(false);
            return;
        }
        if (magicSelector != null)
        {
            magicSelector.SetVisible(true);
            var optionLabels = new List<string>();
            for (int i = 0; i < magicSkillList.Count; i++)
                optionLabels.Add($"{magicSkillList[i].skillName} (MP:{magicSkillList[i].mpCost})");
            magicSelector.SetOptions(optionLabels);

            // 選択保持（オプションON時）: 前回選択した魔法を復元する
            MagicSelectionMemory.Restore(magicSelector, magicSkillList, isBattle: true);

            // 選択変更の記録（多重登録防止のため一度解除してから登録）
            magicSelector.onValueChanged -= OnMagicSelectionChanged;
            magicSelector.onValueChanged += OnMagicSelectionChanged;
        }
        if (magicButton != null)
        {
            magicButton.gameObject.SetActive(true);
            magicButton.interactable = !battleEnded;
        }
    }

    /// <summary>
    /// 魔法セレクターの選択変更を記録する（選択保持オプション用）。
    /// </summary>
    private void OnMagicSelectionChanged(int index)
    {
        if (magicSkillList == null || index < 0 || index >= magicSkillList.Count) return;
        if (magicSkillList[index] == null) return;
        MagicSelectionMemory.BattleSkillId = magicSkillList[index].skillId;
    }

    private SkillData GetSelectedMagicSkill()
    {
        if (magicSelector == null) return null;
        if (magicSkillList == null || magicSkillList.Count == 0) return null;
        int index = magicSelector.Value;
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
        if (giveUpButton != null) giveUpButton.interactable = interactable;
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
    /// ログをキューに追加する（画面更新しない）。SEなし。
    /// 実際の表示は FlushLogsAndThen() で行う。
    /// </summary>
    private void AddLog(string message)
    {
        AddLogEntry(message, BattleSeKind.None, default);
    }

    /// <summary>ダメージ・命中行用。表示の瞬間に属性別の攻撃SEを鳴らす。</summary>
    private void AddLogAttack(string message, WeaponAttribute attr)
    {
        AddLogEntry(message, BattleSeKind.Attack, attr);
    }

    /// <summary>ミス・無効行用。表示の瞬間にミスSEを鳴らす。</summary>
    private void AddLogMiss(string message)
    {
        AddLogEntry(message, BattleSeKind.Miss, default);
    }

    /// <summary>状態異常・デバフ行用。表示の瞬間に状態異常SEを鳴らす。</summary>
    private void AddLogAilment(string message)
    {
        AddLogEntry(message, BattleSeKind.Ailment, default);
    }

    /// <summary>回復・バフ行用。表示の瞬間に回復SEを鳴らす。</summary>
    private void AddLogHeal(string message)
    {
        AddLogEntry(message, BattleSeKind.Heal, default);
    }

    private void AddLogEntry(string message, BattleSeKind kind, WeaponAttribute attr)
    {
        logLines.Add(message);
        // 全件を永続ストアに同期（Itemboxシーン遷移時のリロード対応）
        persistentLogLines = new List<string>(logLines);
        logQueue.Enqueue(new LogEntry { text = message, kind = kind, attr = attr });
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
            LogEntry entry = logQueue.Dequeue(); // キューから取り出す（logLines には追加済み）
            PlayLogSe(entry);                    // 表示の瞬間にSEを鳴らす
            UpdateLogDisplay();                  // 画面を更新
            yield return new WaitForSeconds(LogDisplayInterval);
        }

        // 全ログ表示後の待機
        yield return new WaitForSeconds(LogFlushPostDelay);

        callback?.Invoke();
    }

    /// <summary>ログ行に紐づくSEを再生する。</summary>
    private void PlayLogSe(LogEntry entry)
    {
        if (AudioManager.I == null) return;
        switch (entry.kind)
        {
            case BattleSeKind.Attack: AudioManager.I.PlayAttackSe(entry.attr); break;
            case BattleSeKind.Miss: AudioManager.I.PlayMissSe(); break;
            case BattleSeKind.Ailment: AudioManager.I.PlayAilmentSe(); break;
            case BattleSeKind.Heal: AudioManager.I.PlayHealSe(); break;
            case BattleSeKind.QuizCorrect: AudioManager.I.PlayQuizCorrectSe(); break;
            case BattleSeKind.QuizWrong: AudioManager.I.PlayQuizWrongSe(); break;
            case BattleSeKind.Victory: AudioManager.I.PlayVictorySe(); break;
            case BattleSeKind.LevelUp: AudioManager.I.PlayLevelUpSe(); break;
            case BattleSeKind.Defeat: AudioManager.I.PlayDefeatSe(); break;
        }
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

    // =========================================================
    // ギブアップ処理（追加）
    // =========================================================
    //
    // ギブアップボタンを押すと確認ポップアップを表示する。
    // 「はい」で OnDefeat() を呼び、通常の敗北と同じフローに入る。
    // これによりコンティニュー（広告視聴→復活）も使える。
    //
    // 中断（アプリ強制終了等）だとコンティニュー機能が使えないが、
    // ギブアップは正規の敗北処理を経由するためコンティニュー可能。
    // =========================================================

    /// <summary>
    /// ギブアップボタン押下時の処理。
    /// 確認ポップアップを表示する。
    /// </summary>
    private void OnGiveUpClicked()
    {
        if (battleEnded) return;

        if (giveUpPopup == null)
        {
            // ポップアップUIが未設定の場合は直接敗北処理
            Debug.LogWarning("[Battle] giveUpPopup が未設定のため直接敗北処理を実行");
            OnDefeat();
            return;
        }

        // ボタンを無効化して操作を防ぐ
        SetButtonsInteractable(false);

        if (AudioManager.I != null) AudioManager.I.PlayPopupSe();

        // メッセージを設定
        if (giveUpPopupText != null)
        {
            giveUpPopupText.text = "ギブアップしますか？\n（コンティニュー可能）";
        }

        giveUpPopup.SetActive(true);
    }

    /// <summary>
    /// ギブアップ確認「はい」ボタン押下時の処理。
    /// 敗北扱いにして OnDefeat() を呼ぶ。
    /// </summary>
    private void OnGiveUpYes()
    {
        if (giveUpPopup != null) giveUpPopup.SetActive(false);

        AddLog("You はギブアップした…");
        OnDefeat();
    }

    /// <summary>
    /// ギブアップ確認「いいえ」ボタン押下時の処理。
    /// ポップアップを閉じて戦闘に戻る。
    /// </summary>
    private void OnGiveUpNo()
    {
        if (giveUpPopup != null) giveUpPopup.SetActive(false);

        // ボタンを再有効化
        isPlayerActing = false;
        SetButtonsInteractable(true);
        RefreshSkillButton();
        RefreshMagicSelector();
    }

    /// <summary>
    /// 攻撃アイテム使用時のダメージ計算を実行する。
    /// Itembox から復帰した際に、GameState に保存されたダメージ情報を読み取って
    /// 敵にダメージを与える。
    ///
    /// ダメージ計算:
    ///   1. 固定ダメージ（battleDamage）をベースとする
    ///   2. 敵の属性耐性で軽減（battleAttribute を参照）
    ///   3. 防御ダイスで軽減（battleDamageCategory に基づく）
    ///   4. 最終ダメージを敵HPから差し引く
    ///
    /// 処理後、GameState の一時保存フィールドをリセットする。
    /// </summary>
    /// 

    /// <summary>
    /// 戦闘中の状態異常テキストを更新する。
    /// Phase4: 14引数 SetAll で全ランプを更新する。
    /// </summary>
    private void RefreshBattleStatusEffectUI()
    {
        RefreshBuffDebuffLamps(); // _BuffDebuff.cs に委譲
    }
    private void ApplyBattleItemDamage()
    {
        int baseDamage = GameState.I.pendingBattleItemDamage;
        WeaponAttribute attr = (WeaponAttribute)GameState.I.pendingBattleItemAttribute;
        DamageCategory dmgCat = (DamageCategory)GameState.I.pendingBattleItemDamageCategory;
        string itemName = GameState.I.pendingBattleItemName;

        // 一時保存フィールドをリセット
        GameState.I.pendingBattleItemDamage = 0;
        GameState.I.pendingBattleItemAttribute = 0;
        GameState.I.pendingBattleItemDamageCategory = 0;
        GameState.I.pendingBattleItemName = "";

        if (baseDamage <= 0) return;

        // 属性耐性によるダメージ軽減
        string resistLog;
        int damage = ApplyEnemyAttributeResistance(baseDamage, attr, out resistLog);

        // 防御ダイス
        int enemyDef = GetEnemyDefense(dmgCat);
        int enemyBlocked = RollDefenseDice(enemyDef);
        int finalDamage = damage - enemyBlocked;
        if (finalDamage < 1) finalDamage = 1;

        // 完全無効（耐性100以上）の場合は0ダメージ
        if (damage <= 0) finalDamage = 0;

        enemyCurrentHp -= finalDamage;
        if (enemyCurrentHp < 0) enemyCurrentHp = 0;

        // ログ出力
        string blockLog = enemyBlocked > 0 ? $"（防御{enemyBlocked}軽減）" : "";
        AddLogImmediate($"{itemName} が炸裂！（{attr.ToJapanese()}属性） " +
                        $"{finalDamage}ダメージ！{resistLog}{blockLog}");

        Debug.Log($"[Battle] BattleItem: base={baseDamage} attr={attr} " +
                  $"afterResist={damage} def={enemyDef} blocked={enemyBlocked} final={finalDamage}");
    }

    /// <summary>
    /// SkillEffectProcessor が返す追加効果ログの文言からSE種別を判定する。
    /// ※ Processor 側のログ文言を変更した場合は、ここのパターンも必ず更新すること。
    /// ※ 判定は上から順。「デバフが全て解除」（回復系）は「バフが全て解除」（妨害系）を
    ///   部分文字列として含むため、回復系の判定を必ず先に行う。
    /// </summary>
    private BattleSeKind ClassifyEffectLog(string log)
    {
        if (string.IsNullOrEmpty(log)) return BattleSeKind.None;

        // --- 失敗（耐性・重複・対象なしの共通文言） ---
        if (log.Contains("効果がなかった")) return BattleSeKind.Miss;

        // --- 回復系（「治った」は「石化」より先に判定） ---
        if (log.Contains("回復した！")) return BattleSeKind.Heal;
        if (log.Contains("が治った！")) return BattleSeKind.Heal;
        if (log.Contains("%上昇！")) return BattleSeKind.Heal;
        if (log.Contains("デバフが全て解除された！")) return BattleSeKind.Heal;

        // --- 敵対ディスペル（妨害） ---
        if (log.Contains("バフが全て解除された！")) return BattleSeKind.Ailment;
        if (log.Contains("バフ/デバフが解除された！")) return BattleSeKind.Ailment;

        // --- 状態異常・デバフ系 ---
        if (log.Contains("を受けた！")) return BattleSeKind.Ailment;
        if (log.Contains("は気絶した！")) return BattleSeKind.Ailment;
        if (log.Contains("%低下！")) return BattleSeKind.Ailment;
        if (log.Contains("怒りに燃えた！")) return BattleSeKind.Ailment;
        if (log.Contains("石化")) return BattleSeKind.Ailment;
        if (log.Contains("に下がった！")) return BattleSeKind.Ailment;          // レベルドレイン
        if (log.Contains("MPが") && log.Contains("減った！")) return BattleSeKind.Ailment;

        // --- それ以外は無音 ---
        // 反対効果の解除行（「…上昇が解除された！」等）、自爆「力尽きた！」、反動ダメージ
        return BattleSeKind.None;
    }
}