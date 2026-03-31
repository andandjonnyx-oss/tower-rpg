using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// スキルの追加効果をバトル中に実行するプロセッサ。
/// BattleSceneController から呼び出される。
///
/// 【設計】
///   効果タイプ（SkillEffectData の具象クラス）に応じて処理を分岐する。
///   新しい効果タイプを追加する場合は、このクラスに処理メソッドを追加する。
///
/// 【使用方法】
///   // バトル中、スキル命中後に呼び出す
///   var logs = SkillEffectProcessor.ProcessEffects(
///       skill.additionalEffects,
///       isPlayerAttack: true,
///       enemyMonster, ref enemyIsPoisoned, ref enemyCurrentHp);
///   foreach (var log in logs) AddLog(log);
/// </summary>
public static class SkillEffectProcessor
{
    /// <summary>
    /// スキルの追加効果リストを順に実行し、バトルログを返す。
    /// </summary>
    /// <param name="effects">スキルの追加効果リスト</param>
    /// <param name="isPlayerAttack">true=プレイヤーが攻撃側、false=敵が攻撃側</param>
    /// <param name="enemyMonster">敵モンスターデータ（耐性参照用）</param>
    /// <param name="enemyIsPoisoned">敵の毒状態フラグ（ref）</param>
    /// <param name="enemyCurrentHp">敵の現在HP（ref、回復効果等で変化する場合）</param>
    /// <returns>バトルログメッセージのリスト</returns>
    public static List<string> ProcessEffects(
        List<SkillEffectEntry> effects,
        bool isPlayerAttack,
        Monster enemyMonster,
        ref bool enemyIsPoisoned,
        ref int enemyCurrentHp)
    {
        var logs = new List<string>();
        if (effects == null || effects.Count == 0) return logs;

        for (int i = 0; i < effects.Count; i++)
        {
            var entry = effects[i];
            if (entry == null || entry.effectData == null) continue;

            if (entry.effectData is PoisonEffectData)
            {
                ProcessPoison(entry, isPlayerAttack, enemyMonster,
                              ref enemyIsPoisoned, logs);
            }
            else if (entry.effectData is LevelDrainEffectData)
            {
                ProcessLevelDrain(entry, isPlayerAttack, enemyMonster, logs);
            }
            else if (entry.effectData is HealEffectData)
            {
                ProcessHeal(entry, isPlayerAttack, enemyMonster,
                            ref enemyCurrentHp, logs);
            }
            else
            {
                Debug.LogWarning($"[SkillEffectProcessor] 未対応の効果タイプ: " +
                                 $"{entry.effectData.GetType().Name}");
            }
        }

        return logs;
    }

    // =========================================================
    // 毒付与
    // =========================================================

    /// <summary>
    /// 毒付与効果を処理する。
    /// </summary>
    private static void ProcessPoison(
        SkillEffectEntry entry,
        bool isPlayerAttack,
        Monster enemyMonster,
        ref bool enemyIsPoisoned,
        List<string> logs)
    {
        if (entry.chance <= 0) return;

        if (isPlayerAttack)
        {
            // プレイヤー → 敵に毒付与
            if (enemyIsPoisoned)
            {
                // 既に毒ならスキップ（ログも出さない）
                return;
            }
            int enemyPoisonResist = (enemyMonster != null)
                ? enemyMonster.PoisonResistance : 0;
            bool poisoned = StatusEffectSystem.TryInflict(
                entry.chance, enemyPoisonResist);
            if (poisoned)
            {
                enemyIsPoisoned = true;
                string eName = (enemyMonster != null)
                    ? enemyMonster.Mname : "敵";
                logs.Add($"{eName} は毒を受けた！");
            }
        }
        else
        {
            // 敵 → プレイヤーに毒付与
            if (GameState.I != null && GameState.I.isPoisoned)
            {
                // 既に毒ならスキップ
                return;
            }
            bool poisoned = StatusEffectSystem.TryPoisonPlayer(entry.chance);
            if (poisoned)
            {
                logs.Add("You は毒を受けた！");
            }
        }
    }

    // =========================================================
    // レベルドレイン
    // =========================================================

    /// <summary>
    /// レベルドレイン効果を処理する。
    /// 現在は敵 → プレイヤーのみ対応。
    /// </summary>
    private static void ProcessLevelDrain(
        SkillEffectEntry entry,
        bool isPlayerAttack,
        Monster enemyMonster,
        List<string> logs)
    {
        // 発動率判定
        if (entry.chance < 100)
        {
            int roll = Random.Range(0, 100);
            if (roll >= entry.chance) return;
        }

        if (!isPlayerAttack)
        {
            // 敵 → プレイヤーにレベルドレイン
            if (GameState.I == null)
            {
                return;
            }

            int drainAmount = (entry.intValue > 0) ? entry.intValue : 1;
            bool anySuccess = false;
            for (int d = 0; d < drainAmount; d++)
            {
                bool success = GameState.I.ApplyLevelDrain();
                if (success) anySuccess = true;
                else break; // レベル1に到達したら終了
            }

            if (anySuccess)
            {
                logs.Add($"You のレベルが {GameState.I.level} に下がった！");
            }
            else
            {
                logs.Add("…しかし効果がなかった！");
            }
        }
        else
        {
            // プレイヤー → 敵へのレベルドレインは現状未対応
            Debug.Log("[SkillEffectProcessor] プレイヤー→敵のレベルドレインは未実装");
        }
    }

    // =========================================================
    // HP回復
    // =========================================================

    /// <summary>
    /// HP回復効果を処理する。
    /// スキル使用者自身を回復する。
    /// </summary>
    private static void ProcessHeal(
        SkillEffectEntry entry,
        bool isPlayerAttack,
        Monster enemyMonster,
        ref int enemyCurrentHp,
        List<string> logs)
    {
        // 発動率判定
        if (entry.chance < 100)
        {
            int roll = Random.Range(0, 100);
            if (roll >= entry.chance) return;
        }

        int healPercent = (entry.intValue > 0) ? entry.intValue : 10;

        if (isPlayerAttack)
        {
            // プレイヤー自身を回復
            if (GameState.I == null) return;
            int maxHp = GameState.I.maxHp;
            int healAmount = Mathf.FloorToInt(maxHp * healPercent / 100f + 0.5f);
            if (healAmount < 1) healAmount = 1;
            GameState.I.currentHp += healAmount;
            if (GameState.I.currentHp > maxHp) GameState.I.currentHp = maxHp;
            logs.Add($"You は {healAmount} 回復した！");
        }
        else
        {
            // 敵自身を回復
            if (enemyMonster == null) return;
            int maxHp = enemyMonster.MaxHp;
            int healAmount = Mathf.FloorToInt(maxHp * healPercent / 100f + 0.5f);
            if (healAmount < 1) healAmount = 1;
            enemyCurrentHp += healAmount;
            if (enemyCurrentHp > maxHp) enemyCurrentHp = maxHp;
            string eName = enemyMonster.Mname;
            logs.Add($"{eName} は {healAmount} 回復した！");
        }
    }
}