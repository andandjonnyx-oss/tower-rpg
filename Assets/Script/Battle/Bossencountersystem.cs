using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// ボスエンカウントの管理システム。
/// 各階のボス配置情報を保持し、特定STEPに到達した時に
/// ボス戦を開始する。
///
/// 使い方:
///   1. Tower シーンの適当な GameObject にアタッチ
///   2. インスペクターで bossEntries にボス配置を登録
///   3. MonsterDatabase にボスモンスターを登録（IsBoss=true）
///   4. TowerState.Advance() から TryStartBossBattle() が呼ばれる
///
/// 撃破フラグ:
///   GameState.IsPlayed("BOSS_F{階}") で判定。
///   BattleSceneController.OnVictory() で MarkPlayed() する。
///   既存のイベント既読管理をそのまま流用する。
/// </summary>
public class BossEncounterSystem : MonoBehaviour
{
    public static BossEncounterSystem Instance { get; private set; }

    [Header("Boss Entries")]
    [Tooltip("ボスの配置リスト。階・STEP・対象モンスターを設定する。")]
    [SerializeField] private List<BossEntry> bossEntries = new List<BossEntry>();

    [Header("Scene Names")]
    [SerializeField] private string battleSceneName = "Battle";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // =========================================================
    // ボス撃破フラグID生成
    // =========================================================

    /// <summary>
    /// ボス撃破フラグのIDを生成する。
    /// 例: floor=3 → "BOSS_F03"
    /// </summary>
    public static string GetBossDefeatedId(int floor)
    {
        return $"BOSS_F{floor:D2}";
    }

    /// <summary>
    /// ボス勝利会話のイベントIDを生成する。
    /// 例: floor=3 → "BOSS_F03_VICTORY"
    /// </summary>
    public static string GetBossVictoryTalkId(int floor)
    {
        return $"BOSS_F{floor:D2}_VICTORY";
    }

    /// <summary>
    /// 指定階のボスが撃破済みかどうか。
    /// </summary>
    public static bool IsBossDefeated(int floor)
    {
        if (GameState.I == null) return false;
        return GameState.I.IsPlayed(GetBossDefeatedId(floor));
    }

    // =========================================================
    // ボスエンカウント判定
    // =========================================================

    /// <summary>
    /// 現在の階・STEPでボスと遭遇するか判定し、該当すれば戦闘を開始する。
    /// TowerState.Advance() から呼ばれる。
    ///
    /// 戻り値: true = ボス戦を開始した（以降の処理をスキップすべき）
    /// </summary>
    public bool TryStartBossBattle(int floor, int step)
    {
        // このSTEPにボスが配置されているか検索
        BossEntry entry = FindBossEntry(floor, step);
        if (entry == null) return false;

        // 撃破済みなら何もしない
        if (IsBossDefeated(floor))
        {
            Debug.Log($"[BossEncounter] ボス撃破済み (floor={floor})。通常進行。");
            return false;
        }

        // ボスモンスターが設定されているか確認
        if (entry.bossMonster == null)
        {
            Debug.LogError($"[BossEncounter] ボスモンスターが未設定 (floor={floor}, step={step})");
            return false;
        }

        // =========================================================
        // 第二形態対応: フェーズに応じて出すモンスターを切り替え
        // =========================================================
        Monster targetMonster = entry.bossMonster;

        if (entry.phase2Monster != null && !string.IsNullOrEmpty(entry.phase2StateField))
        {
            int currentPhase = GetBossPhase(entry.phase2StateField);
            if (currentPhase >= 1)
            {
                // 第一形態撃破済み → 第二形態から開始
                targetMonster = entry.phase2Monster;
                Debug.Log($"[BossEncounter] 第二形態から開始 (floor={floor}, phase={currentPhase})");
            }
            else
            {
                // 第一形態から開始 → 第二形態のモンスターをコンテキストに保持
                BattleContext.Phase2Monster = entry.phase2Monster;
                BattleContext.IsPhase2Transition = false;
                Debug.Log($"[BossEncounter] 第一形態から開始 (floor={floor})");
            }
        }

        // ボス戦開始
        Debug.Log($"[BossEncounter] ボス戦開始！ {targetMonster.Mname} (floor={floor}, step={step})");

        BattleContext.EnemyMonster = targetMonster;
        BattleContext.IsBossBattle = true;
        BattleContext.BossFloor = floor;

        SceneManager.LoadScene(battleSceneName, LoadSceneMode.Single);
        return true;
    }

    /// <summary>
    /// GameState からボスフェーズの値を取得する。
    /// フィールド名でリフレクションする。
    /// </summary>
    private int GetBossPhase(string fieldName)
    {
        if (GameState.I == null) return 0;
        var field = typeof(GameState).GetField(fieldName);
        if (field == null)
        {
            Debug.LogError($"[BossEncounter] GameState に {fieldName} フィールドが見つかりません");
            return 0;
        }
        return (int)field.GetValue(GameState.I);
    }

    /// <summary>
    /// GameState のボスフェーズの値を設定する。
    /// </summary>
    public static void SetBossPhase(string fieldName, int value)
    {
        if (GameState.I == null) return;
        var field = typeof(GameState).GetField(fieldName);
        if (field == null)
        {
            Debug.LogError($"[BossEncounter] GameState に {fieldName} フィールドが見つかりません");
            return;
        }
        field.SetValue(GameState.I, value);
    }

    /// <summary>
    /// 指定階・STEPに配置されたボスエントリを検索する。
    /// </summary>
    private BossEntry FindBossEntry(int floor, int step)
    {
        for (int i = 0; i < bossEntries.Count; i++)
        {
            if (bossEntries[i] != null &&
                bossEntries[i].floor == floor &&
                bossEntries[i].step == step)
            {
                return bossEntries[i];
            }
        }
        return null;
    }
}

// =========================================================
// ボス配置データ
// =========================================================

/// <summary>
/// ボス1体分の配置情報。
/// BossEncounterSystem のインスペクターで設定する。
/// </summary>
[Serializable]
public class BossEntry
{
    [Tooltip("ボスが出現する階")]
    public int floor;

    [Tooltip("ボスが出現するSTEP")]
    public int step;

    [Tooltip("ボスモンスターの ScriptableObject")]
    public Monster bossMonster;

    [Tooltip("ボス勝利後の会話イベント（任意）。\n" +
             "設定すると勝利後に Talk シーンへ遷移する。\n" +
             "未設定なら勝利後は直接 Tower に戻る。")]
    public TalkEvent victoryTalkEvent;

    [Tooltip("第二形態のモンスター。設定すると第一形態撃破後に連戦で第二形態が開始される。")]
    public Monster phase2Monster;

    [Tooltip("第二形態のフェーズ管理用GameStateフィールド名。\n" +
             "空の場合は第二形態なし（従来動作）。")]
    public string phase2StateField;

}