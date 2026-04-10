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
/// 【オーバーロード構成】
///   (A) 既存互換: 6引数（毒・スタンのみ、反動なし）
///   (B) 既存互換+反動: 7引数
///   (C) Phase2 フル版: 11引数（全状態異常対応）
///   (D) Phase3 フル版: 19引数（バフ/デバフ対応）  ← 追加
/// </summary>
public static class SkillEffectProcessor
{
    // =========================================================
    // (D) Phase3 フル版: バフ/デバフ対応（内部実装本体）
    // =========================================================

    /// <summary>
    /// スキルの追加効果リストを順に実行し、バトルログを返す（Phase3フル版）。
    /// </summary>
    public static List<string> ProcessEffects(
        List<SkillEffectEntry> effects,
        bool isPlayerAttack,
        Monster enemyMonster,
        ref bool enemyIsPoisoned,
        ref bool enemyIsStunned,
        ref int enemyCurrentHp,
        int lastDamageDealt,
        ref bool enemyIsParalyzed,
        ref bool enemyIsBlind,
        ref int enemyRageTurn,
        ref int playerRageTurn,
        // --- バフ/デバフ（Phase3 追加） ---
        ref int playerDefDebuffTurn,
        ref float playerDefDebuffRate,
        ref int playerDefBuffTurn,
        ref float playerDefBuffRate,
        ref int enemyDefDebuffTurn,
        ref float enemyDefDebuffRate,
        ref int enemyDefBuffTurn,
        ref float enemyDefBuffRate)
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
                                     ref enemyIsPoisoned, ref enemyIsStunned,
                                     ref enemyIsParalyzed, ref enemyIsBlind,
                                     ref enemyRageTurn, ref playerRageTurn,
                                     ref playerDefDebuffTurn, ref playerDefDebuffRate,
                                     ref playerDefBuffTurn, ref playerDefBuffRate,
                                     ref enemyDefDebuffTurn, ref enemyDefDebuffRate,
                                     ref enemyDefBuffTurn, ref enemyDefBuffRate,
                                     logs);
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
            else if (entry.effectData is SelfDestructEffectData)
            {
                ProcessSelfDestruct(entry, isPlayerAttack, enemyMonster,
                                    ref enemyCurrentHp, logs);
            }
            else if (entry.effectData is RecoilEffectData)
            {
                ProcessRecoil(entry, isPlayerAttack, lastDamageDealt, logs);
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
    // (C) Phase2 フル版: 11引数（後方互換ブリッジ）
    // =========================================================

    /// <summary>
    /// Phase2互換オーバーロード（11引数版）。
    /// バフ/デバフのダミー変数を用意して Phase3 版に委譲する。
    /// </summary>
    public static List<string> ProcessEffects(
        List<SkillEffectEntry> effects,
        bool isPlayerAttack,
        Monster enemyMonster,
        ref bool enemyIsPoisoned,
        ref bool enemyIsStunned,
        ref int enemyCurrentHp,
        int lastDamageDealt,
        ref bool enemyIsParalyzed,
        ref bool enemyIsBlind,
        ref int enemyRageTurn,
        ref int playerRageTurn)
    {
        int dPDDT = 0; float dPDDR = 0f;
        int dPDBT = 0; float dPDBR = 0f;
        int dEDDT = 0; float dEDDR = 0f;
        int dEDBT = 0; float dEDBR = 0f;
        return ProcessEffects(effects, isPlayerAttack, enemyMonster,
            ref enemyIsPoisoned, ref enemyIsStunned, ref enemyCurrentHp,
            lastDamageDealt,
            ref enemyIsParalyzed, ref enemyIsBlind, ref enemyRageTurn, ref playerRageTurn,
            ref dPDDT, ref dPDDR, ref dPDBT, ref dPDBR,
            ref dEDDT, ref dEDDR, ref dEDBT, ref dEDBR);
    }

    // =========================================================
    // (A) 既存互換: 6引数（毒・スタンのみ、反動なし）
    // =========================================================

    /// <summary>
    /// 後方互換用オーバーロード（6引数版）。
    /// </summary>
    public static List<string> ProcessEffects(
        List<SkillEffectEntry> effects,
        bool isPlayerAttack,
        Monster enemyMonster,
        ref bool enemyIsPoisoned,
        ref bool enemyIsStunned,
        ref int enemyCurrentHp)
    {
        bool dP = false; bool dB = false;
        int dER = 0; int dPR = 0;
        return ProcessEffects(effects, isPlayerAttack, enemyMonster,
            ref enemyIsPoisoned, ref enemyIsStunned, ref enemyCurrentHp,
            0,
            ref dP, ref dB, ref dER, ref dPR);
    }

    // =========================================================
    // (B) 既存互換+反動: 7引数
    // =========================================================

    /// <summary>
    /// 後方互換用オーバーロード（7引数版）。
    /// </summary>
    public static List<string> ProcessEffects(
        List<SkillEffectEntry> effects,
        bool isPlayerAttack,
        Monster enemyMonster,
        ref bool enemyIsPoisoned,
        ref bool enemyIsStunned,
        ref int enemyCurrentHp,
        int lastDamageDealt)
    {
        bool dP = false; bool dB = false;
        int dER = 0; int dPR = 0;
        return ProcessEffects(effects, isPlayerAttack, enemyMonster,
            ref enemyIsPoisoned, ref enemyIsStunned, ref enemyCurrentHp,
            lastDamageDealt,
            ref dP, ref dB, ref dER, ref dPR);
    }

    // =========================================================
    // 状態異常（付与・回復）— 統合版
    // =========================================================

    private static void ProcessStatusAilment(
        SkillEffectEntry entry,
        bool isPlayerAttack,
        Monster enemyMonster,
        ref bool enemyIsPoisoned,
        ref bool enemyIsStunned,
        ref bool enemyIsParalyzed,
        ref bool enemyIsBlind,
        ref int enemyRageTurn,
        ref int playerRageTurn,
        ref int playerDefDebuffTurn,
        ref float playerDefDebuffRate,
        ref int playerDefBuffTurn,
        ref float playerDefBuffRate,
        ref int enemyDefDebuffTurn,
        ref float enemyDefDebuffRate,
        ref int enemyDefBuffTurn,
        ref float enemyDefBuffRate,
        List<string> logs)
    {
        if (entry.ailmentMode == AilmentMode.Inflict)
        {
            ProcessStatusAilmentInflict(entry, isPlayerAttack, enemyMonster,
                                        ref enemyIsPoisoned, ref enemyIsStunned,
                                        ref enemyIsParalyzed, ref enemyIsBlind,
                                        ref enemyRageTurn, ref playerRageTurn,
                                        ref playerDefDebuffTurn, ref playerDefDebuffRate,
                                        ref playerDefBuffTurn, ref playerDefBuffRate,
                                        ref enemyDefDebuffTurn, ref enemyDefDebuffRate,
                                        ref enemyDefBuffTurn, ref enemyDefBuffRate,
                                        logs);
        }
        else if (entry.ailmentMode == AilmentMode.Cure)
        {
            ProcessStatusAilmentCure(entry, isPlayerAttack, logs);
        }
    }

    /// <summary>
    /// 状態異常付与を処理する。
    /// switch/case で全状態異常を統一的に処理する。
    /// </summary>
    private static void ProcessStatusAilmentInflict(
        SkillEffectEntry entry,
        bool isPlayerAttack,
        Monster enemyMonster,
        ref bool enemyIsPoisoned,
        ref bool enemyIsStunned,
        ref bool enemyIsParalyzed,
        ref bool enemyIsBlind,
        ref int enemyRageTurn,
        ref int playerRageTurn,
        ref int playerDefDebuffTurn,
        ref float playerDefDebuffRate,
        ref int playerDefBuffTurn,
        ref float playerDefBuffRate,
        ref int enemyDefDebuffTurn,
        ref float enemyDefDebuffRate,
        ref int enemyDefBuffTurn,
        ref float enemyDefBuffRate,
        List<string> logs)
    {
        if (entry.chance <= 0) return;

        StatusEffect effect = entry.targetStatusEffect;

        switch (effect)
        {
            // =========================================================
            // 持続型デバフ（双方向対応）: 毒・麻痺・暗闇
            // =========================================================
            case StatusEffect.Poison:
            case StatusEffect.Paralyze:
            case StatusEffect.Blind:
                ProcessBidirectionalAilment(
                    effect, entry.chance, isPlayerAttack, enemyMonster,
                    ref enemyIsPoisoned, ref enemyIsParalyzed, ref enemyIsBlind,
                    logs);
                break;

            // =========================================================
            // 気絶（Stun）— プレイヤー→敵のみ
            // =========================================================
            case StatusEffect.Stun:
                if (isPlayerAttack)
                {
                    if (enemyIsStunned)
                    {
                        logs.Add("…しかし効果がなかった！");
                        return;
                    }
                    int stunResist = StatusEffectSystem.GetEnemyResistance(StatusEffect.Stun, enemyMonster);
                    bool stunned = StatusEffectSystem.TryInflict(entry.chance, stunResist);
                    if (stunned)
                    {
                        enemyIsStunned = true;
                        string eName = (enemyMonster != null) ? enemyMonster.Mname : "敵";
                        logs.Add($"{eName} は気絶した！");
                    }
                    else
                    {
                        logs.Add("…しかし効果がなかった！");
                    }
                }
                else
                {
                    Debug.Log("[SkillEffectProcessor] 敵→プレイヤーのスタン付与は未実装");
                }
                break;

            // =========================================================
            // 怒り（Rage / バーサク）— 使用者自身に付与するバフ
            // =========================================================
            case StatusEffect.Rage:
                ProcessRageInflict(entry.chance, isPlayerAttack, enemyMonster,
                                   ref enemyRageTurn, ref playerRageTurn, logs);
                break;

            // =========================================================
            // 防御バフ/デバフ（Phase3 追加）— 双方向対応
            // =========================================================
            case StatusEffect.DefenseDown:
            case StatusEffect.DefenseUp:
                ProcessDefenseBuffDebuff(
                    entry, isPlayerAttack, enemyMonster,
                    ref playerDefDebuffTurn, ref playerDefDebuffRate,
                    ref playerDefBuffTurn, ref playerDefBuffRate,
                    ref enemyDefDebuffTurn, ref enemyDefDebuffRate,
                    ref enemyDefBuffTurn, ref enemyDefBuffRate,
                    logs);
                break;

            default:
                Debug.Log($"[SkillEffectProcessor] 未実装の状態異常付与: {effect}");
                break;
        }
    }

    /// <summary>
    /// 双方向対応の持続型デバフ付与処理。
    /// Poison / Paralyze / Blind を統一的に処理する。
    /// </summary>
    private static void ProcessBidirectionalAilment(
        StatusEffect effect,
        int chance,
        bool isPlayerAttack,
        Monster enemyMonster,
        ref bool enemyIsPoisoned,
        ref bool enemyIsParalyzed,
        ref bool enemyIsBlind,
        List<string> logs)
    {
        string ailmentName = effect.ToJapanese();
        string eName = (enemyMonster != null) ? enemyMonster.Mname : "敵";

        if (isPlayerAttack)
        {
            // プレイヤー → 敵に付与
            ref bool enemyFlag = ref GetEnemyAilmentRef(effect,
                ref enemyIsPoisoned, ref enemyIsParalyzed, ref enemyIsBlind);

            if (enemyFlag)
            {
                logs.Add("…しかし効果がなかった！");
                return;
            }

            int resist = StatusEffectSystem.GetEnemyResistance(effect, enemyMonster);
            bool inflicted = StatusEffectSystem.TryInflict(chance, resist);
            if (inflicted)
            {
                enemyFlag = true;
                logs.Add($"{eName} は{ailmentName}を受けた！");
            }
            else
            {
                logs.Add("…しかし効果がなかった！");
            }
        }
        else
        {
            // 敵 → プレイヤーに付与
            if (StatusEffectSystem.IsPlayerAffected(effect))
            {
                logs.Add("…しかし効果がなかった！");
                return;
            }

            bool inflicted = StatusEffectSystem.TryInflictPlayer(effect, chance);
            if (inflicted)
            {
                logs.Add($"You は{ailmentName}を受けた！");
            }
            else
            {
                logs.Add("…しかし効果がなかった！");
            }
        }
    }

    /// <summary>
    /// 状態異常ごとに敵側の対応するフラグの参照を返すヘルパー。
    /// </summary>
    private static ref bool GetEnemyAilmentRef(
        StatusEffect effect,
        ref bool enemyIsPoisoned,
        ref bool enemyIsParalyzed,
        ref bool enemyIsBlind)
    {
        switch (effect)
        {
            case StatusEffect.Paralyze: return ref enemyIsParalyzed;
            case StatusEffect.Blind: return ref enemyIsBlind;
            case StatusEffect.Poison:
            default: return ref enemyIsPoisoned;
        }
    }

    /// <summary>
    /// 怒り（バーサク）の付与処理。
    /// 使用者自身にかかるバフ的効果。
    /// </summary>
    private static void ProcessRageInflict(
        int chance,
        bool isPlayerAttack,
        Monster enemyMonster,
        ref int enemyRageTurn,
        ref int playerRageTurn,
        List<string> logs)
    {
        // 発動率判定
        if (chance < 100)
        {
            int roll = Random.Range(0, 100);
            if (roll >= chance) return;
        }

        if (isPlayerAttack)
        {
            // プレイヤーが使用 → プレイヤー自身が怒り状態になる
            if (playerRageTurn > 0)
            {
                logs.Add("…しかし効果がなかった！");
                return;
            }
            playerRageTurn = StatusEffectSystem.RageDuration;
            logs.Add("You は怒りに燃えた！ 攻撃力UP！");
        }
        else
        {
            // 敵が使用 → 敵自身が怒り状態になる
            if (enemyRageTurn > 0)
            {
                logs.Add("…しかし効果がなかった！");
                return;
            }
            enemyRageTurn = StatusEffectSystem.RageDuration;
            string eName = (enemyMonster != null) ? enemyMonster.Mname : "敵";
            logs.Add($"{eName} は怒りに燃えた！ 攻撃力UP！");
        }
    }

    // =========================================================
    // 防御バフ/デバフ付与処理（Phase3 追加）
    // =========================================================

    /// <summary>
    /// 防御バフ/デバフの付与処理。
    /// 双方向対応（プレイヤー→敵、敵→プレイヤー）。
    ///
    /// 【仕様】
    ///   - 反対効果がかかっている場合: 反対効果を解除してから新効果を付与（後優先）
    ///   - 同じ効果が既にかかっている場合: ターン数をリセット（率は上書き）
    ///   - 耐性判定あり（immuneToAllAilments や個別耐性で弾ける）
    ///   - intValue = 効果率（%）。例: 30 = 30%減少/増加
    ///   - duration = 持続ターン数。0 ならデフォルト値を使用。
    /// </summary>
    private static void ProcessDefenseBuffDebuff(
        SkillEffectEntry entry,
        bool isPlayerAttack,
        Monster enemyMonster,
        ref int playerDefDebuffTurn,
        ref float playerDefDebuffRate,
        ref int playerDefBuffTurn,
        ref float playerDefBuffRate,
        ref int enemyDefDebuffTurn,
        ref float enemyDefDebuffRate,
        ref int enemyDefBuffTurn,
        ref float enemyDefBuffRate,
        List<string> logs)
    {
        StatusEffect effect = entry.targetStatusEffect;
        StatusEffect opposite = StatusEffectSystem.GetOpposite(effect);
        float rate = (entry.intValue > 0) ? entry.intValue : 30f;
        int dur = (entry.duration > 0) ? entry.duration : StatusEffectSystem.DefaultBuffDebuffDuration;
        string effectName = effect.ToJapanese();

        if (isPlayerAttack)
        {
            // プレイヤーが使用
            // DefenseDown → 敵に防御デバフ / DefenseUp → プレイヤー自身に防御バフ
            if (effect == StatusEffect.DefenseDown)
            {
                // 敵に防御デバフ付与
                string eName = (enemyMonster != null) ? enemyMonster.Mname : "敵";

                // 耐性判定
                int resist = StatusEffectSystem.GetEnemyResistance(effect, enemyMonster);
                if (!StatusEffectSystem.TryInflict(entry.chance, resist))
                {
                    logs.Add("…しかし効果がなかった！");
                    return;
                }

                // 反対効果（DefenseUp）を解除
                if (enemyDefBuffTurn > 0)
                {
                    enemyDefBuffTurn = 0;
                    enemyDefBuffRate = 0f;
                    logs.Add($"{eName} の防御アップが解除された！");
                }

                // 付与（同効果なら上書き）
                enemyDefDebuffTurn = dur;
                enemyDefDebuffRate = rate;
                logs.Add($"{eName} の防御が {rate}%下がった！（{dur}ターン）");
            }
            else if (effect == StatusEffect.DefenseUp)
            {
                // プレイヤー自身に防御バフ付与（耐性判定なし — 自分へのバフ）
                // 反対効果（DefenseDown）を解除
                if (playerDefDebuffTurn > 0)
                {
                    playerDefDebuffTurn = 0;
                    playerDefDebuffRate = 0f;
                    logs.Add("You の防御ダウンが解除された！");
                }

                playerDefBuffTurn = dur;
                playerDefBuffRate = rate;
                logs.Add($"You の防御が {rate}%上がった！（{dur}ターン）");
            }
        }
        else
        {
            // 敵が使用
            // DefenseDown → プレイヤーに防御デバフ / DefenseUp → 敵自身に防御バフ
            if (effect == StatusEffect.DefenseDown)
            {
                // プレイヤーに防御デバフ付与
                int resist = StatusEffectSystem.GetPlayerResistance(effect);
                if (!StatusEffectSystem.TryInflict(entry.chance, resist))
                {
                    logs.Add("…しかし効果がなかった！");
                    return;
                }

                // 反対効果（DefenseUp）を解除
                if (playerDefBuffTurn > 0)
                {
                    playerDefBuffTurn = 0;
                    playerDefBuffRate = 0f;
                    logs.Add("You の防御アップが解除された！");
                }

                playerDefDebuffTurn = dur;
                playerDefDebuffRate = rate;
                logs.Add($"You の防御が {rate}%下がった！（{dur}ターン）");
            }
            else if (effect == StatusEffect.DefenseUp)
            {
                // 敵自身に防御バフ付与（耐性判定なし — 自分へのバフ）
                string eName = (enemyMonster != null) ? enemyMonster.Mname : "敵";

                // 反対効果（DefenseDown）を解除
                if (enemyDefDebuffTurn > 0)
                {
                    enemyDefDebuffTurn = 0;
                    enemyDefDebuffRate = 0f;
                    logs.Add($"{eName} の防御ダウンが解除された！");
                }

                enemyDefBuffTurn = dur;
                enemyDefBuffRate = rate;
                logs.Add($"{eName} の防御が {rate}%上がった！（{dur}ターン）");
            }
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
        StatusEffect effect = entry.targetStatusEffect;

        if (isPlayerAttack)
        {
            switch (effect)
            {
                case StatusEffect.Poison:
                case StatusEffect.Paralyze:
                case StatusEffect.Blind:
                    if (StatusEffectSystem.IsPlayerAffected(effect))
                    {
                        StatusEffectSystem.CurePlayer(effect);
                        logs.Add($"You の{effect.ToJapanese()}が治った！");
                    }
                    break;
                default:
                    Debug.Log($"[SkillEffectProcessor] 未実装の状態異常回復: {effect}");
                    break;
            }
        }
        else
        {
            Debug.Log("[SkillEffectProcessor] 敵の状態異常回復は未実装");
        }
    }

    // =========================================================
    // レベルドレイン
    // =========================================================

    private static void ProcessLevelDrain(
        SkillEffectEntry entry,
        bool isPlayerAttack,
        Monster enemyMonster,
        List<string> logs)
    {
        if (entry.chance < 100)
        {
            int roll = Random.Range(0, 100);
            if (roll >= entry.chance) return;
        }

        if (!isPlayerAttack)
        {
            if (GameState.I == null) return;

            int drainAmount = (entry.intValue > 0) ? entry.intValue : 1;
            bool anySuccess = false;
            for (int d = 0; d < drainAmount; d++)
            {
                bool success = GameState.I.ApplyLevelDrain();
                if (success) anySuccess = true;
                else break;
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
            Debug.Log("[SkillEffectProcessor] プレイヤー→敵のレベルドレインは未実装");
        }
    }

    // =========================================================
    // HP回復
    // =========================================================

    private static void ProcessHeal(
        SkillEffectEntry entry,
        HealEffectData healData,
        bool isPlayerAttack,
        Monster enemyMonster,
        ref int enemyCurrentHp,
        List<string> logs)
    {
        if (entry.chance < 100)
        {
            int roll = Random.Range(0, 100);
            if (roll >= entry.chance) return;
        }

        if (isPlayerAttack)
        {
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
            if (enemyMonster == null) return;
            int maxHp = enemyMonster.MaxHp;
            int healAmount = CalcHealAmountEnemy(healData.formulaType, entry.intValue,
                                                 maxHp, enemyMonster);
            enemyCurrentHp += healAmount;
            if (enemyCurrentHp > maxHp) enemyCurrentHp = maxHp;
            string eName = enemyMonster.Mname;
            logs.Add($"{eName} は {healAmount} 回復した！");
        }
    }

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
    // 自爆
    // =========================================================

    private static void ProcessSelfDestruct(
        SkillEffectEntry entry,
        bool isPlayerAttack,
        Monster enemyMonster,
        ref int enemyCurrentHp,
        List<string> logs)
    {
        if (entry.chance < 100)
        {
            int roll = Random.Range(0, 100);
            if (roll >= entry.chance) return;
        }

        if (!isPlayerAttack)
        {
            enemyCurrentHp = 0;
            string eName = (enemyMonster != null) ? enemyMonster.Mname : "敵";
            logs.Add($"{eName} は力尽きた！");
            Debug.Log($"[SkillEffectProcessor] 自爆: {eName} の HP を 0 に設定");
        }
        else
        {
            if (GameState.I != null)
            {
                GameState.I.currentHp = 0;
                logs.Add("You は力尽きた！");
                Debug.Log("[SkillEffectProcessor] 自爆: プレイヤーの HP を 0 に設定");
            }
        }
    }

    // =========================================================
    // 反動ダメージ
    // =========================================================

    private static void ProcessRecoil(
        SkillEffectEntry entry,
        bool isPlayerAttack,
        int lastDamageDealt,
        List<string> logs)
    {
        if (entry.chance < 100)
        {
            int roll = Random.Range(0, 100);
            if (roll >= entry.chance) return;
        }

        if (lastDamageDealt <= 0) return;

        int recoilPercent = (entry.intValue > 0) ? entry.intValue : 10;
        int recoilDamage = Mathf.FloorToInt(lastDamageDealt * recoilPercent / 100f);
        if (recoilDamage < 1) recoilDamage = 1;

        if (isPlayerAttack)
        {
            if (GameState.I != null)
            {
                GameState.I.currentHp -= recoilDamage;
                if (GameState.I.currentHp < 0) GameState.I.currentHp = 0;
                logs.Add($"呪いの反動で You は {recoilDamage} ダメージ！");
                Debug.Log($"[SkillEffectProcessor] 反動ダメージ: {lastDamageDealt} × {recoilPercent}% = {recoilDamage}");
            }
        }
        else
        {
            Debug.Log("[SkillEffectProcessor] 敵→敵自身の反動ダメージは未実装");
        }
    }
}