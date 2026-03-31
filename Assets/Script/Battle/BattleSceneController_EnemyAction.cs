using UnityEngine;

/// <summary>
/// BattleSceneController の敵行動パート（partial class）。
/// 敵ターン処理、行動選択（LUC判定）、各種攻撃実行、ターン終了処理を担当する。
/// </summary>
public partial class BattleSceneController
{
    // =========================================================
    // 敵ターン
    // =========================================================

    /// <summary>
    /// 敵の行動処理。
    /// Monster に actions 配列が設定されている場合は行動テーブルに従い、
    /// 未設定の場合は従来通り Attack 依存の通常攻撃を行う。
    /// </summary>
    private void EnemyTurn()
    {
        if (battleEnded) return;
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
    ///
    /// 命中判定（追加）:
    ///   Monster.BaseHitRate × (1 - プレイヤー回避率/100)、最低10%。
    /// </summary>
    private void ExecuteLegacyAttack()
    {
        if (!CheckEnemyHit(enemyMonster.BaseHitRate))
        {
            AddLog($"{enemyMonster.Mname} の攻撃！ …しかし外れた！");
            AfterEnemyAction();
            return;
        }

        int enemyDamage = enemyMonster.Attack;
        if (enemyDamage < 1) enemyDamage = 1;

        int defense = GetPlayerDefense(DamageCategory.Physical);
        int blocked = RollDefenseDice(defense);
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
    ///
    /// 閾値の決定:
    ///   「倍率による閾値」と「固定差（±10）による閾値」を両方計算し、
    ///   大きい方を採用する。
    ///
    ///   プレイヤー有利の閾値 = max( enemyLuc×1.5（四捨五入）, enemyLuc+10 )
    ///     → playerLuc がこの値以上なら、actionRange = baseRange × 0.8
    ///
    ///   敵有利の閾値       = max( enemyLuc×0.5（四捨五入）, enemyLuc-10 )
    ///     → playerLuc がこの値未満なら、actionRange = baseRange × 1.2
    ///
    /// 切り替わりの境界:
    ///   敵LUC  0～19 → +10 / -10 の固定差が優先（序盤安定）
    ///   敵LUC 20     → 同値
    ///   敵LUC 21以上  → ×1.5 / ×0.5 の倍率が優先（高LUC帯スケール）
    ///
    /// 矛盾（両条件同時成立）は発生しないことを検証済み。
    /// 万一の場合はプレイヤー有利を優先する。
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
    /// EnemyActionEntry.skill が null の場合は通常攻撃（物理）にフォールバックする。
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
            case MonsterActionType.NormalAttack: ExecuteEnemyNormalAttack(action.skill); break;
            case MonsterActionType.SkillAttack: ExecuteEnemySkillAttack(action.skill); break;
            case MonsterActionType.Idle: ExecuteEnemyIdle(action.skill); break;
            default: ExecuteEnemyIdle(action.skill); break;
        }
    }

    /// <summary>
    /// 敵の通常攻撃。Monster.Attack 依存ダメージ。
    /// skill.damageCategory に応じて物理防御 or 魔法防御のダイスを適用する。
    ///
    /// 命中判定:
    ///   skill.baseHitRate × (1 - プレイヤー回避率/100)、最低10%。
    ///
    /// 追加効果:
    ///   ダメージ適用後に additionalEffects を実行する。
    /// </summary>
    private void ExecuteEnemyNormalAttack(SkillData skill)
    {
        if (!CheckEnemyHit(skill.baseHitRate))
        {
            string actionName = !string.IsNullOrEmpty(skill.skillName) ? skill.skillName : "攻撃";
            AddLog($"{enemyMonster.Mname} の{actionName}！ …しかし外れた！");
            AfterEnemyAction();
            return;
        }

        int enemyDamage = enemyMonster.Attack;
        if (enemyDamage < 1) enemyDamage = 1;

        int defense = GetPlayerDefense(skill.damageCategory);
        int blocked = RollDefenseDice(defense);
        int finalDamage = enemyDamage - blocked;
        if (finalDamage < 0) finalDamage = 0;

        ApplyDamageToPlayer(finalDamage);

        string actionName2 = !string.IsNullOrEmpty(skill.skillName) ? skill.skillName : "攻撃";
        if (blocked > 0)
            AddLog($"{enemyMonster.Mname} の{actionName2}！ {finalDamage}ダメージ！（{blocked}軽減）");
        else
            AddLog($"{enemyMonster.Mname} の{actionName2}！ {finalDamage}ダメージ！");

        // 追加効果の実行
        ProcessEnemySkillEffects(skill);

        AfterEnemyAction();
    }

