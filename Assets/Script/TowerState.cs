using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

public class TowerState : MonoBehaviour
{

    [Header("Config")]
    [SerializeField] private int maxStepPerFloor = 20;

    [Header("UI (TextMeshProUGUI)")]
    [SerializeField] private TextMeshProUGUI floorText; // 「1階」
    [SerializeField] private TextMeshProUGUI stepText;  // 「1STEP」

    // =========================================================
    // 状態異常表示用 UI（追加）
    // =========================================================

    [Header("UI - Status Effect Lamp")]
    [Tooltip("味方の状態異常ランプ（joutaiijoujoumikata にアタッチ）")]
    [SerializeField] private StatusEffectLamp playerStatusLamp;

    [Header("UI - Blind Overlay")]
    [Tooltip("暗闇時に背景を覆う黒い Image。\n"
           + "背景画像の上、ボタン類の下に配置する。\n"
           + "初期状態は非表示（SetActive(false)）にしておくこと。\n"
           + "未設定の場合は暗闇の背景暗転を行わない。")]
    [SerializeField] private GameObject blindOverlay;

    [Header("UI - Paralyze Blocker")]
    [Tooltip("麻痺待機中に全タッチ操作をブロックする透明パネル。\n"
       + "Canvas の最前面に配置し、Raycast Target = true にする。\n"
       + "初期状態は非表示にしておくこと。\n"
       + "未設定の場合は進むボタンのみ無効化する。")]
    [SerializeField] private GameObject paralyzeBlocker;


    // =========================================================
    // 非バトル魔法 UI（MagicSelector 版に変更）
    // =========================================================
    [Header("UI - Field Magic")]
    [Tooltip("塔シーン用の魔法選択ドロップダウン（自作 MagicSelector）。\n"
           + "noBattleOk == true の魔法スキルのみ表示される。\n"
           + "未設定の場合は魔法UIを表示しない。")]
    [SerializeField] private MagicSelector magicSelector;

    [Tooltip("魔法実行ボタン。")]
    [SerializeField] private Button magicButton;

    [Tooltip("魔法使用時のログを表示するテキスト（任意）。\n"
           + "未設定の場合はログ表示なし。")]
    [SerializeField] private TextMeshProUGUI magicLogText;

    // =========================================================
    // 進むボタン参照（Phase3 追加 — 麻痺の待機制御用）
    // =========================================================
    [Header("UI - Advance Button")]
    [Tooltip("進むボタン。麻痺時に一時無効化するために参照する。\n"
           + "未設定の場合は麻痺の待機制御を行わない。")]
    [SerializeField] private Button advanceButton;

    /// <summary>ドロップダウンに表示中のスキルリストキャッシュ。</summary>
    private List<SkillData> fieldMagicList = new List<SkillData>();

    // 内部ステータス
    public int Floor { get; private set; } = 1;
    public int Step { get; private set; } = 1;

    /// <summary>麻痺待機中かどうか。二重実行防止用。</summary>
    private bool isParalyzeWaiting = false;

    private void Start()
    {
        var gs = GameState.I;
        if (gs == null) return;

        SyncFromGameState();
        RefreshUI();
        RefreshStatusEffectUI(); // 追加
        RefreshBlindOverlay();   // Phase3: 暗闇オーバーレイ初期化

        // 魔法ボタン登録
        if (magicButton != null) magicButton.onClick.AddListener(OnFieldMagicClicked);
        RefreshFieldMagicUI();
    }

    // 進むボタンから呼ぶ
    public void Advance()
    {

        if (TowerItemTrigger.Instance != null && TowerItemTrigger.Instance.IsBusy)
            return;

        // =========================================================
        // 麻痺チェック（Phase3 追加）
        // 麻痺中は1秒の待機時間を入れてから処理を実行する。
        // 待機中はボタンを無効化して連打を防ぐ。
        // =========================================================
        if (GameState.I != null && GameState.I.isParalyzed && !isParalyzeWaiting)
        {
            StartCoroutine(ParalyzeAdvanceCoroutine());
            return;
        }

        AdvanceInternal();
    }

