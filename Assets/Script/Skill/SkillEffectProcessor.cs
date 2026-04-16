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
/// 【オーバーロード構成（Phase4: 構造体ベース）】
///   (A) 既存互換: 6引数（毒・スタンのみ、反動なし）
///   (B) 既存互換+反動: 7引数
///   (C) Phase2互換: 11引数（全状態異常対応）
///   (D) Phase4 フル版: 構造体ベース（全バフ/デバフ対応）  ← 新メイン
///   (E) Phase5 フル版: Phase4 + 沈黙（enemyIsSilenced）   ← 最新メイン
/// </summary>
public static class SkillEffectProcessor
{
    // =========================================================
    // (E) Phase5 フル版: Phase4 + 沈黙対応（最新メイン実装）
    // =========================================================

    /// <summary>
    /// スキルの追加効果リストを順に実行し、バトルログを返す（Phase5フル版）。
    /// Phase4 に enemyIsSilenced を追加。
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
        ref bool enemyIsSilenced,
        ref int enemyRageTurn,
        ref int playerRageTurn,
        ref BattleBuffState buffState)
    {
        var logs = new List<string>();
        if (effects == null || effects.Count == 0) return logs;

        for (int i = 0; i < effects.Count; i++)
        {
            var entry = effects[i];
            if (entry == null || entry.effectData == null) continue;

            if (entry.effectData is DispelEffectData dispelData)
            {
                ProcessDispel(entry, dispelData, isPlayerAttack, enemyMonster,
                              ref buffState, logs);
            }
            else if (entry.effectData is StatusAilmentEffectData)
            {
                ProcessStatusAilment(entry, isPlayerAttack, enemyMonster,
                                     ref enemyIsPoisoned, ref enemyIsStunned,
                                     ref enemyIsParalyzed, ref enemyIsBlind,
                                     ref enemyIsSilenced,
                                     ref enemyRageTurn, ref playerRageTurn,
                                     ref buffState,
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
            else if (entry.effectData is MpDamageEffectData)
            {
                ProcessMpDamage(entry, isPlayerAttack, logs);
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
    // (D) Phase4 フル版: 構造体ベース（後方互換ブリッジ → Phase5 に委譲）
    // =========================================================

    /// <summary>
    /// Phase4互換オーバーロード。
    /// enemyIsSilenced のダミーを用意して Phase5 版に委譲する。
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
        ref BattleBuffState buffState)
    {
        bool dummySilenced = false;
        return ProcessEffects(effects, isPlayerAttack, enemyMonster,
            ref enemyIsPoisoned, ref enemyIsStunned, ref enemyCurrentHp,
            lastDamageDealt,
            ref enemyIsParalyzed, ref enemyIsBlind, ref dummySilenced,
            ref enemyRageTurn, ref playerRageTurn,
            ref buffState);
    }

    // =========================================================
    // (C) Phase2 フル版: 11引数（後方互換ブリッジ）
    // =========================================================

    /// <summary>
    /// Phase2互換オーバーロード（11引数版）。
    /// バフ/デバフのダミー状態を用意して Phase4 版に委譲する。
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
        BattleBuffState dummy = new BattleBuffState();
        return ProcessEffects(effects, isPlayerAttack, enemyMonster,
            ref enemyIsPoisoned, ref enemyIsStunned, ref enemyCurrentHp,
            lastDamageDealt,
            ref enemyIsParalyzed, ref enemyIsBlind, ref enemyRageTurn, ref playerRageTurn,
            ref dummy);
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
    // ディスペル（バフ/デバフ解除）
    // =========================================================

    /// <summary>
    /// ディスペル効果を処理する。
    /// DispelMode に応じて、自分のデバフ解除 / 相手のバフ解除 / 全解除 を行う。
    ///
    /// 【仕様】
    ///   - CureOwnDebuffs  : 使用者自身のデバフ（Down系5種）を全解除
    ///   - DispelEnemyBuffs : 相手のバフ（Up系5種）を全解除
    ///   - DispelAll        : 敵味方双方のバフ/デバフ（Up/Down系5種全て）を全解除
    ///   - いずれも状態異常（毒・麻痺・暗闇・沈黙・怒り）は解除しない
    ///   - chance による発動率判定あり
    /// </summary>
    private static void ProcessDispel(
        SkillEffectEntry entry,
        DispelEffectData dispelData,
        bool isPlayerAttack,
        Monster enemyMonster,
        ref BattleBuffState buffState,
        List<string> logs)
    {
        // 発動率判定
        if (entry.chance < 100)
        {
            int roll = Random.Range(0, 100);
            if (roll >= entry.chance)
            {
                logs.Add("…しかし効果がなかった！");
                return;
            }
        }

        string eName = (enemyMonster != null) ? enemyMonster.Mname : "敵";

        switch (dispelData.dispelMode)
        {
            case DispelMode.CureOwnDebuffs:
                if (isPlayerAttack)
                {
                    bool cleared = ClearDebuffs(ref buffState.player);
                    if (cleared)
                        logs.Add("You のデバフが全て解除された！");
                    else
                        logs.Add("…しかし効果がなかった！");
                }
                else
                {
                    bool cleared = ClearDebuffs(ref buffState.enemy);
                    if (cleared)
                        logs.Add($"{eName} のデバフが全て解除された！");
                    else
                        logs.Add("…しかし効果がなかった！");
                }
                break;

            case DispelMode.DispelEnemyBuffs:
                if (isPlayerAttack)
                {
                    bool cleared = ClearBuffs(ref buffState.enemy);
                    if (cleared)
                        logs.Add($"{eName} のバフが全て解除された！");
                    else
                        logs.Add("…しかし効果がなかった！");
                }
                else
                {
                    bool cleared = ClearBuffs(ref buffState.player);
                    if (cleared)
                        logs.Add("You のバフが全て解除された！");
                    else
                        logs.Add("…しかし効果がなかった！");
                }
                break;

            case DispelMode.DispelAll:
                {
                    bool anyCleared = false;
                    anyCleared |= ClearDebuffs(ref buffState.player);
                    anyCleared |= ClearBuffs(ref buffState.player);
                    anyCleared |= ClearDebuffs(ref buffState.enemy);
                    anyCleared |= ClearBuffs(ref buffState.enemy);

                    if (anyCleared)
                        logs.Add("全てのバフ/デバフが解除された！");
                    else
                        logs.Add("…しかし効果がなかった！");
                }
                break;

            default:
                Debug.LogWarning($"[SkillEffectProcessor] 未対応のDispelMode: {dispelData.dispelMode}");
                break;
        }
    }

    /// <summary>
    /// 指定した BuffDebuffSet のデバフ（Down系）を全て解除する。
    /// 1つでも解除した場合は true を返す。
    /// </summary>
    private static bool ClearDebuffs(ref BuffDebuffSet set)
    {
        bool any = false;
        if (set.def.IsDebuffed) { set.def.debuffTurn = 0; set.def.debuffRate = 0f; any = true; }
        if (set.atk.IsDebuffed) { set.atk.debuffTurn = 0; set.atk.debuffRate = 0f; any = true; }
        if (set.matk.IsDebuffed) { set.matk.debuffTurn = 0; set.matk.debuffRate = 0f; any = true; }
        if (set.mdef.IsDebuffed) { set.mdef.debuffTurn = 0; set.mdef.debuffRate = 0f; any = true; }
        if (set.luc.IsDebuffed) { set.luc.debuffTurn = 0; set.luc.debuffRate = 0f; any = true; }
        return any;
    }

    /// <summary>
    /// 指定した BuffDebuffSet のバフ（Up系）を全て解除する。
    /// 1つでも解除した場合は true を返す。
    /// </summary>
    private static bool ClearBuffs(ref BuffDebuffSet set)
    {
        bool any = false;
        if (set.def.IsBuffed) { set.def.buffTurn = 0; set.def.buffRate = 0f; any = true; }
        if (set.atk.IsBuffed) { set.atk.buffTurn = 0; set.atk.buffRate = 0f; any = true; }
        if (set.matk.IsBuffed) { set.matk.buffTurn = 0; set.matk.buffRate = 0f; any = true; }
        if (set.mdef.IsBuffed) { set.mdef.buffTurn = 0; set.mdef.buffRate = 0f; any = true; }
        if (set.luc.IsBuffed) { set.luc.buffTurn = 0; set.luc.buffRate = 0f; any = true; }
        return any;
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
        ref bool enemyIsSilenced,
        ref int enemyRageTurn,
        ref int playerRageTurn,
        ref BattleBuffState buffState,
        List<string> logs)
    {
        if (entry.ailmentMode == AilmentMode.Inflict)
        {
            ProcessStatusAilmentInflict(entry, isPlayerAttack, enemyMonster,
                                        ref enemyIsPoisoned, ref enemyIsStunned,
                                        ref enemyIsParalyzed, ref enemyIsBlind,
                                        ref enemyIsSilenced,
                                        ref enemyRageTurn, ref playerRageTurn,
                                        ref buffState,
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
        ref bool enemyIsSilenced,
        ref int enemyRageTurn,
        ref int playerRageTurn,
        ref BattleBuffState buffState,
        List<string> logs)
    {
        if (entry.chance <= 0) return;

        StatusEffect effect = entry.targetStatusEffect;

        switch (effect)
        {
            // =========================================================
            // 持続型デバフ（双方向対応）: 毒・麻痺・暗闇・沈黙
            // =========================================================
            case StatusEffect.Poison:
            case StatusEffect.Paralyze:
            case StatusEffect.Blind:
            case StatusEffect.Silence:
                ProcessBidirectionalAilment(
                    effect, entry.chance, isPlayerAttack, enemyMonster,
                    ref enemyIsPoisoned, ref enemyIsParalyzed, ref enemyIsBlind,
                    ref enemyIsSilenced,
                    logs);
                break;

            // =========================================================
            // 石化（Petrify）— Phase A: 付与のみ実装
            // 独立処理: bool だけでなく残ターン/最大ターンを扱うため専用メソッドに分離
            // =========================================================
            case StatusEffect.Petrify:
                ProcessPetrifyInflict(entry.chance, isPlayerAttack, enemyMonster, logs);
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
            // バフ/デバフ（Phase4: 全5種汎用処理）
            // =========================================================
            case StatusEffect.DefenseDown:
            case StatusEffect.DefenseUp:
            case StatusEffect.AttackDown:
            case StatusEffect.AttackUp:
            case StatusEffect.MagicAttackDown:
            case StatusEffect.MagicAttackUp:
            case StatusEffect.MagicDefenseDown:
            case StatusEffect.MagicDefenseUp:
            case StatusEffect.LuckDown:
            case StatusEffect.LuckUp:
                ProcessStatBuffDebuff(entry, isPlayerAttack, enemyMonster,
                                      ref buffState, logs);
                break;

            default:
                Debug.Log($"[SkillEffectProcessor] 未実装の状態異常付与: {effect}");
                break;
        }
    }

    /// <summary>
    /// 双方向対応の持続型デバフ付与処理。
    /// Poison / Paralyze / Blind / Silence を統一的に処理する。
    /// </summary>
    private static void ProcessBidirectionalAilment(
        StatusEffect effect,
        int chance,
        bool isPlayerAttack,
        Monster enemyMonster,
        ref bool enemyIsPoisoned,
        ref bool enemyIsParalyzed,
        ref bool enemyIsBlind,
        ref bool enemyIsSilenced,
        List<string> logs)
    {
        string ailmentName = effect.ToJapanese();
        string eName = (enemyMonster != null) ? enemyMonster.Mname : "敵";

        if (isPlayerAttack)
        {
            // プレイヤー → 敵に付与
            ref bool enemyFlag = ref GetEnemyAilmentRef(effect,
                ref enemyIsPoisoned, ref enemyIsParalyzed, ref enemyIsBlind,
                ref enemyIsSilenced);

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
        ref bool enemyIsBlind,
        ref bool enemyIsSilenced)
    {
        switch (effect)
        {
            case StatusEffect.Paralyze: return ref enemyIsParalyzed;
            case StatusEffect.Blind: return ref enemyIsBlind;
            case StatusEffect.Silence: return ref enemyIsSilenced;
            case StatusEffect.Poison:
            default: return ref enemyIsPoisoned;
        }
    }

    /// <summary>
    /// 石化付与処理（双方向対応・Phase A）。
    /// プレイヤー→敵、敵→プレイヤー両方に対応。
    ///
    /// 【Phase A の挙動】
    ///   - 既に石化中の場合: 残ターンを1減らす（最低1でクランプ）
    ///   - 未石化の場合: 石化フラグを立て、残ターンを初期値（プレイヤー5/敵10）にセット
    ///   - 発動率 chance と耐性判定を経る
    ///   - Phase A では耐性 petrifyResistance は 0 固定扱い（EnemyData に未追加のため）
    ///   - プレイヤー側の耐性は StatusEffectSystem.GetPlayerResistance(Petrify) を使用
    ///   - 敵の immuneToAllAilments は尊重する
    ///
    /// 【Phase B 以降で追加予定】
    ///   - DEF/MDEF 倍率の適用
    ///   - ターン進行時の残ターン-1（TickPetrifyTurns）
    ///   - 残ターン0到達時の敗北/撃破判定
    /// </summary>
    private static void ProcessPetrifyInflict(
        int chance,
        bool isPlayerAttack,
        Monster enemyMonster,
        List<string> logs)
    {
        string eName = (enemyMonster != null) ? enemyMonster.Mname : "敵";

        if (isPlayerAttack)
        {
            // プレイヤー → 敵に付与
            // immuneToAllAilments チェック（耐性100扱い）
            int resist = StatusEffectSystem.GetEnemyResistance(StatusEffect.Petrify, enemyMonster);
            // Phase A: petrifyResistance は EnemyData に未追加のため、
            // 現状は汎用の GetEnemyResistance(Petrify) が immuneToAllAilments のみ判定する想定。

            bool inflicted = StatusEffectSystem.TryInflict(chance, resist);
            if (!inflicted)
            {
                logs.Add("…しかし効果がなかった！");
                return;
            }

            string msg = BattleSceneController.InflictPetrifyToEnemy();
            logs.Add($"{eName} は{msg}");
        }
        else
        {
            // 敵 → プレイヤーに付与
            int resist = StatusEffectSystem.GetPlayerResistance(StatusEffect.Petrify);
            bool inflicted = StatusEffectSystem.TryInflict(chance, resist);
            if (!inflicted)
            {
                logs.Add("…しかし効果がなかった！");
                return;
            }

            string msg = BattleSceneController.InflictPetrifyToPlayer();
            logs.Add($"You は{msg}");
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
    // バフ/デバフ付与処理（Phase4: 全5種汎用）
    // =========================================================

    /// <summary>
    /// 全5種のバフ/デバフ付与を汎用的に処理する。
    /// 双方向対応（プレイヤー→敵、敵→プレイヤー）。
    ///
    /// 【仕様】
    ///   - Down系（デバフ）: 相手に付与。耐性判定あり。
    ///   - Up系（バフ）: 自分自身に付与。耐性判定なし。
    ///   - 反対効果がかかっている場合: 反対効果を解除してから新効果を付与（後優先）
    ///   - 同じ効果が既にかかっている場合: ターン数をリセット（率は上書き）
    ///   - intValue = 効果率（%）。例: 30 = 30%減少/増加
    ///   - duration = 持続ターン数。0 ならデフォルト値を使用。
    /// </summary>
    private static void ProcessStatBuffDebuff(
        SkillEffectEntry entry,
        bool isPlayerAttack,
        Monster enemyMonster,
        ref BattleBuffState buffState,
        List<string> logs)
    {
        StatusEffect effect = entry.targetStatusEffect;
        bool isDebuff = StatusEffectSystem.IsDebuff(effect);
        float rate = (entry.intValue > 0) ? entry.intValue : 30f;
        int dur = (entry.duration > 0) ? entry.duration : StatusEffectSystem.DefaultBuffDebuffDuration;

        // 表示用名前の取得
        string effectName;
        string oppositeName;
        if (isPlayerAttack)
        {
            // プレイヤーが使用 → 敵にデバフ or 自分にバフ
            // 敵用の表示名を使う場合は ToJapaneseEnemy
            if (isDebuff)
                effectName = effect.ToJapaneseEnemy();
            else
                effectName = effect.ToJapanese();

            StatusEffect opposite = StatusEffectSystem.GetOpposite(effect);
            if (isDebuff)
                oppositeName = opposite.ToJapaneseEnemy();
            else
                oppositeName = opposite.ToJapanese();
        }
        else
        {
            // 敵が使用 → プレイヤーにデバフ or 敵自身にバフ
            if (isDebuff)
                effectName = effect.ToJapanese();
            else
                effectName = effect.ToJapaneseEnemy();

            StatusEffect opposite = StatusEffectSystem.GetOpposite(effect);
            if (isDebuff)
                oppositeName = opposite.ToJapanese();
            else
                oppositeName = opposite.ToJapaneseEnemy();
        }

        string eName = (enemyMonster != null) ? enemyMonster.Mname : "敵";

        if (isDebuff)
        {
            // --- デバフ: 相手に付与 ---
            if (isPlayerAttack)
            {
                // プレイヤー → 敵にデバフ
                int resist = StatusEffectSystem.GetEnemyResistance(effect, enemyMonster);
                if (!StatusEffectSystem.TryInflict(entry.chance, resist))
                {
                    logs.Add("…しかし効果がなかった！");
                    return;
                }

                ref BuffDebuffPair enemyPair = ref buffState.enemy.GetPairRef(effect);
                // 反対効果（バフ）を解除
                if (enemyPair.buffTurn > 0)
                {
                    enemyPair.buffTurn = 0;
                    enemyPair.buffRate = 0f;
                    StatusEffect opposite = StatusEffectSystem.GetOpposite(effect);
                    logs.Add($"{eName} の{oppositeName}が解除された！");
                }

                enemyPair.debuffTurn = dur;
                enemyPair.debuffRate = rate;
                logs.Add($"{eName} の{effectName}！ {rate}%低下！（{dur}ターン）");
            }
            else
            {
                // 敵 → プレイヤーにデバフ
                int resist = StatusEffectSystem.GetPlayerResistance(effect);
                if (!StatusEffectSystem.TryInflict(entry.chance, resist))
                {
                    logs.Add("…しかし効果がなかった！");
                    return;
                }

                ref BuffDebuffPair playerPair = ref buffState.player.GetPairRef(effect);
                // 反対効果（バフ）を解除
                if (playerPair.buffTurn > 0)
                {
                    playerPair.buffTurn = 0;
                    playerPair.buffRate = 0f;
                    logs.Add($"You の{oppositeName}が解除された！");
                }

                playerPair.debuffTurn = dur;
                playerPair.debuffRate = rate;
                logs.Add($"You の{effectName}！ {rate}%低下！（{dur}ターン）");
            }
        }
        else
        {
            // --- バフ: 自分自身に付与（耐性判定なし） ---
            if (isPlayerAttack)
            {
                // プレイヤーが使用 → プレイヤー自身にバフ
                ref BuffDebuffPair playerPair = ref buffState.player.GetPairRef(effect);
                // 反対効果（デバフ）を解除
                if (playerPair.debuffTurn > 0)
                {
                    playerPair.debuffTurn = 0;
                    playerPair.debuffRate = 0f;
                    logs.Add($"You の{oppositeName}が解除された！");
                }

                playerPair.buffTurn = dur;
                playerPair.buffRate = rate;
                logs.Add($"You の{effectName}！ {rate}%上昇！（{dur}ターン）");
            }
            else
            {
                // 敵が使用 → 敵自身にバフ
                ref BuffDebuffPair enemyPair = ref buffState.enemy.GetPairRef(effect);
                // 反対効果（デバフ）を解除
                if (enemyPair.debuffTurn > 0)
                {
                    enemyPair.debuffTurn = 0;
                    enemyPair.debuffRate = 0f;
                    logs.Add($"{eName} の{oppositeName}が解除された！");
                }

                enemyPair.buffTurn = dur;
                enemyPair.buffRate = rate;
                logs.Add($"{eName} の{effectName}！ {rate}%上昇！（{dur}ターン）");
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
                case StatusEffect.Silence:
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

    // =========================================================
    // MPダメージ（敵→プレイヤーのみ）
    // =========================================================

    /// <summary>
    /// MPダメージ効果を処理する。
    /// 敵→プレイヤー方向のみ有効（プレイヤー→敵は発動しない）。
    ///
    /// 【仕様】
    ///   - 固定値（entry.intValue）でそのまま削る
    ///   - MDEFで軽減されない（素通し）
    ///   - MP0でクランプ（マイナスにならない）
    ///   - 発動率は entry.chance（%）
    ///   - 既にMP0の場合: ログなしでスキップ（自然な演出のため）
    /// </summary>
    private static void ProcessMpDamage(
        SkillEffectEntry entry,
        bool isPlayerAttack,
        List<string> logs)
    {
        // プレイヤー→敵方向は発動しない（敵にMP概念なし、ログも出さない）
        if (isPlayerAttack) return;

        // 発動率判定
        if (entry.chance < 100)
        {
            int roll = Random.Range(0, 100);
            if (roll >= entry.chance) return;
        }

        if (GameState.I == null) return;

        int damage = (entry.intValue > 0) ? entry.intValue : 0;
        if (damage <= 0) return;

        int before = GameState.I.currentMp;
        if (before <= 0) return; // 既にMP0なら何もしない（ログも出さない）

        int after = Mathf.Max(0, before - damage);
        GameState.I.currentMp = after;
        int actual = before - after;

        logs.Add($"You のMPが {actual} 減った！");
        Debug.Log($"[SkillEffectProcessor] MPダメージ: {before} → {after} (-{actual})");
    }
}