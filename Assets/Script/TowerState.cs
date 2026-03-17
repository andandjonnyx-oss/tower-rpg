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

    // 内部ステータス
    public int Floor { get; private set; } = 1;
    public int Step { get; private set; } = 1;


    private void Start()
    {
        var gs = GameState.I;
        if (gs == null) return;

        SyncFromGameState();
        RefreshUI();

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
        }


        var gs = GameState.I;
        if (gs == null)
        {
            Debug.LogError("GameState not found. Cannot trigger events.");
            RefreshUI();
            return;
        }

        gs.floor = Floor;
        gs.step = Step;

        RefreshUI();

        // ①会話優先（会話が始まったら以降を止める）
        bool talkStarted = TowerEventTrigger.Instance.TryTriggerTalkEvent();
        if (talkStarted) return;

        // ② アイテム
        bool itemStarted = TowerItemTrigger.Instance != null &&
                           TowerItemTrigger.Instance.TryTriggerItemEvent(Floor, Step);
        if (itemStarted) return;

        // ③エンカウント
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

    private void SyncFromGameState()
    {
        var gs = GameState.I;
        if (gs == null) return;

        Floor = gs.floor;
        Step = gs.step;
    }

}