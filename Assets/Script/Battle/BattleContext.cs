using System.Collections.Generic;
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

    /// <summary>
    /// ボス戦でイベント勝利（餌付け等）したかどうか。
    /// true の場合、OnVictory で通常勝利とは異なる会話に分岐する。
    /// BattleSceneController が即勝利処理時にセットし、
    /// 会話遷移後にリセットされる。
    /// </summary>
    public static bool IsBossEventWin;

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

    // =========================================================
    // ボス第二形態 連戦フラグ（追加）
    // =========================================================

    /// <summary>
    /// 第一形態を倒した直後の連戦フラグ。
    /// true の場合、OnVictory でクリアフラグを立てずに
    /// 第二形態の戦闘を開始する。
    /// </summary>
    public static bool IsPhase2Transition;

    /// <summary>
    /// 第二形態のモンスターデータ。
    /// BossEncounterSystem が第一形態開始時にセットする。
    /// </summary>
    public static Monster Phase2Monster;

    // =========================================================
    // ボス戦コンティニュー用アイテムスナップショット（追加）
    // =========================================================

    /// <summary>
    /// ボス戦開始時の ItemBoxManager のスナップショット。
    /// コンティニュー時にアイテムを戦闘開始時の状態に復元するために使用。
    /// 各要素は (uid, itemId) のペア。
    /// 戦闘終了時（勝利/敗北帰還）に null にクリアする。
    /// </summary>
    public static List<ItemSnapshotEntry> ItemSnapshot;

    /// <summary>
    /// ボス戦開始時の装備武器uid。コンティニュー時に復元する。
    /// </summary>
    public static string SnapshotEquippedWeaponUid;
}

/// <summary>
/// アイテムスナップショットの1エントリ。
/// uid と itemId のペアを保持する。
/// </summary>
[System.Serializable]
public class ItemSnapshotEntry
{
    public string uid;
    public string itemId;

    public ItemSnapshotEntry(string uid, string itemId)
    {
        this.uid = uid;
        this.itemId = itemId;
    }
}