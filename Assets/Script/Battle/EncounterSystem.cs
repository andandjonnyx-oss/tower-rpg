using UnityEngine;
using UnityEngine.SceneManagement;

public class EncounterSystem : MonoBehaviour
{
    public static EncounterSystem Instance { get; private set; }

    [SerializeField] private MonsterDatabase monsterDatabase;

    [Header("Encounter")]
    [Tooltip("通常時のエンカウント率。アイテム判定をすり抜けた残りに対して判定するため、\n" +
             "実質エンカウント率は概ね 0.8×この値 になる。実質20%にしたい場合は 0.25。")]
    [Range(0f, 1f)] public float encounterRate = 0.25f; // ★0.20→0.25

    [Tooltip("ノーアイテムモード時のエンカウント率。アイテム判定がスキップされ独立判定になるため、\n" +
         "実質エンカウント率はこの値そのものになる。")]
    [Range(0f, 1f)] public float encounterRateNoItem = 0.20f; // ★追加

    [Header("Scene Names")]
    public string battleSceneName = "Battle";
    public string towerSceneName = "Tower"; // あなたの塔シーン名に合わせて変更

    private void Awake()
    {

        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

    }



    /// <summary>
    /// Step進行直後に呼ぶ。
    /// </summary>
    /// <param name="floor">現在の階</param>
    /// <param name="step">現在のStep(1..20など)</param>
    /// <param name="talkEventHappenedThisStep">このStepで会話イベントが発生したか</param>
    public void TryStartEncounter(int floor, int step)
    {

        // STEP1は無効
        if (step == 1) return;

        // 会話イベントが出たStepは無効 現在はTowerState側で実装
        //if (talkEventHappenedThisStep) return;

        // 20%判定
        float roll = Random.value;
        if (roll > encounterRate) return;

        // 出現する敵からピックアップ
        Monster picked = monsterDatabase.GetRandomCandidate(floor, step);
        if (picked == null) return;

        // Battleへ渡す
        BattleContext.EnemyMonster = picked;

        // Battleシーンへ
        SceneManager.LoadScene(battleSceneName, LoadSceneMode.Single);

        Debug.Log("[Encounter] START BATTLE!");
    }

    /// <param name="noItemMode">アイテムが出ないモードか（true なら独立した20%判定）</param>
    public void TryStartEncounter(int floor, int step, bool noItemMode = false)
    {
        // STEP1は無効
        if (step == 1) return;

        float rate = noItemMode ? encounterRateNoItem : encounterRate; // ★モードで切替

        float roll = Random.value;
        if (roll > rate) return;

        Monster picked = monsterDatabase.GetRandomCandidate(floor, step);
        if (picked == null) return;

        BattleContext.EnemyMonster = picked;
        SceneManager.LoadScene(battleSceneName, LoadSceneMode.Single);

        Debug.Log($"[Encounter] START BATTLE! (rate={rate}, noItemMode={noItemMode})");
    }
}