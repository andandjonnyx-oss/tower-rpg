using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// BattleSceneController のバフ/デバフ管理パート（partial class）。
/// 5種×プレイヤー/敵のバフ/デバフ状態を BattleBuffState 構造体で一元管理する。
///
/// 【責務】
///   - バフ/デバフフィールドの宣言
///   - 初期化 / リセット
///   - ターンカウントダウン（TickBuffDebuffTurns）
///   - ランプUI更新（RefreshBuffDebuffLamps）
///
/// 既存の playerDefDebuffTurn 等の個別フィールドを置き換える。
/// </summary>
public partial class BattleSceneController
{
    // =========================================================
    // バフ/デバフ統合フィールド（Phase4: 構造体ベース）
    // =========================================================

    /// <summary>
    /// プレイヤーと敵の全バフ/デバフ状態。
    /// 戦闘限定（セーブ対象外）。戦闘終了で自動リセット。
    /// </summary>
    private static BattleBuffState buffState = new BattleBuffState();

    // =========================================================
    // 初期化 / リセット
    // =========================================================

    /// <summary>
    /// バフ/デバフフィールドを初期化する。戦闘開始時に呼ぶ。
    /// </summary>
    public static void InitBuffDebuffFields()
    {
        buffState = new BattleBuffState();
    }

    /// <summary>
    /// バフ/デバフフィールドをリセットする。戦闘終了時に呼ぶ。
    /// </summary>
    public static void ResetBuffDebuffFields()
    {
        buffState = new BattleBuffState();
    }

    // =========================================================
    // ターンカウントダウン
    // =========================================================

    /// <summary>
    /// 全バフ/デバフのターンカウントダウンを行い、解除ログを返す。
    /// AfterEnemyAction() から呼ばれる。
    /// </summary>
    public List<string> TickBuffDebuffTurns()
    {
        var logs = new List<string>();
        string eName = (enemyMonster != null) ? enemyMonster.Mname : "敵";

        // プレイヤー側
        TickOnePair(ref buffState.player.def, "You", "防御", logs);
        TickOnePair(ref buffState.player.atk, "You", "攻撃", logs);
        TickOnePair(ref buffState.player.matk, "You", "魔攻", logs);
        TickOnePair(ref buffState.player.mdef, "You", "魔防", logs);
        TickOnePair(ref buffState.player.luc, "You", "運", logs);

        // 敵側
        TickOnePair(ref buffState.enemy.def, eName, "防御", logs);
        TickOnePair(ref buffState.enemy.atk, eName, "攻撃", logs);
        TickOnePair(ref buffState.enemy.matk, eName, "回避", logs); // 敵の魔攻=回避
        TickOnePair(ref buffState.enemy.mdef, eName, "魔防", logs);
        TickOnePair(ref buffState.enemy.luc, eName, "運", logs);

        return logs;
    }

    /// <summary>
    /// 1ペア分のターンカウントダウン。
    /// </summary>
    private void TickOnePair(ref BuffDebuffPair pair, string ownerName, string statName, List<string> logs)
    {
        if (pair.debuffTurn > 0)
        {
            pair.debuffTurn--;
            if (pair.debuffTurn <= 0)
            {
                pair.debuffRate = 0f;
                logs.Add($"{ownerName} の{statName}ダウンが解除された。");
            }
        }
        if (pair.buffTurn > 0)
        {
            pair.buffTurn--;
            if (pair.buffTurn <= 0)
            {
                pair.buffRate = 0f;
                logs.Add($"{ownerName} の{statName}アップが解除された。");
            }
        }
    }

    // =========================================================
    // ランプUI更新
    // =========================================================

    /// <summary>
    /// バフ/デバフランプのUI表示を更新する。
    /// RefreshBattleStatusEffectUI() から呼ばれる。
    /// Phase C: 16引数版 SetAll に変更し、石化ランプも反映する。
    /// </summary>
    public void RefreshBuffDebuffLamps()
    {
        if (playerStatusLamp != null && GameState.I != null)
        {
            playerStatusLamp.SetAll(
                GameState.I.isPoisoned,
                GameState.I.isParalyzed,
                GameState.I.isBlind,
                playerRageTurn > 0,
                GameState.I.isSilenced,
                GameState.I.isPetrified,
                buffState.player.def.IsDebuffed,
                buffState.player.def.IsBuffed,
                buffState.player.atk.IsDebuffed,
                buffState.player.atk.IsBuffed,
                buffState.player.matk.IsDebuffed,
                buffState.player.matk.IsBuffed,
                buffState.player.mdef.IsDebuffed,
                buffState.player.mdef.IsBuffed,
                buffState.player.luc.IsDebuffed,
                buffState.player.luc.IsBuffed
            );
        }

        if (enemyStatusLamp != null)
        {
            enemyStatusLamp.SetAll(
                enemyIsPoisoned,
                enemyIsParalyzed,
                enemyIsBlind,
                enemyRageTurn > 0,
                enemyIsSilenced,
                EnemyIsPetrified,
                buffState.enemy.def.IsDebuffed,
                buffState.enemy.def.IsBuffed,
                buffState.enemy.atk.IsDebuffed,
                buffState.enemy.atk.IsBuffed,
                buffState.enemy.matk.IsDebuffed,
                buffState.enemy.matk.IsBuffed,
                buffState.enemy.mdef.IsDebuffed,
                buffState.enemy.mdef.IsBuffed,
                buffState.enemy.luc.IsDebuffed,
                buffState.enemy.luc.IsBuffed
            );
        }
    }
}