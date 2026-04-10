using UnityEngine;

/// <summary>
/// BattleSceneController の敵行動パート（partial class）。
/// 敵ターン処理、行動選択（LUC判定）、各種攻撃実行、ターン終了処理を担当する。
///
/// 【先制攻撃システム】
///   BeginPlayerTurn() で敵の行動が事前抽選される。
///   EnemyTurn() では、pendingEnemyAction が残っている場合はそれを使用する。
///   先制技（Preemptive）の場合は PlayerAction 側で既に実行済みなので、
///   EnemyTurn では通常のターン終了処理のみ行う。
///
/// 【NormalAttack 廃止】
///   旧 NormalAttack は SkillAttack に統一。
///   全ての敵攻撃は ExecuteEnemySkillAttack() で処理する。
///   既存アセットの actionType=1（旧NormalAttack）も SkillAttack と同一扱い。
///
/// 【気絶（スタン）】
///   EnemyTurn() で先制チェックの直後にスタンチェックを行う。
///   スタン中は行動スキップ → ターン終了処理（毒ダメージ等）のみ実行。
///   スタンは1ターン限定で自動解除。
///
/// 【麻痺（Phase2追加）】
///   スタンチェック後に麻痺チェック。20%で行動キャンセル。
///
/// 【暗闘（Phase2追加）】
///   敵が暗闇の場合、baseHitRate を半分にする。
///
/// 【怒り（Phase2追加）】
///   敵が怒り中は actionRange = 1（最初のアクション＝通常攻撃のみ）。
///   攻撃力に RageAttackMultiplier（1.5倍）を乗算。
///   AfterEnemyAction で怒りターンカウントダウン。
/// </summary>
public partial class BattleSceneController
{
    // =========================================================
    // 防御コマンド定数（追加）
    // =========================================================
    //
    // 防御中の防御力倍率とダイス成功率。
    //   DefendDefenseMultiplier: 防御力を何倍にするか（2倍）
    //   DefendDiceRange: 防御ダイスの乱数上限（1.5f → 成功率67%）
    //   通常時の diceRange は DefaultDefenseDiceRange = 2.0f（成功率50%）
    // =========================================================

    /// <summary>防御中の防御力倍率。</summary>
    private const int DefendDefenseMultiplier = 2;

    /// <summary>防御中の防御ダイス乱数上限（成功率67%）。</summary>
    private const float DefendDiceRange = 1.5f;

    // =========================================================
    // 敵ターン
    // =========================================================

    /// <summary>
    /// 敵の行動処理。
    ///
    /// 【先制攻撃システム対応】
    ///   pendingEnemyAction が残っている場合:
    ///     - 先制技だった場合: PlayerAction 側で既に実行済みなので、ここでは
    ///       ターン終了処理（毒ダメージ等）のみ行う。
    ///     - 通常技だった場合: 事前抽選済みの行動をそのまま実行する。
    ///   pendingEnemyAction が null の場合:
    ///     - actions 配列があれば新規に抽選して実行する。
    ///     - なければ Legacy 通常攻撃。
    ///
    /// 【気絶（スタン）チェック】
    ///   先制チェック後にスタンチェックを行い、スタン中は行動スキップ。
    ///
    /// 【麻痺チェック（Phase2追加）】
    ///   スタンチェック後に麻痺チェック。20%で行動キャンセル。
    /// </summary>
    private void EnemyTurn()
    {
        if (battleEnded) return;

        // =========================================================
        // 先制済みチェック（最優先）
        // =========================================================
        if (isEnemyPreemptive)
        {
            isEnemyPreemptive = false; // フラグをリセット
            pendingEnemyAction = null; // 念のためクリア
            AfterEnemyAction();
            return;
        }

        // =========================================================
        // 気絶（スタン）チェック
        // =========================================================
        if (enemyIsStunned)
        {
            enemyIsStunned = false; // 1ターンで解除
            enemyForcedNextSkill = null; // 予定行動をキャンセル
            AddLog($"{enemyMonster.Mname} は気絶して動けない！");
            AfterEnemyAction();
            return;
        }

        // =========================================================
        // 麻痺チェック（Phase2追加）
        // =========================================================
        if (enemyIsParalyzed)
        {
            if (StatusEffectSystem.CheckParalyzeCancel())
            {
                enemyForcedNextSkill = null; // 予定行動をキャンセル
                AddLog($"{enemyMonster.Mname} は麻痺して動けない！");
                AfterEnemyAction();
                return;
            }
        }

        // =========================================================
        // 次ターン強制行動チェック（力をためる等）
        // =========================================================
        if (enemyForcedNextSkill != null)
        {
            SkillData forced = enemyForcedNextSkill;
            enemyForcedNextSkill = null; // 消費
                                         // 強制スキルを actions[0] と同じ形式で実行
            ExecuteEnemySkillAttack(forced);
            return;
        }


        // =========================================================
        // 怒り中の敵は通常攻撃のみ（Phase2追加）
        // actionRange を 1 にして最初のアクション（通常攻撃）を強制選択する。
        // =========================================================
        if (enemyRageTurn > 0 && enemyMonster.actions != null && enemyMonster.actions.Length > 0)
        {
            // 最初のアクションを強制使用（通常攻撃想定）
            ExecuteEnemyAction(enemyMonster.actions[0]);
            return;
        }

        // 事前抽選済みの行動がある場合（通常技）
        if (pendingEnemyAction != null)
        {
            EnemyActionEntry pending = pendingEnemyAction;
            pendingEnemyAction = null; // 消費

            // 通常の事前抽選済み行動を実行
            ExecuteEnemyAction(pending);
            return;
        }

        // 事前抽選なし（actions 未設定 or 初回）
        if (enemyMonster.actions == null || enemyMonster.actions.Length == 0)
        {
            ExecuteLegacyAttack();
            return;
        }

        EnemyActionEntry selectedAction = SelectEnemyAction();
        ExecuteEnemyAction(selectedAction);
    }

