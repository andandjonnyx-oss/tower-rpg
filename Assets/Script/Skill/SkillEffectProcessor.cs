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
///   var logs = SkillEffectProcessor.ProcessEffects(
///       skill.additionalEffects,
///       isPlayerAttack: true,
///       enemyMonster, ref enemyIsPoisoned, ref enemyIsStunned, ref enemyCurrentHp);
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
    /// <param name="enemyIsStunned">敵の気絶状態フラグ（ref）</param>
    /// <param name="enemyCurrentHp">敵の現在HP（ref、回復効果等で変化する場合）</param>
    /// <returns>バトルログメッセージのリスト</returns>
    public static List<string> ProcessEffects(
        List<SkillEffectEntry> effects,
        bool isPlayerAttack,
        Monster enemyMonster,
        ref bool enemyIsPoisoned,
        ref bool enemyIsStunned,
        ref int enemyCurrentHp)
    {
        var logs = new List<string>();
        if (effects == null || effects.Count == 0) return logs;

        for (int i = 0; i < effects.Count; i++)
        {
            var entry = effects[i];
            if (entry == null || entry.effectData == null) continue;

            if (entry.effectData is StatusAilmentEffectData)
            {
                ProcessStatusAilment(entry, isPlayerAttack, enemyMonster,
                                     ref enemyIsPoisoned, ref enemyIsStunned, logs);
            }
            else if (entry.effectData is LevelDrainEffectData)
            {
                ProcessLevelDrain(entry, isPlayerAttack, enemyMonster, logs);
            }
            else if (entry.effectData is HealEffectData healData)
            {
                ProcessHeal(entry, healData, isPlayerAttack, enemyMonster,
                            ref enemyCurrentHp, logs);
            }
            // =========================================================
            // 自爆エフェクト（追加）
            // =========================================================
            else if (entry.effectData is SelfDestructEffectData)
            {
                ProcessSelfDestruct(entry, isPlayerAttack, enemyMonster,
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
    // 状態異常（付与・回復）
    // =========================================================

    /// <summary>
    /// 状態異常効果を処理する。
    /// ailmentMode に応じて付与 or 回復を実行する。
    /// </summary>
    private static void ProcessStatusAilment(
        SkillEffectEntry entry,
        bool isPlayerAttack,
        Monster enemyMonster,
        ref bool enemyIsPoisoned,
        ref bool enemyIsStunned,
        List<string> logs)
    {
        if (entry.ailmentMode == AilmentMode.Inflict)
        {
            ProcessStatusAilmentInflict(entry, isPlayerAttack, enemyMonster,
                                        ref enemyIsPoisoned, ref enemyIsStunned, logs);
        }
        else if (entry.ailmentMode == AilmentMode.Cure)
        {
            ProcessStatusAilmentCure(entry, isPlayerAttack, logs);
        }
    }

    /// <summary>
    /// 状態異常付与を処理する。
    /// </summary>
    private static void ProcessStatusAilmentInflict(
        SkillEffectEntry entry,
        bool isPlayerAttack,
        Monster enemyMonster,
        ref bool enemyIsPoisoned,
        ref bool enemyIsStunned,
        List<string> logs)
    {
        if (entry.chance <= 0) return;

        // =========================================================
        // 毒（Poison）
        // =========================================================
        if (entry.targetStatusEffect == StatusEffect.Poison)
        {
            if (isPlayerAttack)
            {
                // プレイヤー → 敵に毒付与
                if (enemyIsPoisoned) return; // 既に毒ならスキップ
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
                if (GameState.I != null && GameState.I.isPoisoned) return;
                bool poisoned = StatusEffectSystem.TryPoisonPlayer(entry.chance);
                if (poisoned)
                {
                    logs.Add("You は毒を受けた！");
                }
            }
        }
        // =========================================================
        // 気絶（Stun）
        // =========================================================
        else if (entry.targetStatusEffect == StatusEffect.Stun)
        {
            if (isPlayerAttack)
            {
                // プレイヤー → 敵にスタン付与
                if (enemyIsStunned) return; // 既にスタンならスキップ
                int enemyStunResist = StatusEffectSystem.GetEnemyStunResistance(enemyMonster);
                bool stunned = StatusEffectSystem.TryStunEnemy(
                    entry.chance, enemyStunResist);
                if (stunned)
                {
                    enemyIsStunned = true;
                    string eName = (enemyMonster != null)
                        ? enemyMonster.Mname : "敵";
                    logs.Add($"{eName} は気絶した！");
                }
            }
            else
            {
                // 敵 → プレイヤーへのスタン付与は現状未対応
                Debug.Log("[SkillEffectProcessor] 敵→プレイヤーのスタン付与は未実装");
            }
        }
        else
        {
            // 未実装の状態異常
            Debug.Log($"[SkillEffectProcessor] 未実装の状態異常付与: {entry.targetStatusEffect}");
        }
    }

    /// <summary>
    /// 状態異常回復を処理する。
    /// 使用者自身の状態異常を回復する。
    /// </summary>
    private static void ProcessStatusAilmentCure(
        SkillEffectEntry entry,
        bool isPlayerAttack,
        List<string> logs)
    {
        if (entry.targetStatusEffect == StatusEffect.Poison)
        {
            if (isPlayerAttack)
            {
                // プレイヤーの毒を回復
                if (GameState.I != null && GameState.I.isPoisoned)
                {
                    GameState.I.isPoisoned = false;
                    logs.Add("You の毒が治った！");
                }
            }
            else
            {
                // 敵の毒回復は現状未対応（敵が自分の毒を治すスキルは将来対応）
                Debug.Log("[SkillEffectProcessor] 敵の状態異常回復は未実装");
            }
        }
        else
        {
            Debug.Log($"[SkillEffectProcessor] 未実装の状態異常回復: {entry.targetStatusEffect}");
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
    /// HealEffectData.formulaType に応じて回復量の計算式を切り替える。
    /// </summary>
    private static void ProcessHeal(
        SkillEffectEntry entry,
        HealEffectData healData,
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

        if (isPlayerAttack)
        {
            // プレイヤー自身を回復
            if (GameState.I == null) return;
            int healAmount = CalcHealAmount(healData.formulaType, entry.intValue,
                                            GameState.I.maxHp, GameState.I);
            GameState.I.currentHp += healAmount;
            if (GameState.I.currentHp > GameState.I.maxHp)
                GameState.I.currentHp = GameState.I.maxHp;
            logs.Add($"You は {healAmount} 回復した！");
        }
        else
        {
            // 敵自身を回復
            if (enemyMonster == null) return;
            int maxHp = enemyMonster.MaxHp;
            // 敵はステータス参照不可のため、Fixed / MaxHpPercent のみ有効
            int healAmount = CalcHealAmountEnemy(healData.formulaType, entry.intValue,
                                                 maxHp, enemyMonster);
            enemyCurrentHp += healAmount;
            if (enemyCurrentHp > maxHp) enemyCurrentHp = maxHp;
            string eName = enemyMonster.Mname;
            logs.Add($"{eName} は {healAmount} 回復した！");
        }
    }

    /// <summary>
    /// プレイヤーの回復量を計算する。
    /// </summary>
    private static int CalcHealAmount(HealFormulaType formulaType, int intValue,
                                       int maxHp, GameState gs)
    {
        int amount;
        switch (formulaType)
        {
            case HealFormulaType.Fixed:
                amount = (intValue > 0) ? intValue : 1;
                break;
            case HealFormulaType.MaxHpPercent:
                int percent = (intValue > 0) ? intValue : 10;
                amount = Mathf.FloorToInt(maxHp * percent / 100f + 0.5f);
                break;
            case HealFormulaType.IntMultiplier:
                int intStat = (gs != null) ? gs.baseINT : 1;
                int multiplier = (intValue > 0) ? intValue : 1;
                amount = intStat * multiplier;
                break;
            case HealFormulaType.StrMultiplier:
                int strStat = (gs != null) ? gs.baseSTR : 1;
                int strMul = (intValue > 0) ? intValue : 1;
                amount = strStat * strMul;
                break;
            default:
                amount = (intValue > 0) ? intValue : 1;
                break;
        }
        if (amount < 1) amount = 1;
        return amount;
    }

    /// <summary>
    /// 敵の回復量を計算する。
    /// 敵はステータス（INT/STR）を持たないため、ステータス依存の式は
    /// Monster.Attack をフォールバックとして使用する。
    /// </summary>
    private static int CalcHealAmountEnemy(HealFormulaType formulaType, int intValue,
                                            int maxHp, Monster monster)
    {
        int amount;
        switch (formulaType)
        {
            case HealFormulaType.Fixed:
                amount = (intValue > 0) ? intValue : 1;
                break;
            case HealFormulaType.MaxHpPercent:
                int percent = (intValue > 0) ? intValue : 10;
                amount = Mathf.FloorToInt(maxHp * percent / 100f + 0.5f);
                break;
            case HealFormulaType.IntMultiplier:
            case HealFormulaType.StrMultiplier:
                // 敵にはINT/STRがないため、Monster.Attack をフォールバック
                int fallbackStat = (monster != null) ? monster.Attack : 1;
                int mul = (intValue > 0) ? intValue : 1;
                amount = fallbackStat * mul;
                Debug.Log($"[SkillEffectProcessor] 敵のステータス依存回復: " +
                          $"Monster.Attack({fallbackStat}) × {mul} = {fallbackStat * mul}");
                break;
            default:
                amount = (intValue > 0) ? intValue : 1;
                break;
        }
        if (amount < 1) amount = 1;
        return amount;
    }

    // =========================================================
    // 自爆（追加）
    // =========================================================

    /// <summary>
    /// 自爆効果を処理する。
    /// スキル使用者自身の HP を 0 にする。
    ///
    /// 【処理】
    ///   isPlayerAttack == false（敵が使用）: enemyCurrentHp を 0 にする。
    ///   isPlayerAttack == true（プレイヤーが使用）: プレイヤーの currentHp を 0 にする。
    ///     ※ 通常はプレイヤーが自爆スキルを使うことは想定しないが、
    ///       将来の拡張のために対応しておく。
    /// </summary>
    private static void ProcessSelfDestruct(
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

        if (!isPlayerAttack)
        {
            // 敵が自爆 → 敵の HP を 0 にする
            enemyCurrentHp = 0;
            string eName = (enemyMonster != null) ? enemyMonster.Mname : "敵";
            logs.Add($"{eName} は力尽きた！");
            Debug.Log($"[SkillEffectProcessor] 自爆: {eName} の HP を 0 に設定");
        }
        else
        {
            // プレイヤーが自爆（通常は使わないが念のため）
            if (GameState.I != null)
            {
                GameState.I.currentHp = 0;
                logs.Add("You は力尽きた！");
                Debug.Log("[SkillEffectProcessor] 自爆: プレイヤーの HP を 0 に設定");
            }
        }
    }
}