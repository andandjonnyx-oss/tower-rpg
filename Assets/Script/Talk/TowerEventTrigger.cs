using UnityEngine;
using UnityEngine.SceneManagement;

public class TowerEventTrigger : MonoBehaviour
{
    // 同じシーン内の他スクリプトから
    // TowerEventTrigger.Instance で参照できるようにする
    // 読み取りは外部から可能 (get)
    // 代入はこのクラス内だけ (private set)
    public static TowerEventTrigger Instance { get; private set; }

    [SerializeField] private TalkEventDatabase database;
    [SerializeField] private string talkSceneName = "Talk";

    // シーンに存在するこのTowerEventTriggerを
    // static Instanceとして登録する
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private bool AreAllConditionsMet(TalkEvent e, GameState gs)
    {
        if (e.conditions == null || e.conditions.Count == 0) return true;

        foreach (var c in e.conditions)
        {
            if (c == null) continue; // 未設定条件は無視（好みでfalseにしてもOK）
            if (!c.Evaluate(gs)) return false;
        }
        return true;
    }
    public bool TryTriggerTalkEvent()
    {

        //ゲーム進行状態にデータベースが設定されているか確認
        var gs = GameState.I;
        if (gs == null || database == null) return false;

        //デバッグ用。今の階層とステップをコンソール出力
        Debug.Log($"EventCheck: floor={GameState.I.floor} step={GameState.I.step}");

        //今の階層とステップに対応したイベントを一覧を取得
        var list = database.FindByCondition(gs.floor, gs.step);
        if (list == null || list.Count == 0) return false;

        //デバッグ用。イベントの個数をコンソール出力
        Debug.Log("Hit Event Count: " + list.Count);

        foreach (var e in list)
        {
            //NULL、ID無し、再生済みの場合は次へ
            if (e == null) continue;
            if (string.IsNullOrEmpty(e.id)) continue;
            if (gs.IsPlayed(e.id)) continue;

            //テスト用。フラグの追加
            if (!AreAllConditionsMet(e, gs)) continue;


            //未再生のイベントのIDを記録し、シーン遷移（Talk）
            gs.pendingEventId = e.id;
            SceneManager.LoadScene(talkSceneName);
            return true;
        }

        return false;
    }
}