    /// <summary>
    /// 従来の敵攻撃処理（actions 未設定時のフォールバック）。
    /// Monster.Attack をそのままダメージとして使用する。
    /// 防御ダイスによる軽減を適用する。
    /// プレイヤーが防御中の場合、防御力2倍・ダイス優遇を適用する。
    ///
    /// ※ actions 配列が未設定のモンスター用の安全ネット。
    ///   新規モンスターは必ず actions を設定すること。
    ///
    /// 命中判定:
    ///   Monster.BaseHitRate × (1 - プレイヤー回避率/100)、最低10%。
    ///   敵が暗闇の場合、baseHitRate を半分にする。
    /// </summary>
    private void ExecuteLegacyAttack()
    {
        // Phase2: 暗闇補正
        int hitRate = enemyMonster.BaseHitRate;
        if (enemyIsBlind) hitRate = hitRate / 2;

        if (!CheckEnemyHit(hitRate))
        {
            AddLog($"{enemyMonster.Mname} の攻撃！ …しかし外れた！");
            AfterEnemyAction();
            return;
        }

        // Phase2: 怒り中は攻撃力1.5倍
        int enemyDamage = ApplyEnemyAttackBuffDebuff(enemyMonster.Attack);
        if (enemyRageTurn > 0)
        {
            enemyDamage = Mathf.FloorToInt(enemyDamage * StatusEffectSystem.RageAttackMultiplier + 0.5f);
        }
        if (enemyDamage < 1) enemyDamage = 1;

        int defense = GetPlayerDefense(DamageCategory.Physical);
        int blocked;
        if (isDefending)
        {
            defense *= DefendDefenseMultiplier;
            blocked = RollDefenseDice(defense, DefendDiceRange);
        }
        else
        {
            blocked = RollDefenseDice(defense);
        }
        int finalDamage = enemyDamage - blocked;
        if (finalDamage < 0) finalDamage = 0;

        ApplyDamageToPlayer(finalDamage);

        if (blocked > 0)
            AddLog($"{enemyMonster.Mname} の攻撃！ {finalDamage}ダメージ！（{blocked}軽減）");
        else
            AddLog($"{enemyMonster.Mname} の攻撃！ {finalDamage}ダメージ！");

        AfterEnemyAction();
    }

