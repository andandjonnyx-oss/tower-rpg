using System.Collections.Generic;
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

        // =========================================================
        // 確率分岐グループの収集
        // =========================================================
        // 同じ randomGroup を持つイベントをグループ化し、
        // グループ単位で重み付き抽選を行う。
        // グループに属さないイベント（randomGroup が空）は従来通り順番に処理。

        // グループ名 → そのグループの未再生イベントリスト
        var groups = new Dictionary<string, List<TalkEvent>>();
        // グループに属さない通常イベント
        var normalEvents = new List<TalkEvent>();

        foreach (var e in list)
        {
            if (e == null) continue;
            if (string.IsNullOrEmpty(e.id)) continue;
            if (gs.IsPlayed(e.id)) continue;
            if (!AreAllConditionsMet(e, gs)) continue;

            if (!string.IsNullOrEmpty(e.randomGroup))
            {
                // グループイベント
                if (!groups.TryGetValue(e.randomGroup, out var groupList))
                {
                    groupList = new List<TalkEvent>();
                    groups.Add(e.randomGroup, groupList);
                }
                groupList.Add(e);
            }
            else
            {
                // 通常イベント（従来互換）
                normalEvents.Add(e);
            }
        }

        // =========================================================
        // グループ抽選（確率分岐イベントを優先処理）
        // =========================================================
        foreach (var kvp in groups)
        {
            var groupList = kvp.Value;
            if (groupList.Count == 0) continue;

            // 重み付き抽選
            TalkEvent winner = WeightedRandom(groupList);
            if (winner == null) continue;

            Debug.Log($"[RandomGroup:{kvp.Key}] Winner: {winner.id} (weight={winner.randomWeight})");

            // 当選イベントを再生済みにし、排他IDもまとめて MarkPlayed
            gs.pendingEventId = winner.id;
            MarkExclusiveIds(gs, winner);
            SceneManager.LoadScene(talkSceneName);
            return true;
        }

        // =========================================================
        // 通常イベント（従来互換）
        // =========================================================
        foreach (var e in normalEvents)
        {
            //未再生のイベントのIDを記録し、シーン遷移（Talk）
            gs.pendingEventId = e.id;
            SceneManager.LoadScene(talkSceneName);
            return true;
        }

        return false;
    }

    // =========================================================
    // 重み付き抽選
    // =========================================================
    /// <summary>
    /// グループ内のイベントから randomWeight に基づいて1つを抽選する。
    /// 累積確率方式で正確な確率配分を実現する。
    /// </summary>
    private TalkEvent WeightedRandom(List<TalkEvent> candidates)
    {
        float totalWeight = 0f;
        foreach (var e in candidates)
        {
            totalWeight += Mathf.Max(0f, e.randomWeight);
        }

        // 全重み0の場合はランダムに1つ（フォールバック）
        if (totalWeight <= 0f)
        {
            return candidates[Random.Range(0, candidates.Count)];
        }

        float roll = Random.Range(0f, totalWeight);
        float cumulative = 0f;

        foreach (var e in candidates)
        {
            cumulative += Mathf.Max(0f, e.randomWeight);
            if (roll < cumulative)
            {
                return e;
            }
        }

        // 浮動小数点の丸め誤差対策（最後の要素を返す）
        return candidates[candidates.Count - 1];
    }

    // =========================================================
    // 排他ID の MarkPlayed
    // =========================================================
    /// <summary>
    /// 当選イベントの exclusiveIds に列挙されたIDをまとめて MarkPlayed する。
    /// これにより、同グループの他の分岐イベントが今後発生しなくなる。
    /// </summary>
    private void MarkExclusiveIds(GameState gs, TalkEvent winner)
    {
        if (winner.exclusiveIds == null) return;

        foreach (var exId in winner.exclusiveIds)
        {
            if (!string.IsNullOrEmpty(exId) && !gs.IsPlayed(exId))
            {
                gs.MarkPlayed(exId);
                Debug.Log($"[RandomGroup] Exclusive MarkPlayed: {exId}");
            }
        }
    }
}