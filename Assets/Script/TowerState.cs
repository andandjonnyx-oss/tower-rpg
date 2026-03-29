using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

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

}