    /// <summary>
    /// LUC 差に応じた乱数の上限値（actionRange）を計算し、
    /// 行動テーブルから行動を選択する。
    ///
    /// 比較対象:
    ///   プレイヤー側 = GameState.I.baseLUC（生のステータス値）
    ///   敵側         = Monster.Luck
    ///
    /// actionRange の決定ルール（baseActionRange=100 の場合）:
    ///   プレイヤー有利:
    ///     baseLUC が敵Luck より 10以上高い OR baseLUC が敵Luck の 1.5倍以上（四捨五入）
    ///     → actionRange = 80（敵が弱体化）
    ///     ※どちらか片方でも満たせば適用。両方満たす場合も 80。
    ///
    ///   敵有利:
    ///     baseLUC が敵Luck より 10以上低い OR baseLUC が敵Luck の半分未満（四捨五入）
    ///     → actionRange = 120（敵が強化）
    ///     ※どちらか片方でも満たせば適用。両方満たす場合も 120。
    ///
    ///   互角:
    ///     上記どちらの条件も満たさない場合
    ///     → actionRange = baseActionRange（通常100）
    ///
    ///   優先順位:
    ///     プレイヤー有利 と 敵有利 の両方が真になることはロジック上ないが、
    ///     万一の場合はプレイヤー有利（変動が大きい方）を優先する。
    ///
    /// 乱数 0 ～ actionRange-1 を振り、
    /// actions[i].threshold を昇順に走査して、乱数値 < threshold の最初の行動を返す。
    /// </summary>
    private EnemyActionEntry SelectEnemyAction()
    {
        int playerLuc = (GameState.I != null) ? GameState.I.baseLUC : 1;
        int enemyLuc = enemyMonster.Luck;
        int actionRange = CalcActionRange(playerLuc, enemyLuc, enemyMonster.baseActionRange);
        int roll = Random.Range(0, actionRange);

        Debug.Log($"[Battle] EnemyAction: playerLuc={playerLuc} enemyLuc={enemyLuc} " +
                  $"baseRange={enemyMonster.baseActionRange} actionRange={actionRange} roll={roll}");

        for (int i = 0; i < enemyMonster.actions.Length; i++)
        {
            if (roll < enemyMonster.actions[i].threshold)
                return enemyMonster.actions[i];
        }
        return enemyMonster.actions[enemyMonster.actions.Length - 1];
    }

    /// <summary>
    /// プレイヤーと敵の Luck を比較し、行動判定の乱数上限値を返す。
    /// </summary>
    private int CalcActionRange(int playerLuc, int enemyLuc, int baseRange)
    {
        int advByRatio = Mathf.FloorToInt(enemyLuc * 1.5f + 0.5f);
        int advByFixed = enemyLuc + 10;
        int advThreshold = Mathf.Max(advByRatio, advByFixed);

        int disadvByRatio = Mathf.FloorToInt(enemyLuc * 0.5f + 0.5f);
        int disadvByFixed = enemyLuc - 10;
        int disadvThreshold = Mathf.Max(disadvByRatio, disadvByFixed);

        if (playerLuc >= advThreshold)
        {
            int range = Mathf.FloorToInt(baseRange * 0.8f + 0.5f);
            Debug.Log($"[Battle] LUC判定: プレイヤー有利 " +
                      $"playerLuc={playerLuc} >= {advThreshold}(ratio={advByRatio},fixed={advByFixed}) " +
                      $"actionRange={range}");
            return range;
        }

        if (playerLuc < disadvThreshold)
        {
            int range = Mathf.FloorToInt(baseRange * 1.2f + 0.5f);
            Debug.Log($"[Battle] LUC判定: 敵有利 " +
                      $"playerLuc={playerLuc} < {disadvThreshold}(ratio={disadvByRatio},fixed={disadvByFixed}) " +
                      $"actionRange={range}");
            return range;
        }

        Debug.Log($"[Battle] LUC判定: 互角 playerLuc={playerLuc} " +
                  $"advThreshold={advThreshold} disadvThreshold={disadvThreshold} " +
                  $"actionRange={baseRange}");
        return baseRange;
    }

