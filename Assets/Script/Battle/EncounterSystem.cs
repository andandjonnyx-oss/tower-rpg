using UnityEngine;
using UnityEngine.SceneManagement;

public class EncounterSystem : MonoBehaviour
{
    public static EncounterSystem Instance { get; private set; }

    [SerializeField] private MonsterDatabase monsterDatabase;

    [Header("Encounter")]
    [Range(0f, 1f)] public float encounterRate = 0.20f;

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
}