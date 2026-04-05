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
    [Header("UI - Status Effect (Optional)")]
    [Tooltip("毒状態時にメッセージを表示するテキスト。未設定なら表示しない。")]
    [SerializeField] private TextMeshProUGUI statusEffectText;

    // =========================================================
    // 非バトル魔法 UI（追加）
    // =========================================================
    [Header("UI - Field Magic")]
    [Tooltip("塔シーン用の魔法選択ドロップダウン。\n"
           + "noBattleOk == true の魔法スキルのみ表示される。\n"
           + "未設定の場合は魔法UIを表示しない。")]
    [SerializeField] private TMP_Dropdown magicDropdown;

    [Tooltip("魔法実行ボタン。")]
    [SerializeField] private Button magicButton;

    [Tooltip("魔法使用時のログを表示するテキスト（任意）。\n"
           + "未設定の場合はログ表示なし。")]
    [SerializeField] private TextMeshProUGUI magicLogText;

    /// <summary>ドロップダウンに表示中のスキルリストキャッシュ。</summary>
    private List<SkillData> fieldMagicList = new List<SkillData>();

    // 内部ステータス
    public int Floor { get; private set; } = 1;
    public int Step { get; private set; } = 1;


    private void Start()
    {
        var gs = GameState.I;
        if (gs == null) return;

        SyncFromGameState();
        RefreshUI();
        RefreshStatusEffectUI(); // 追加

        // 魔法ボタン登録
        if (magicButton != null) magicButton.onClick.AddListener(OnFieldMagicClicked);
        RefreshFieldMagicUI();
    }

    // 進むボタンから呼ぶ
    public void Advance()
    {

        if (TowerItemTrigger.Instance != null && TowerItemTrigger.Instance.IsBusy)
            return;

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
        // 毒ダメージ処理（追加）
        // 1歩進むごとに最大HPの3%ダメージ、10%で自然治癒
        // =========================================================
        if (gs2.isPoisoned)
        {
            int poisonDmg;
            bool cured;
            StatusEffectSystem.ApplyTowerPoisonToPlayer(out poisonDmg, out cured);

            if (cured)
            {
                Debug.Log($"[Tower] 毒が自然治癒した！ (最後のダメージ: {poisonDmg})");
            }
            else
            {
                Debug.Log($"[Tower] 毒ダメージ: {poisonDmg} (HP: {gs2.currentHp}/{gs2.maxHp})");
            }

            RefreshStatusEffectUI(); // 追加
            SaveManager.Save(); // 毒ダメージ後にセーブ
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
    /// 状態異常 UI の更新（追加）。
    /// statusEffectText が設定されている場合のみ表示する。
    /// </summary>
    private void RefreshStatusEffectUI()
    {
        if (statusEffectText == null) return;

        if (GameState.I != null && GameState.I.isPoisoned)
        {
            statusEffectText.text = "【毒】";
            statusEffectText.color = new Color(0.5f, 0f, 0.8f); // 紫色
        }
        else
        {
            statusEffectText.text = "";
        }
    }

    private void SyncFromGameState()
    {
        var gs = GameState.I;
        if (gs == null) return;

        Floor = gs.floor;
        Step = gs.step;
    }

    // =========================================================
    // 非バトル魔法（追加）
    // =========================================================
    //
    // 塔シーンで noBattleOk == true の魔法を使用できる。
    // MP を消費し、追加効果（回復・状態異常回復等）を実行する。
    // ダメージ系効果は対象がいないため自然にスキップされる。
    // 使用しても STEP は進まない（その場で即時実行）。
    // =========================================================

    /// <summary>
    /// 非バトル魔法のドロップダウンとボタンを更新する。
    /// noBattleOk == true のスキルがなければ UI を非表示にする。
    /// </summary>
    private void RefreshFieldMagicUI()
    {
        fieldMagicList = PassiveCalculator.CollectNoBattleMagicSkills();

        if (fieldMagicList.Count == 0)
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
            for (int i = 0; i < fieldMagicList.Count; i++)
            {
                options.Add($"{fieldMagicList[i].skillName} (MP:{fieldMagicList[i].mpCost})");
            }
            magicDropdown.AddOptions(options);
            magicDropdown.value = 0;
            magicDropdown.RefreshShownValue();
        }

        if (magicButton != null)
        {
            magicButton.gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// 魔法ボタンが押された時の処理。
    /// ドロップダウンで選択中のスキルを MP 消費して実行する。
    /// </summary>
    private void OnFieldMagicClicked()
    {
        if (magicDropdown == null) return;
        if (fieldMagicList == null || fieldMagicList.Count == 0) return;

        int index = magicDropdown.value;
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

        // 即時セーブ
        SaveManager.Save();
    }

    /// <summary>
    /// 非バトル魔法のログを表示する。
    /// magicLogText が設定されていなければ Debug.Log のみ。
    /// </summary>
    private void ShowFieldMagicLog(string message)
    {
        if (magicLogText != null)
        {
            magicLogText.text = message;
        }
    }

}