    /// <summary>
    /// 選択された敵行動を実行する。
    /// </summary>
    private void ExecuteEnemyAction(EnemyActionEntry action)
    {
        if (action.skill == null)
        {
            Debug.LogWarning("[Battle] EnemyActionEntry.skill が null です。通常攻撃で代替します。");
            ExecuteLegacyAttack();
            return;
        }
        switch (action.skill.actionType)
        {
#pragma warning disable 0618 // Obsolete 警告を抑制（既存アセット互換のため）
            case MonsterActionType.NormalAttack: ExecuteEnemySkillAttack(action.skill); break;
#pragma warning restore 0618
            case MonsterActionType.SkillAttack: ExecuteEnemySkillAttack(action.skill); break;
            case MonsterActionType.Preemptive: ExecuteEnemySkillAttack(action.skill); break;
            case MonsterActionType.Idle: ExecuteEnemyIdle(action.skill); break;
            default: ExecuteEnemyIdle(action.skill); break;
        }

        // ExecuteEnemyAction の末尾付近に追加
        if (action.skill.enemyNextForceSkill != null)
        {
            enemyForcedNextSkill = action.skill.enemyNextForceSkill;
        }
    }

    /// <summary>
    /// 敵のスキル攻撃。SkillData のパラメータでダメージ計算する。
    /// Phase2: 敵が暗闇の場合、baseHitRate を半分にする。
    /// Phase2: 敵が怒り中の場合、攻撃力に RageAttackMultiplier を乗算する。
    /// </summary>
    private void ExecuteEnemySkillAttack(SkillData skill)
    {
        // Phase2: 暗闘補正 — 敵が暗闇なら命中率半分
        int effectiveHitRate = skill.baseHitRate;
        if (enemyIsBlind) effectiveHitRate = effectiveHitRate / 2;

        // =========================================================
        // 非ダメージスキル: 命中判定をスキップし、追加効果のみ実行
        // （ヒール等の自己対象スキルは命中判定不要）
        // =========================================================
        if (skill.IsNonDamage)
        {
            string effectSkillName = !string.IsNullOrEmpty(skill.skillName) ? skill.skillName : "スキル";

            // 敵対象の非ダメージスキル（毒付与等）は回避判定を行う
            if (skill.IsHostileNonDamage)
            {
                if (!CheckEnemyHit(effectiveHitRate))
                {
                    AddLog($"{enemyMonster.Mname} の{effectSkillName}！ …しかし外れた！");
                    AfterEnemyAction();
                    return;
                }
            }

            AddLog($"{enemyMonster.Mname} の{effectSkillName}！");
            ProcessEnemySkillEffects(skill);
            AfterEnemyAction();
            return;
        }

        // --- ここから先はダメージスキルのみ ---
        if (!CheckEnemyHit(effectiveHitRate))
        {
            string missName = !string.IsNullOrEmpty(skill.skillName)
                ? skill.skillName
                : $"{skill.skillAttribute.ToJapanese()}攻撃";
            AddLog($"{enemyMonster.Mname} の{missName}！ …しかし外れた！");
            AfterEnemyAction();
            return;
        }

        // =========================================================
        // 乱数ダメージスキル（追加）
        // randomDamageMax > 0 の場合、1〜maxの乱数がベースダメージ。
        // クリティカル無効、防御ダイス有効。属性は None を想定。
        // =========================================================
        if (skill.randomDamageMax > 0)
        {
            int rndBase = Random.Range(1, skill.randomDamageMax + 1);
            string rndName = !string.IsNullOrEmpty(skill.skillName)
                ? skill.skillName : "攻撃";

            // 防御ダイス
            int defense = GetPlayerDefense(skill.damageCategory);

            if (skill.defenseIgnoreRate > 0f)
            {
                defense = Mathf.FloorToInt(defense * (1f - skill.defenseIgnoreRate) + 0.5f);
            }

            int blocked;
            if (isDefending)
            {
                defense *= DefendDefenseMultiplier;
                blocked = RollDefenseDice(defense, DefendDiceRange);
            }
            else
            {
                blocked = RollDefenseDice(defense);
            }
            int finalDmg = rndBase - blocked;
            if (finalDmg < 0) finalDmg = 0;

            ApplyDamageToPlayer(finalDmg);

            string blockLog = blocked > 0 ? $"（防御{blocked}軽減）" : "";
            AddLog($"{enemyMonster.Mname} の{rndName}！ {finalDmg}ダメージ！（乱数{rndBase}）{blockLog}");

            Debug.Log($"[Battle] RandomDamage: roll={rndBase} max={skill.randomDamageMax} " +
                      $"defense={defense} blocked={blocked} final={finalDmg}");

            ProcessEnemySkillEffects(skill);
            AfterEnemyAction();
            return;
        }


        // --- ダメージ計算（多段攻撃対応） ---
        int hits = skill.EffectiveHitCount;
        int totalDamage = 0;
        int hitSuccess = 0;

        if (hits > 1)
        {
            string multiName = !string.IsNullOrEmpty(skill.skillName) ? skill.skillName : "攻撃";
            AddLog($"{enemyMonster.Mname} の{multiName}！（{hits}回攻撃）");
        }

        for (int h = 0; h < hits; h++)
        {
            // 多段攻撃の2発目以降は個別に命中判定（暗闇補正付き）
            if (h > 0 && !CheckEnemyHit(effectiveHitRate))
            {
                AddLog($"  {h + 1}撃目 …外れた！");
                continue;
            }

            int baseDamage;
            if (skill.damageMultiplier > 0f)
            {
                int attackPower = ApplyEnemyAttackBuffDebuff(enemyMonster.Attack);
                // Phase2: 怒り中は攻撃力1.5倍
                if (enemyRageTurn > 0)
                {
                    attackPower = Mathf.FloorToInt(attackPower * StatusEffectSystem.RageAttackMultiplier + 0.5f);
                }
                baseDamage = Mathf.FloorToInt(attackPower * skill.damageMultiplier + 0.5f);
            }
            else
                baseDamage = 0;
            baseDamage += skill.bonusDamage;
            if (baseDamage < 1) baseDamage = 1;

            int resistance = PassiveCalculator.CalcTotalAttributeResistance(skill.skillAttribute);
            float reductionRate = resistance / 100f;
            int afterResist = Mathf.FloorToInt(baseDamage * (1f - reductionRate) + 0.5f);
            if (afterResist < 0) afterResist = 0;

            int defense = GetPlayerDefense(skill.damageCategory);

            if (skill.defenseIgnoreRate > 0f)
            {
                defense = Mathf.FloorToInt(defense * (1f - skill.defenseIgnoreRate) + 0.5f);
            }

            int blocked;
            if (isDefending)
            {
                defense *= DefendDefenseMultiplier;
                blocked = RollDefenseDice(defense, DefendDiceRange);
            }
            else
            {
                blocked = RollDefenseDice(defense);
            }
            int finalDamage = afterResist - blocked;
            if (finalDamage < 0) finalDamage = 0;

            ApplyDamageToPlayer(finalDamage);
            totalDamage += finalDamage;
            hitSuccess++;

            if (hits > 1)
            {
                string logSuffix = "";
                if (resistance > 0 && blocked > 0) logSuffix = $"（耐性+防御{blocked}軽減）";
                else if (resistance > 0) logSuffix = "（耐性で軽減）";
                else if (resistance < 0) logSuffix = "（弱点で増加）";
                else if (blocked > 0) logSuffix = $"（防御{blocked}軽減）";
                AddLog($"  {h + 1}撃目 {finalDamage}ダメージ！{logSuffix}");
            }
            else
            {
                // 単発攻撃: 従来ログ
                string actionName = !string.IsNullOrEmpty(skill.skillName)
                    ? skill.skillName
                    : $"{skill.skillAttribute.ToJapanese()}攻撃";

                string logSuffix = "";
                if (resistance > 0 && blocked > 0) logSuffix = $"（耐性で軽減+防御{blocked}軽減）";
                else if (resistance > 0) logSuffix = "（耐性で軽減）";
                else if (resistance < 0) logSuffix = "（弱点で増加）";
                else if (blocked > 0) logSuffix = $"（防御{blocked}軽減）";

                AddLog($"{enemyMonster.Mname} の{actionName}！（{skill.skillAttribute.ToJapanese()}属性） " +
                       $"{finalDamage}ダメージ！{logSuffix}");
            }

            Debug.Log($"[Battle] SkillAttack hit {h + 1}/{hits}: base={baseDamage} resistance={resistance} " +
                      $"afterResist={afterResist} defense={defense} blocked={blocked} final={finalDamage} defending={isDefending}");

            // 途中でプレイヤーが倒れたら残りをスキップ
            if (GameState.I != null && GameState.I.currentHp <= 0) break;
        }

        // 多段攻撃の合計ログ
        if (hits > 1)
        {
            AddLog($"  → 合計 {totalDamage}ダメージ！（{hitSuccess}/{hits}命中）");
        }

        // 追加効果の実行（全ヒット完了後に1回だけ）
        ProcessEnemySkillEffects(skill);

        AfterEnemyAction();

    }

