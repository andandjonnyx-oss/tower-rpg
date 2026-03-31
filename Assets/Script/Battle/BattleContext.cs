using UnityEngine;

//受け渡し用の箱
public static class BattleContext
{
    // 選ばれた敵
    public static Monster EnemyMonster;

    //現在地。将来的に
    public static int Floor;
    public static int Step;

    // =========================================================
    // ボス戦フラグ（追加）
    // =========================================================

    /// <summary>
    /// 現在の戦闘がボス戦かどうか。
    /// BossEncounterSystem が true にセットし、
    /// BattleSceneController が勝利/敗北処理で参照してリセットする。
    /// </summary>
    public static bool IsBossBattle;

    /// <summary>
    /// ボスが配置されている階。勝利時の撃破フラグ生成に使う。
    /// </summary>
    public static int BossFloor;

    // =========================================================
    // デバッグ戦闘フラグ（追加）
    // =========================================================

    /// <summary>
    /// デバッグシーンから開始した戦闘かどうか。
    /// true の場合、勝利/敗北後に Tower ではなく DebugReturnScene に戻る。
    /// </summary>
    public static bool IsDebugBattle;

    /// <summary>
    /// デバッグ戦闘終了後に戻るシーン名。
    /// IsDebugBattle == true の時のみ使用。
    /// </summary>
    public static string DebugReturnScene = "Debug";

}