    /// <summary>
    /// 麻痺時の遅延移動コルーチン。
    /// 1秒待機してから AdvanceInternal() を実行する。
    /// </summary>
    private IEnumerator ParalyzeAdvanceCoroutine()
    {
        isParalyzeWaiting = true;

        // 全操作をブロック
        if (paralyzeBlocker != null) paralyzeBlocker.SetActive(true);
        if (advanceButton != null) advanceButton.interactable = false;

        Debug.Log("[Tower] 麻痺: 1秒待機中…");
        yield return new WaitForSeconds(1.0f);

        isParalyzeWaiting = false;

        // ブロック解除
        if (paralyzeBlocker != null) paralyzeBlocker.SetActive(false);
        if (advanceButton != null) advanceButton.interactable = true;

        AdvanceInternal();
    }

    /// <summary>
    /// 実際の移動処理。Advance() から直接、または麻痺コルーチン経由で呼ばれる。
    /// </summary>
    private void AdvanceInternal()
    {
        Step++;

        if (Step > maxStepPerFloor)
        {
            Floor++;
            Step = 1;

            // ★ 新しい階に到達したら到達フラグを更新
            var gs = GameState.I;
            if (gs != null)
                gs.UpdateReachedFloor(Floor);
        }


        var gs2 = GameState.I;
        if (gs2 == null)
        {
            Debug.LogError("GameState not found. Cannot trigger events.");
            RefreshUI();
            return;
        }

        gs2.floor = Floor;
        gs2.step = Step;

        RefreshUI();

        // =========================================================
        // 状態異常ステップ処理（Phase3: ApplyTowerStepEffects に統合）
        // 毒ダメージ + 全状態異常の自然治癒を一括処理する。
        // =========================================================
        List<string> stepLogs;
        StatusEffectSystem.ApplyTowerStepEffects(out stepLogs);

        if (stepLogs.Count > 0)
        {
            for (int i = 0; i < stepLogs.Count; i++)
            {
                Debug.Log($"[Tower] {stepLogs[i]}");
            }

            RefreshStatusEffectUI();
            RefreshBlindOverlay(); // Phase3: 暗闇治癒の反映
            SaveManager.Save(); // 状態異常変化後にセーブ
        }

        // 階層進行を即時セーブ
        SaveManager.Save();

        // ①会話優先（会話が始まったら以降を止める）
        bool talkStarted = TowerEventTrigger.Instance.TryTriggerTalkEvent();
        if (talkStarted) return;

        // =========================================================
        // ②ボスエンカウント判定（追加）
        // 会話イベントの後、アイテム・通常エンカウントの前に判定する。
        // 19STEPの会話イベント（①で処理済み）→ 20STEPのボス戦（ここ）
        // という流れになる。
        // =========================================================
        if (BossEncounterSystem.Instance != null)
        {
            bool bossStarted = BossEncounterSystem.Instance.TryStartBossBattle(Floor, Step);
            if (bossStarted) return;
        }

        // ③ アイテム
        bool itemStarted = TowerItemTrigger.Instance != null &&
                           TowerItemTrigger.Instance.TryTriggerItemEvent(Floor, Step);
        if (itemStarted) return;

        // ④エンカウント
        if (EncounterSystem.Instance != null)
            EncounterSystem.Instance.TryStartEncounter(Floor, Step);
        else
            Debug.LogError("EncounterSystem is not assigned.");

    }

    private void RefreshUI()
    {
        if (floorText != null) floorText.text = $"{Floor}階";
        if (stepText != null) stepText.text = $"{Step}STEP";
    }

    /// <summary>
    /// 状態異常 UI の更新（Phase3 拡張: 毒・麻痺・暗闇 複数表示対応）。
    /// statusEffectText が設定されている場合のみ表示する。
    /// </summary>
    private void RefreshStatusEffectUI()
    {
        // --- ランプ方式 ---
        if (playerStatusLamp != null && GameState.I != null)
        {
            playerStatusLamp.SetAll(
                GameState.I.isPoisoned,
                GameState.I.isParalyzed,
                GameState.I.isBlind,
                false  // Tower では怒り状態は発生しない
            );
        }
    }

    /// <summary>
    /// 暗闇オーバーレイの表示/非表示を更新する（Phase3 追加）。
    /// blindOverlay が未設定なら何もしない。
    /// </summary>
    private void RefreshBlindOverlay()
    {
        if (blindOverlay == null) return;

        bool isBlind = (GameState.I != null && GameState.I.isBlind);
        blindOverlay.SetActive(isBlind);
    }

    private void SyncFromGameState()
    {
        var gs = GameState.I;
        if (gs == null) return;

        Floor = gs.floor;
        Step = gs.step;
    }