    /// <summary>
    /// 敵が何もしない。
    /// </summary>
    private void ExecuteEnemyIdle(SkillData skill)
    {
        string actionName = !string.IsNullOrEmpty(skill.skillName) ? skill.skillName : "様子を見ている";
        AddLog($"{enemyMonster.Mname} は{actionName}…");
        AfterEnemyAction();
    }

    /// <summary>
    /// 敵スキルの追加効果を実行する共通メソッド。
    /// </summary>
    private void ProcessEnemySkillEffects(SkillData skill)
    {
        if (!skill.HasAdditionalEffects) return;

        var logs = SkillEffectProcessor.ProcessEffects(
            skill.additionalEffects,
            isPlayerAttack: false,
            enemyMonster,
            ref enemyIsPoisoned,
            ref enemyIsStunned,
            ref enemyCurrentHp,
            0,
            ref enemyIsParalyzed,
            ref enemyIsBlind,
            ref enemyRageTurn,
            ref playerRageTurn,
            ref buffState);

        for (int i = 0; i < logs.Count; i++)
        {
            AddLog(logs[i]);
        }

        RefreshBattleStatusEffectUI();
    }
    /// <summary>
    /// 敵の行動後の共通処理。
    /// ターン終了時の毒ダメージを適用し、
    /// 怒りターンカウントダウンを行い、
    /// プレイヤー敗北判定を行い、生存していればプレイヤーターンに戻す。
    /// </summary>
    private void AfterEnemyAction()
    {
        // =========================================================
        // ターン終了時の毒ダメージ
        // プレイヤーと敵の両方に毒ダメージを適用する
        // =========================================================

        // --- プレイヤーの毒ダメージ ---
        int playerPoisonDmg = StatusEffectSystem.ApplyBattlePoisonToPlayer();
        if (playerPoisonDmg > 0)
        {
            AddLog($"You は毒のダメージで {playerPoisonDmg} 受けた！");
        }

        // --- 敵の毒ダメージ ---
        if (enemyIsPoisoned && enemyMonster != null)
        {
            int enemyPoisonDmg = StatusEffectSystem.CalcBattlePoisonDamage(enemyMonster.MaxHp);
            enemyCurrentHp -= enemyPoisonDmg;
            if (enemyCurrentHp < 0) enemyCurrentHp = 0;
            AddLog($"{enemyMonster.Mname} は毒のダメージで {enemyPoisonDmg} 受けた！");

            if (enemyCurrentHp <= 0)
            {
                // 毒で敵が倒れた → ログを表示してから勝利処理
                FlushLogsAndThen(() => OnVictory());
                return;
            }
        }

        // =========================================================
        // 怒りターンカウントダウン（Phase2追加）
        // =========================================================
        if (enemyRageTurn > 0)
        {
            enemyRageTurn--;
            if (enemyRageTurn <= 0)
            {
                AddLog($"{enemyMonster.Mname} の怒りが収まった。");
            }
        }
        if (playerRageTurn > 0)
        {
            playerRageTurn--;
            if (playerRageTurn <= 0)
            {
                AddLog("You の怒りが収まった。");
            }
        }

        // =========================================================
        // バフ/デバフ ターンカウントダウン（Phase4: 全5種一括）
        // =========================================================
        {
            var buffLogs = TickBuffDebuffTurns();
            for (int bl = 0; bl < buffLogs.Count; bl++)
            {
                AddLog(buffLogs[bl]);
            }
        }


        // ログを全部表示してから勝敗判定・ターン移行
        FlushLogsAndThen(() =>
        {
            // プレイヤー敗北判定
            if (GameState.I != null && GameState.I.currentHp <= 0)
            {
                OnDefeat();
                return;
            }

            // プレイヤーターンに戻す
            SetButtonsInteractable(true);
            RefreshSkillButton();
            RefreshMagicSelector();
            RefreshBattleStatusEffectUI();
        });
    }
}