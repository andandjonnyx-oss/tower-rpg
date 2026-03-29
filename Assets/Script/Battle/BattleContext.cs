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

}