    /// <summary>
    /// 敵のスキル攻撃。SkillData のパラメータでダメージ計算する。
    ///
    /// 非ダメージスキル判定:
    ///   IsNonDamage == true の場合はダメージ計算をスキップし、
    ///   追加効果のみ実行する。
    ///
    /// ダメージ計算:
    ///   1. fixedDamage > 0 ならそれを使用
    ///      damageMultiplier > 0 なら Monster.Attack × damageMultiplier（四捨五入）
    ///      どちらも 0 なら非ダメージスキル（上で処理済み）
    ///   2. resistance = PassiveCalculator.CalcTotalAttributeResistance(attackAttribute)
    ///      → afterResist = baseDamage × (1 - resistance / 100)
    ///   3. 防御ダイスで軽減
    ///
    /// 命中判定:
    ///   skill.baseHitRate × (1 - プレイヤー回避率/100)、最低10%。
    /// </summary>
    private void ExecuteEnemySkillAttack(SkillData skill)
    {
        if (!CheckEnemyHit(skill.baseHitRate))
        {
            string missName = !string.IsNullOrEmpty(skill.skillName)
                ? skill.skillName
                : $"{skill.skillAttribute.ToJapanese()}攻撃";
            AddLog($"{enemyMonster.Mname} の{missName}！ …しかし外れた！");
            AfterEnemyAction();
            return;
        }

        // =========================================================
        // 非ダメージスキル: 追加効果のみ実行
        // =========================================================
        if (skill.IsNonDamage)
        {
            string effectSkillName = !string.IsNullOrEmpty(skill.skillName) ? skill.skillName : "スキル";
            AddLog($"{enemyMonster.Mname} の{effectSkillName}！");

            // 追加効果の実行
            ProcessEnemySkillEffects(skill);

            AfterEnemyAction();
            return;
        }

        // --- 通常のダメージ計算 ---
        int baseDamage;
        if (skill.fixedDamage > 0) baseDamage = skill.fixedDamage;
        else if (skill.damageMultiplier > 0f) baseDamage = Mathf.FloorToInt(enemyMonster.Attack * skill.damageMultiplier + 0.5f);
        else baseDamage = enemyMonster.Attack;
        if (baseDamage < 1) baseDamage = 1;

        int resistance = PassiveCalculator.CalcTotalAttributeResistance(skill.skillAttribute);
        float reductionRate = resistance / 100f;
        int afterResist = Mathf.FloorToInt(baseDamage * (1f - reductionRate) + 0.5f);
        if (afterResist < 0) afterResist = 0;

        int defense = GetPlayerDefense(skill.damageCategory);
        int blocked = RollDefenseDice(defense);
        int finalDamage = afterResist - blocked;
        if (finalDamage < 0) finalDamage = 0;

        ApplyDamageToPlayer(finalDamage);

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

        Debug.Log($"[Battle] SkillAttack: base={baseDamage} resistance={resistance} " +
                  $"afterResist={afterResist} defense={defense} blocked={blocked} final={finalDamage}");

        // 追加効果の実行
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
    /// SkillEffectProcessor を呼び出し、結果のログを追加する。
    /// </summary>
    private void ProcessEnemySkillEffects(SkillData skill)
    {
        if (!skill.HasAdditionalEffects) return;

        var logs = SkillEffectProcessor.ProcessEffects(
            skill.additionalEffects,
            isPlayerAttack: false,
            enemyMonster,
            ref enemyIsPoisoned,
            ref enemyCurrentHp);

        for (int i = 0; i < logs.Count; i++)
        {
            AddLog(logs[i]);
        }
    }

    /// <summary>
    /// 敵の行動後の共通処理。
    /// ターン終了時の毒ダメージを適用し、
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
                OnVictory();
                return;
            }
        }

        // プレイヤー敗北判定
        if (GameState.I != null && GameState.I.currentHp <= 0)
        {
            OnDefeat();
            return;
        }

        // プレイヤーターンに戻す
        SetButtonsInteractable(true);
        RefreshSkillButton();
        RefreshMagicDropdown();
    }
}