    // =========================================================
    // 非バトル魔法（MagicSelector 版に変更）
    // =========================================================
    //
    // 塔シーンで noBattleOk == true の魔法を使用できる。
    // MP を消費し、追加効果（回復・状態異常回復等）を実行する。
    // ダメージ系効果は対象がいないため自然にスキップされる。
    // 使用しても STEP は進まない（その場で即時実行）。
    // =========================================================

    /// <summary>
    /// 非バトル魔法の MagicSelector とボタンを更新する。
    /// noBattleOk == true のスキルがなければ UI を非表示にする。
    /// </summary>
    private void RefreshFieldMagicUI()
    {
        fieldMagicList = PassiveCalculator.CollectNoBattleMagicSkills();

        if (fieldMagicList.Count == 0)
        {
            if (magicSelector != null) magicSelector.SetVisible(false);
            if (magicButton != null) magicButton.gameObject.SetActive(false);
            return;
        }

        if (magicSelector != null)
        {
            magicSelector.SetVisible(true);
            var optionLabels = new List<string>();
            for (int i = 0; i < fieldMagicList.Count; i++)
            {
                optionLabels.Add($"{fieldMagicList[i].skillName} (MP:{fieldMagicList[i].mpCost})");
            }
            magicSelector.SetOptions(optionLabels);
        }

        if (magicButton != null)
        {
            magicButton.gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// 魔法ボタンが押された時の処理。
    /// MagicSelector で選択中のスキルを MP 消費して実行する。
    /// </summary>
    private void OnFieldMagicClicked()
    {
        if (magicSelector == null) return;
        if (fieldMagicList == null || fieldMagicList.Count == 0) return;

        int index = magicSelector.Value;
        if (index < 0 || index >= fieldMagicList.Count) return;

        SkillData magic = fieldMagicList[index];
        if (magic == null) return;

        // MP チェック
        if (GameState.I == null) return;
        if (GameState.I.currentMp < magic.mpCost)
        {
            ShowFieldMagicLog($"MPが足りない！（必要:{magic.mpCost} 現在:{GameState.I.currentMp}）");
            return;
        }

        // MP 消費
        GameState.I.currentMp -= magic.mpCost;

        // 追加効果の実行（敵なし = null で呼ぶ）
        string resultLog = $"{magic.skillName} を唱えた！ MP-{magic.mpCost}";

        if (magic.HasAdditionalEffects)
        {
            bool dummyPoisoned = false;
            bool dummyStunned = false;
            int dummyHp = 0;

            var logs = SkillEffectProcessor.ProcessEffects(
                magic.additionalEffects,
                isPlayerAttack: true,
                null, // enemyMonster = null（非バトル）
                ref dummyPoisoned,
                ref dummyStunned,
                ref dummyHp);

            for (int i = 0; i < logs.Count; i++)
            {
                resultLog += $"\n{logs[i]}";
            }
        }

        ShowFieldMagicLog(resultLog);
        Debug.Log($"[Tower] 非バトル魔法使用: {resultLog}");

        // 状態異常UIの更新（毒消し等の反映）
        RefreshStatusEffectUI();
        RefreshBlindOverlay(); // Phase3: 暗闇治癒の反映

        // 即時セーブ
        SaveManager.Save();
    }

    /// <summary>
    /// 非バトル魔法のログを表示する。
    /// magicLogText が設定されていなければ Debug.Log のみ。
    /// </summary>
    /// <summary>ログ自動消去用コルーチン参照。</summary>
    private Coroutine magicLogHideCoroutine;

    private void ShowFieldMagicLog(string message)
    {
        if (magicLogText != null)
        {
            // 前回のタイマーが残っていれば停止
            if (magicLogHideCoroutine != null)
                StopCoroutine(magicLogHideCoroutine);

            magicLogText.text = message;
            magicLogHideCoroutine = StartCoroutine(HideMagicLogAfterDelay(1.0f));
        }
    }

    /// <summary>
    /// 指定秒数後に magicLogText を空にする。
    /// </summary>
    private IEnumerator HideMagicLogAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (magicLogText != null)
            magicLogText.text = "";
        magicLogHideCoroutine = null;
    }

    /// <summary>
    /// 外部から魔法UI（MagicSelector + ボタン）を再描画させるための公開メソッド。
    /// アイテム入手時など、所持品が変わったタイミングで呼ぶ。
    /// </summary>
    public void RefreshFieldMagicFromExternal()
    {
        RefreshFieldMagicUI();
    }

}