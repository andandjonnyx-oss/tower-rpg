using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// BattleSceneController のプレイヤー行動パート（partial class）。
/// 通常攻撃、武器スキル、魔法スキル、防御、アイテム使用を担当する。
///
/// 【先制攻撃システム】
///   BeginPlayerTurn() で敵の行動が事前抽選され、先制技が選ばれた場合
///   isEnemyPreemptive == true となる。
///   各プレイヤー行動メソッド（OnAttackClicked 等）では、プレイヤーの行動処理の前に
///   ExecutePreemptiveAttack() を呼び出して先制割り込みを処理する。
///   先制でプレイヤーが倒された場合はプレイヤー行動をスキップする。
///
/// 【麻痺（Phase2追加）】
///   各行動冒頭で麻痺チェック。20%で行動キャンセル→敵ターンへ。
///
/// 【暗闇（Phase2追加）】
///   プレイヤーが暗闘の場合、敵回避力を2倍として命中計算する。
///   → CheckPlayerHitWithBlind() を使用。
///
/// 【怒り（Phase2追加）】
///   怒り中はスキル/魔法/アイテム/防御ボタンの押下を拒否、攻撃のみ許可。
///   攻撃力に RageAttackMultiplier（1.5倍）を乗算。
///
/// 【多段攻撃の命中判定ルール】
///   hitCount > 1 の多段攻撃スキルでは、スキル発動自体は100%確定。
///   各ヒットごとに個別に命中/回避判定を行う。
///   （単発スキルでは従来通りスキル発動前に命中判定を行う）
/// </summary>
public partial class BattleSceneController
{
    // =========================================================
    // プレイヤーターン
    // =========================================================

    /// <summary>
    /// プレイヤーが行動する直前に呼ぶ共通処理。
    /// インベントリ内の全武器のクールダウンを 1 進める。
    /// （装備していない武器もクールダウンが進む）
    /// </summary>
    private void TickAllWeaponCooldowns()
    {
        if (ItemBoxManager.Instance == null) return;
        var items = ItemBoxManager.Instance.GetItems();
        if (items == null) return;
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] != null && items[i].data != null &&
                items[i].data.category == ItemCategory.Weapon)
                items[i].TickCooldowns();
        }
    }

    // =========================================================
    // 暗闇対応の命中判定ラッパー（Phase2追加）
    // =========================================================

    /// <summary>
    /// プレイヤー攻撃の命中判定。暗闇の場合、敵の回避力を2倍にして計算する。
    /// CombatUtils.CheckPlayerHit() の内部では enemyMonster.Evasion を直接参照するため、
    /// ここでは暗闇時に hitRate そのものを補正するのではなく、
    /// CombatUtils 側で暗闇を考慮した CheckPlayerHitBlind() を呼ぶ。
    ///
    /// 設計判断: CombatUtils を変更せず、呼び出し側で敵回避力を2倍換算する。
    /// → baseHitRate × (1 - (enemyEvasion×2 - accuracy)/100) で実質命中率が下がる。
    /// </summary>
    private bool CheckPlayerHitWithBlind(int baseHitRate)
    {
        if (GameState.I != null && GameState.I.isBlind)
        {
            // 暗闇: 敵回避力2倍として計算
            // CheckPlayerHit 内部で enemyMonster.Evasion を参照するため、
            // ここでは baseHitRate を実質的に下げて同じ効果を実現する。
            // 計算: hitChance = baseHitRate × (1 - (evasion*2 - accuracy)/100)
            //       = baseHitRate × (1 - evasion/100 - evasion/100 + accuracy/100)
            //       = [baseHitRate × (1 - (evasion - accuracy)/100)] - baseHitRate × evasion/100
            // → 簡易実装: baseHitRate を半分にして通常の CheckPlayerHit を呼ぶ
            //   これは「暗闇で命中率が半分になる」のと概ね同等の効果。
            //   ただし敵回避力2倍の方が仕様に忠実なので、CombatUtils に委譲する。
            return CheckPlayerHitBlindInternal(baseHitRate);
        }
        return CheckPlayerHit(baseHitRate);
    }

    /// <summary>
    /// 暗闇時の命中判定（内部）。敵回避力を2倍にして計算する。
    /// CheckPlayerHit と同じロジックだが、enemyEvasion を2倍にする。
    /// </summary>
    private bool CheckPlayerHitBlindInternal(int baseHitRate)
    {
        int playerAccuracy = (GameState.I != null) ? GameState.I.Accuracy : 0;
        int enemyEvasion = (enemyMonster != null) ? enemyMonster.Evasion * 2 : 0; // ★暗闇: 2倍

        float hitChance = baseHitRate * (1f - (enemyEvasion - playerAccuracy) / 100f);

        if (hitChance < 25f) hitChance = 25f;
        if (hitChance > 100f) hitChance = 100f;

        float roll = Random.Range(0f, 100f);
        bool hit = roll < hitChance;

        Debug.Log($"[Battle] PlayerHitCheck(Blind): baseHit={baseHitRate} accuracy={playerAccuracy} " +
                  $"enemyEvasion={enemyEvasion}(x2) hitChance={hitChance:F2}% roll={roll:F2} hit={hit}");

        return hit;
    }

    // =========================================================
    // 麻痺チェック共通処理（Phase2追加）
    // =========================================================

    /// <summary>
    /// プレイヤーの麻痺による行動キャンセルを判定する。
    /// 麻痺中は 20% で行動がキャンセルされ、敵ターンに移行する。
    /// </summary>
    /// <returns>true: 行動キャンセル（呼び出し元は return すべき）</returns>
    private bool CheckPlayerParalyze()
    {
        if (GameState.I == null) return false;
        if (!GameState.I.isParalyzed) return false;

        if (StatusEffectSystem.CheckParalyzeCancel())
        {
            AddLog("You は麻痺して動けない！");
            FlushLogsAndThen(() => EnemyTurn());
            return true;
        }
        return false;
    }

    // =========================================================
    // 先制攻撃割り込み処理（追加）
    // =========================================================

    /// <summary>
    /// 先制攻撃の割り込みを実行する。
    /// isEnemyPreemptive == true の場合、プレイヤー行動の前に敵先制技を実行する。
    /// 先制フラグは実行後にリセットする。
    /// </summary>
    /// <returns>
    /// true: 先制攻撃が発動し、プレイヤーが倒された（プレイヤー行動をスキップすべき）。
    /// false: 先制攻撃なし、または先制後もプレイヤーは生存（行動を続行）。
    /// </returns>
    private bool ExecutePreemptiveIfNeeded()
    {
        if (!isEnemyPreemptive || pendingEnemyAction == null) return false;

        // 先制技のアクションを取り出す（pendingEnemyAction は消費済みにする）
        // ★ isEnemyPreemptive は EnemyTurn 側でリセットする。
        //   こうすることで EnemyTurn が「このターンは先制済み → 通常行動スキップ」
        //   と判定できる。ミスしても通常攻撃に切り替わらない。
        EnemyActionEntry preemptiveAction = pendingEnemyAction;
        pendingEnemyAction = null; // 消費済み

        AddLog($"▶ {enemyMonster.Mname} の先制攻撃！");

        // 先制技を実行（Preemptive は SkillAttack と同じダメージ計算）
        ExecutePreemptiveAction(preemptiveAction);

        // プレイヤーが倒されたかチェック
        if (GameState.I != null && GameState.I.currentHp <= 0)
        {
            // 先制でプレイヤーが倒された → 敵の自爆チェック後に敗北処理
            if (enemyCurrentHp <= 0)
            {
                // 敵も自爆で死んだ → 勝利扱い（先に敵が倒れた扱い）
                FlushLogsAndThen(() => OnVictory());
            }
            else
            {
                FlushLogsAndThen(() => OnDefeat());
            }
            return true; // プレイヤー行動スキップ
        }

        // 敵が自爆で倒れた場合
        if (enemyCurrentHp <= 0)
        {
            FlushLogsAndThen(() => OnVictory());
            return true; // プレイヤー行動スキップ
        }

        // 先制後もプレイヤーは生存 → 通常通りプレイヤー行動を続行
        return false;
    }

    /// <summary>
    /// 先制技を実行する。SkillAttack と同じダメージ計算を行う。
    /// ターン終了処理（毒ダメージ等）は行わない（プレイヤー行動後に行う）。
    ///
    /// 非ダメージスキル:
    ///   IsNonDamage == true の場合は命中判定をスキップし、追加効果のみ実行する。
    ///
    /// 多段攻撃:
    ///   hitCount > 1 の場合、スキル発動自体は確定し、各ヒットごとに命中判定を行う。
    /// </summary>
    private void ExecutePreemptiveAction(EnemyActionEntry action)
    {
        if (action.skill == null)
        {
            Debug.LogWarning("[Battle] 先制攻撃のスキルが null です。スキップします。");
            return;
        }

        SkillData skill = action.skill;

        // Phase2: 暗闇補正（先制攻撃にも適用）
        int effectiveHitRate = skill.baseHitRate;
        if (enemyIsBlind) effectiveHitRate = effectiveHitRate / 2;

        // =========================================================
        // 非ダメージスキル: 命中判定をスキップし、追加効果のみ実行
        // （ヒール等の自己対象スキルは命中判定不要）
        // =========================================================
        if (skill.IsNonDamage)
        {
            string effectSkillName = !string.IsNullOrEmpty(skill.skillName) ? skill.skillName : "先制スキル";

            // 敵対象の非ダメージ先制スキルは回避判定を行う
            if (skill.IsHostileNonDamage)
            {
                if (!CheckEnemyHit(effectiveHitRate))
                {
                    AddLog($"{enemyMonster.Mname} の{effectSkillName}！ …しかし外れた！");
                    return;
                }
            }

            AddLog($"{enemyMonster.Mname} の{effectSkillName}！");
            ProcessEnemySkillEffects(skill);
            return;
        }

        // --- ここから先はダメージスキルのみ ---

        // =========================================================
        // 多段攻撃対応: hitCount > 1 の場合はスキル発動確定、各ヒットで個別判定
        // =========================================================
        int hits = skill.EffectiveHitCount;

        // 単発スキル: 従来通りスキル発動前に命中判定
        if (hits <= 1)
        {
            if (!CheckEnemyHit(effectiveHitRate))
            {
                string missName = !string.IsNullOrEmpty(skill.skillName)
                    ? skill.skillName : "先制攻撃";
                AddLog($"{enemyMonster.Mname} の{missName}！ …しかし外れた！");
                return;
            }
        }

        // 乱数ダメージスキル（追加）
        if (skill.randomDamageMax > 0)
        {
            int rndBase = Random.Range(1, skill.randomDamageMax + 1);
            string rndName = !string.IsNullOrEmpty(skill.skillName)
                ? skill.skillName : "先制攻撃";

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

            ProcessEnemySkillEffects(skill);
            return;
        }

        // =========================================================
        // HP依存ダメージスキル（先制攻撃版）
        // =========================================================
        if (skill.IsHpDependent)
        {

            if (skill.hpDependentType == HpDependentType.CurrentHpDamage)
            {
                string cdName = !string.IsNullOrEmpty(skill.skillName)
                    ? skill.skillName : "先制攻撃";

                if (!CheckEnemyHit(effectiveHitRate))
                {
                    AddLog($"{enemyMonster.Mname} の{cdName}！ …しかし外れた！");
                    return;
                }

                int baseDamage = enemyCurrentHp;
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

                string logSuffix = "";
                if (resistance > 0 && blocked > 0) logSuffix = $"（耐性+防御{blocked}軽減）";
                else if (resistance > 0) logSuffix = "（耐性で軽減）";
                else if (resistance < 0) logSuffix = "（弱点で増加）";
                else if (blocked > 0) logSuffix = $"（防御{blocked}軽減）";

                AddLog($"{enemyMonster.Mname} の{cdName}！ {finalDamage}ダメージ！（残りHP{baseDamage}）{logSuffix}");

                Debug.Log($"[Battle] CurrentHpDamage(Preemptive): userHp={baseDamage} " +
                          $"resistance={resistance} afterResist={afterResist} " +
                          $"defense={defense} blocked={blocked} final={finalDamage}");

                ProcessEnemySkillEffects(skill);
                return;
            }

            string hpDepName = !string.IsNullOrEmpty(skill.skillName)
                ? skill.skillName : "先制攻撃";

            if (!CheckEnemyHit(effectiveHitRate))
            {
                AddLog($"{enemyMonster.Mname} の{hpDepName}！ …しかし外れた！");
                return;
            }

            int playerHp = (GameState.I != null) ? GameState.I.currentHp : 0;
            int hpDamage = CalcHpDependentDamage(skill.hpDependentType, playerHp,
                            (GameState.I != null) ? GameState.I.maxHp : 0, skill.hpDependentPercent);

            ApplyDamageToPlayer(hpDamage);

            string hpDepLog;
            switch (skill.hpDependentType)
            {
                case HpDependentType.HalfCurrentHp: hpDepLog = "（HP半減）"; break;
                case HpDependentType.ReduceToOne: hpDepLog = "（HP→1）"; break;
                case HpDependentType.MaxHpPercent: hpDepLog = $"（最大HPの{skill.hpDependentPercent}%）"; break;
                default: hpDepLog = ""; break;
            }
            AddLog($"{enemyMonster.Mname} の{hpDepName}！ {hpDamage}ダメージ！{hpDepLog}");

            Debug.Log($"[Battle] HpDependent(Preemptive): type={skill.hpDependentType} " +
                      $"beforeHp={playerHp} damage={hpDamage}");

            ProcessEnemySkillEffects(skill);
            return;
        }



        // ダメージ計算（多段攻撃対応）
        int totalDamage = 0;
        int hitSuccess = 0;

        if (hits > 1)
        {
            string multiName = !string.IsNullOrEmpty(skill.skillName) ? skill.skillName : "先制攻撃";
            AddLog($"{enemyMonster.Mname} の{multiName}！（{hits}回攻撃）");
        }

        for (int h = 0; h < hits; h++)
        {
            // ★多段攻撃: 全ヒット（1発目含む）で個別に命中判定
            // ★単発攻撃: ループ前で判定済みなのでここはスキップ
            if (hits > 1 && !CheckEnemyHit(effectiveHitRate))
            {
                AddLog($"  {h + 1}撃目 …外れた！");
                continue;
            }

            float hitMul = skill.GetHitDamageMultiplier(h);
            int hitBonus = skill.GetHitBonusDamage(h);
            int baseDamage;
            if (hitMul > 0f)
            {
                int attackPower = ApplyEnemyAttackBuffDebuff(enemyMonster.Attack);
                // Phase2: 怒り中は攻撃力1.5倍
                if (enemyRageTurn > 0)
                {
                    attackPower = Mathf.FloorToInt(attackPower * StatusEffectSystem.RageAttackMultiplier + 0.5f);
                }
                baseDamage = Mathf.FloorToInt(attackPower * hitMul + 0.5f);
            }
            else
                baseDamage = 0;
            baseDamage += hitBonus;
            if (baseDamage < 1) baseDamage = 1;

            WeaponAttribute hitAttr = skill.GetHitAttribute(h);
            int resistance = PassiveCalculator.CalcTotalAttributeResistance(hitAttr);

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
                string attrTag = skill.HasMultiHitEntries ? $"（{hitAttr.ToJapanese()}）" : "";
                AddLog($"  {h + 1}撃目{attrTag} {finalDamage}ダメージ！{logSuffix}");
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

                AddLog($"{enemyMonster.Mname} の{actionName}！（{hitAttr.ToJapanese()}属性） " +
                       $"{finalDamage}ダメージ！{logSuffix}");

                Debug.Log($"[Battle] Preemptive: base={baseDamage} resistance={resistance} " +
                          $"afterResist={afterResist} defense={defense} blocked={blocked} final={finalDamage}");
            }

            // 途中でプレイヤーが倒れたら残りをスキップ
            if (GameState.I != null && GameState.I.currentHp <= 0) break;
        }

        // 多段攻撃の合計ログ
        if (hits > 1)
        {
            AddLog($"  → 合計 {totalDamage}ダメージ！（{hitSuccess}/{hits}命中）");
        }

        // 追加効果の実行
        ProcessEnemySkillEffects(skill);
    }

    /// <summary>
    /// 攻撃ボタンが押された時の処理（プレイヤーターン・通常攻撃）。
    ///
    /// Phase2:
    ///   - 麻痺チェック（20%行動キャンセル）
    ///   - 暗闇時は CheckPlayerHitWithBlind() を使用
    ///   - 怒り中は攻撃力に RageAttackMultiplier を乗算
    ///   - 武器付与処理を汎用 switch 化
    /// </summary>
    private void OnAttackClicked()
    {
        if (battleEnded) return;
        BeginPlayerTurn(); // ターン開始ログ（防御フラグリセット + 敵行動事前抽選）
        SetButtonsInteractable(false);
        TickAllWeaponCooldowns();

        // Phase2: 麻痺チェック
        if (CheckPlayerParalyze()) return;

        // 先制攻撃割り込み
        if (ExecutePreemptiveIfNeeded()) return;

        string weaponName; WeaponAttribute weaponAttribute; int weaponPower;
        GetEquippedWeaponInfo(out weaponName, out weaponAttribute, out weaponPower);
        int baseHit = GetEquippedWeaponBaseHitRate();

        // Phase2: 暗闇対応の命中判定
        if (!CheckPlayerHitWithBlind(baseHit))
        {
            AddLog($"You は {weaponName} で攻撃！ …しかし外れた！");
            FlushLogsAndThen(() => EnemyTurn());
            return;
        }

        int damage = (GameState.I != null) ? ApplyPlayerAttackBuffDebuff(GameState.I.Attack) : 1;
        // Phase2: 怒り中は攻撃力1.5倍
        if (playerRageTurn > 0)
        {
            damage = Mathf.FloorToInt(damage * StatusEffectSystem.RageAttackMultiplier + 0.5f);
        }
        if (damage < 1) damage = 1;

        // 属性耐性によるダメージ軽減
        string resistLog;
        damage = ApplyEnemyAttributeResistance(damage, weaponAttribute, out resistLog);

        bool isCrit = CheckPlayerCrit();

        int finalDamage;
        if (isCrit)
        {
            finalDamage = damage * 2;
        }
        else
        {
            int enemyDef = GetEnemyDefense(DamageCategory.Physical);
            int enemyBlocked = RollDefenseDice(enemyDef);
            finalDamage = damage - enemyBlocked;
            if (finalDamage < 1) finalDamage = 1;
        }

        // 完全無効（耐性100以上）の場合は0ダメージ
        if (damage <= 0) finalDamage = 0;

        finalDamage = ApplyCharmDamageReduction(finalDamage);

        enemyCurrentHp -= finalDamage;
        if (enemyCurrentHp < 0) enemyCurrentHp = 0;

        if (isCrit)
            AddLog($"You は {weaponName} で攻撃！（{weaponAttribute.ToJapanese()}属性） クリティカル！ {finalDamage}ダメージ！{resistLog}");
        else
            AddLog($"You は {weaponName} で攻撃！（{weaponAttribute.ToJapanese()}属性） {finalDamage}ダメージ！{resistLog}");

        // =========================================================
        // 武器の状態異常付与判定 — 汎用 switch 化（Phase2）
        // =========================================================
        if (equippedWeaponItem != null && equippedWeaponItem.data != null
            && equippedWeaponItem.data.weaponInflictChance > 0)
        {
            StatusEffect inflictEffect = equippedWeaponItem.data.weaponInflictEffect;
            float inflictChance = equippedWeaponItem.data.weaponInflictChance;
            string eName = (enemyMonster != null) ? enemyMonster.Mname : "敵";

            switch (inflictEffect)
            {
                case StatusEffect.Poison:
                    if (!enemyIsPoisoned)
                    {
                        int resist = StatusEffectSystem.GetEnemyResistance(StatusEffect.Poison, enemyMonster);
                        if (StatusEffectSystem.TryInflict(inflictChance, resist))
                        {
                            enemyIsPoisoned = true;
                            AddLog($"{eName} は毒を受けた！");
                        }
                    }
                    break;

                case StatusEffect.Stun:
                    if (!enemyIsStunned)
                    {
                        int resist = StatusEffectSystem.GetEnemyResistance(StatusEffect.Stun, enemyMonster);
                        if (StatusEffectSystem.TryInflict(inflictChance, resist))
                        {
                            enemyIsStunned = true;
                            AddLog($"{eName} は気絶した！");
                        }
                    }
                    break;

                case StatusEffect.Paralyze:
                    if (!enemyIsParalyzed)
                    {
                        int resist = StatusEffectSystem.GetEnemyResistance(StatusEffect.Paralyze, enemyMonster);
                        if (StatusEffectSystem.TryInflict(inflictChance, resist))
                        {
                            enemyIsParalyzed = true;
                            AddLog($"{eName} は麻痺した！");
                        }
                    }
                    break;

                case StatusEffect.Blind:
                    if (!enemyIsBlind)
                    {
                        int resist = StatusEffectSystem.GetEnemyResistance(StatusEffect.Blind, enemyMonster);
                        if (StatusEffectSystem.TryInflict(inflictChance, resist))
                        {
                            enemyIsBlind = true;
                            AddLog($"{eName} は暗闇に包まれた！");
                        }
                    }
                    break;

                case StatusEffect.Rage:
                    if (playerRageTurn <= 0)
                    {
                        // 怒りは耐性判定なし（自分へのバフ扱い）、chanceのみ
                        float rageRoll = Random.Range(0f, 100f);
                        if (rageRoll < inflictChance)
                        {
                            playerRageTurn = StatusEffectSystem.RageDuration;
                            AddLog("You は怒りに燃えた！ 攻撃力UP！");
                        }
                    }
                    break;
            }
        }

        RefreshBattleStatusEffectUI(); // ★追加: 武器付与後にランプ更新

        if (enemyCurrentHp <= 0) { FlushLogsAndThen(() => OnVictory()); return; }
        FlushLogsAndThen(() => EnemyTurn());
    }

    /// <summary>
    /// スキルボタンが押された時の処理（プレイヤーターン・武器スキル攻撃）。
    /// Phase2: 怒り中はスキル使用不可。麻痺チェック追加。暗闇対応。
    ///
    /// 多段攻撃（hitCount > 1）の場合:
    ///   スキル発動自体は100%確定し、各ヒットごとに命中/回避判定を行う。
    /// </summary>
    private void OnSkillClicked()
    {
        if (battleEnded) return;

        // Phase2: 怒り中はスキル使用不可
        if (playerRageTurn > 0)
        {
            AddLogImmediate("怒りで我を忘れている！ 攻撃しかできない！");
            return;
        }

        SkillData skill = GetFirstSkill();
        if (skill == null) { AddLogImmediate("使えるスキルがない！"); return; }

        TickAllWeaponCooldowns();
        if (equippedWeaponItem == null || !equippedWeaponItem.CanUseSkill(skill.skillId))
        {
            AddLogImmediate($"{skill.skillName} はまだ使えない！");
            SetButtonsInteractable(true);
            RefreshSkillButton();
            return;
        }

        BeginPlayerTurn(); // ターン開始ログ（防御フラグリセット + 敵行動事前抽選）
        SetButtonsInteractable(false);
        string weaponName; WeaponAttribute weaponAttribute; int weaponPower;
        GetEquippedWeaponInfo(out weaponName, out weaponAttribute, out weaponPower);
        equippedWeaponItem.UseSkill(skill);

        // Phase2: 麻痺チェック
        if (CheckPlayerParalyze()) return;

        // 先制攻撃割り込み
        if (ExecutePreemptiveIfNeeded()) return;

        // =========================================================
        // 非ダメージスキル: 命中判定をスキップし、追加効果のみ実行
        // =========================================================
        if (skill.IsNonDamage)
        {
            if (skill.IsHostileNonDamage)
            {
                if (!CheckPlayerHitWithBlind(skill.baseHitRate))
                {
                    AddLog($"You は {skill.skillName}！ …しかし外れた！");
                    FlushLogsAndThen(() => EnemyTurn());
                    return;
                }
            }

            AddLog($"You は {skill.skillName}！");
            ProcessPlayerSkillEffects(skill);

            if (enemyCurrentHp <= 0) { FlushLogsAndThen(() => OnVictory()); return; }
            FlushLogsAndThen(() => EnemyTurn());
            return;
        }

        // --- ここから先はダメージスキルのみ ---

        // =========================================================
        // HP依存ダメージスキル（追加）
        // =========================================================
        if (skill.IsHpDependent)
        {
            if (skill.hpDependentType == HpDependentType.CurrentHpDamage)
            {
                if (!CheckPlayerHitWithBlind(skill.baseHitRate))
                {
                    AddLog($"You は {skill.skillName}！ …しかし外れた！");
                    FlushLogsAndThen(() => EnemyTurn());
                    return;
                }

                // ダメージ = 使用者（プレイヤー）の現在HP
                int baseDamage = (GameState.I != null) ? GameState.I.currentHp : 1;
                if (baseDamage < 1) baseDamage = 1;

                // 属性耐性を適用
                string resistLog;
                int afterResist = ApplyEnemyAttributeResistance(baseDamage, skill.skillAttribute, out resistLog);

                // 防御ダイスを適用（defenseIgnoreRate 対応）
                int enemyDef = GetEnemyDefense(skill.damageCategory);
                if (skill.defenseIgnoreRate > 0f)
                {
                    enemyDef = Mathf.FloorToInt(enemyDef * (1f - skill.defenseIgnoreRate) + 0.5f);
                }
                int enemyBlocked = RollDefenseDice(enemyDef);
                int finalDamage = afterResist - enemyBlocked;
                if (finalDamage < 1) finalDamage = 1;
                if (afterResist <= 0) finalDamage = 0;

                finalDamage = ApplyCharmDamageReduction(finalDamage);

                enemyCurrentHp -= finalDamage;
                if (enemyCurrentHp < 0) enemyCurrentHp = 0;

                string blockLog = enemyBlocked > 0 ? $"（防御{enemyBlocked}軽減）" : "";
                AddLog($"You は {skill.skillName}！ {finalDamage}ダメージ！（残りHP{baseDamage}）{resistLog}{blockLog}");

                Debug.Log($"[Battle] CurrentHpDamage(Player→Enemy/Skill): userHp={baseDamage} " +
                          $"afterResist={afterResist} blocked={enemyBlocked} final={finalDamage}");

                ProcessPlayerSkillEffects(skill, finalDamage);

                if (GameState.I != null && GameState.I.currentHp <= 0)
                {
                    if (enemyCurrentHp <= 0) { FlushLogsAndThen(() => OnVictory()); }
                    else { FlushLogsAndThen(() => OnDefeat()); }
                    return;
                }

                if (enemyCurrentHp <= 0) { FlushLogsAndThen(() => OnVictory()); return; }
                FlushLogsAndThen(() => EnemyTurn());
                return;
            }


            // 単発前提: 命中判定
            if (!CheckPlayerHitWithBlind(skill.baseHitRate))
            {
                AddLog($"You は {skill.skillName}！ …しかし外れた！");
                FlushLogsAndThen(() => EnemyTurn());
                return;
            }

            // MaxHpPercent: ボス/メタル系には無効
            if (skill.hpDependentType == HpDependentType.MaxHpPercent && IsEnemyImmuneToMaxHpPercent())
            {
                AddLog($"You は {skill.skillName}！ …しかし{enemyMonster.Mname}には効かなかった！");
                FlushLogsAndThen(() => EnemyTurn());
                return;
            }

            int hpDamage = CalcHpDependentDamage(skill.hpDependentType, enemyCurrentHp,
                enemyMonster != null ? enemyMonster.MaxHp : 0, skill.hpDependentPercent);

            enemyCurrentHp -= hpDamage;
            if (enemyCurrentHp < 0) enemyCurrentHp = 0;

            string hpDepLog;
            switch (skill.hpDependentType)
            {
                case HpDependentType.HalfCurrentHp: hpDepLog = "（HP半減）"; break;
                case HpDependentType.ReduceToOne: hpDepLog = "（HP→1）"; break;
                case HpDependentType.MaxHpPercent: hpDepLog = $"（最大HPの{skill.hpDependentPercent}%）"; break;
                default: hpDepLog = ""; break;
            }
            AddLog($"You は {skill.skillName}！ {hpDamage}ダメージ！{hpDepLog}");

            Debug.Log($"[Battle] HpDependent(Player→Enemy/Skill): type={skill.hpDependentType} " +
                      $"beforeHp={enemyCurrentHp + hpDamage} damage={hpDamage}");

            ProcessPlayerSkillEffects(skill, hpDamage);

            if (GameState.I != null && GameState.I.currentHp <= 0)
            {
                if (enemyCurrentHp <= 0) { FlushLogsAndThen(() => OnVictory()); }
                else { FlushLogsAndThen(() => OnDefeat()); }
                return;
            }

            if (enemyCurrentHp <= 0) { FlushLogsAndThen(() => OnVictory()); return; }
            FlushLogsAndThen(() => EnemyTurn());
            return;
        }


        // =========================================================
        // 多段攻撃対応
        // ★ hitCount > 1: スキル発動は確定、各ヒットで個別に命中判定
        // ★ hitCount == 1: 従来通りスキル発動前に命中判定
        // =========================================================
        int hits = skill.EffectiveHitCount;

        // 単発スキル: 発動前に命中判定（従来動作）
        if (hits <= 1)
        {
            if (!CheckPlayerHitWithBlind(skill.baseHitRate))
            {
                AddLog($"You は {skill.skillName}！ …しかし外れた！");
                FlushLogsAndThen(() => EnemyTurn());
                return;
            }
        }

        int totalDamage = 0;
        int hitSuccess = 0;

        if (hits > 1)
        {
            AddLog($"You は {skill.skillName}！（{hits}回攻撃）");
        }

        for (int h = 0; h < hits; h++)
        {
            // ★多段攻撃: 全ヒット（1発目含む）で個別に命中判定
            // ★単発攻撃: ループ前で判定済みなのでここはスキップ
            if (hits > 1 && !CheckPlayerHitWithBlind(skill.baseHitRate))
            {
                AddLog($"  {h + 1}撃目 …外れた！");
                continue;
            }

            int attack = (GameState.I != null) ? ApplyPlayerAttackBuffDebuff(GameState.I.Attack) : 1;
            // Phase2: 怒り中は攻撃力1.5倍
            if (playerRageTurn > 0)
            {
                attack = Mathf.FloorToInt(attack * StatusEffectSystem.RageAttackMultiplier + 0.5f);
            }
            float hitMul = skill.GetHitDamageMultiplier(h);
            int hitBonus = skill.GetHitBonusDamage(h);
            int damage;
            if (hitMul > 0f)
            {
                damage = Mathf.FloorToInt(attack * hitMul + 0.5f);
            }
            else
            {
                damage = 0;
            }
            damage += hitBonus;

            if (damage < 1) damage = 1;

            WeaponAttribute skillAttr = skill.GetHitAttribute(h);
            string resistLog;
            damage = ApplyEnemyAttributeResistance(damage, skillAttr, out resistLog);

            bool isCrit = CheckPlayerCrit();
            int finalDamage;
            if (isCrit)
            {
                finalDamage = damage * 2;
            }
            else
            {
                int enemyDef = GetEnemyDefense(skill.damageCategory);

                if (skill.defenseIgnoreRate > 0f)
                {
                    enemyDef = Mathf.FloorToInt(enemyDef * (1f - skill.defenseIgnoreRate) + 0.5f);
                }

                int enemyBlocked = RollDefenseDice(enemyDef);
                finalDamage = damage - enemyBlocked;
                if (finalDamage < 1) finalDamage = 1;
            }

            // 完全無効（耐性100以上）の場合は0ダメージ
            if (damage <= 0) finalDamage = 0;

            finalDamage = ApplyCharmDamageReduction(finalDamage);

            enemyCurrentHp -= finalDamage;
            if (enemyCurrentHp < 0) enemyCurrentHp = 0;
            totalDamage += finalDamage;
            hitSuccess++;

            if (hits > 1)
            {
                string attrTag = skill.HasMultiHitEntries ? $"（{skillAttr.ToJapanese()}）" : "";
                string hitPrefix = $"  {h + 1}撃目{attrTag}";
                if (isCrit)
                    AddLog($"{hitPrefix} クリティカル！ {finalDamage}ダメージ！{resistLog}");
                else
                    AddLog($"{hitPrefix} {finalDamage}ダメージ！{resistLog}");
            }
            else
            {
                if (isCrit)
                    AddLog($"You は {skill.skillName}！（{skillAttr.ToJapanese()}属性） クリティカル！ {finalDamage}ダメージ！{resistLog}");
                else
                    AddLog($"You は {skill.skillName}！（{skillAttr.ToJapanese()}属性） {finalDamage}ダメージ！{resistLog}");
            }

            if (enemyCurrentHp <= 0) break;
        }

        if (hits > 1)
        {
            AddLog($"  → 合計 {totalDamage}ダメージ！（{hitSuccess}/{hits}命中）");
        }

        // 追加効果の実行
        ProcessPlayerSkillEffects(skill, totalDamage);

        // 反動ダメージでプレイヤーが倒された場合の判定
        if (GameState.I != null && GameState.I.currentHp <= 0)
        {
            if (enemyCurrentHp <= 0)
            {
                FlushLogsAndThen(() => OnVictory());
            }
            else
            {
                FlushLogsAndThen(() => OnDefeat());
            }
            return;
        }

        if (enemyCurrentHp <= 0) { FlushLogsAndThen(() => OnVictory()); return; }
        FlushLogsAndThen(() => EnemyTurn());
    }

    // =========================================================
    // 魔法スキル発動（ドロップダウン選択 → 魔法ボタン押下）
    // =========================================================

    /// <summary>
    /// 魔法ボタンが押された時の処理（プレイヤーターン・魔法スキル発動）。
    /// Phase2: 怒り中は魔法使用不可。麻痺チェック追加。暗闇対応。
    ///
    /// 多段攻撃（hitCount > 1）の場合:
    ///   スキル発動自体は100%確定し、各ヒットごとに命中/回避判定を行う。
    /// </summary>
    private void OnMagicClicked()
    {
        if (battleEnded) return;

        // Phase2: 怒り中は魔法使用不可
        if (playerRageTurn > 0)
        {
            AddLogImmediate("怒りで我を忘れている！ 攻撃しかできない！");
            return;
        }

        // 沈黙チェック: 味方が沈黙中は魔法使用不可
        if (GameState.I != null && GameState.I.isSilenced)
        {
            AddLogImmediate("沈黙で魔法が唱えられない！");
            return;
        }

        SkillData magic = GetSelectedMagicSkill();
        if (magic == null) { AddLogImmediate("魔法が選択されていない！"); return; }

        int currentMp = (GameState.I != null) ? GameState.I.currentMp : 0;
        if (currentMp < magic.mpCost)
        {
            AddLogImmediate($"MPが足りない！（必要:{magic.mpCost} 現在:{currentMp}）");
            return;
        }

        BeginPlayerTurn(); // ターン開始ログ（防御フラグリセット + 敵行動事前抽選）
        SetButtonsInteractable(false);
        TickAllWeaponCooldowns();
        if (GameState.I != null) GameState.I.currentMp -= magic.mpCost;

        // Phase2: 麻痺チェック
        if (CheckPlayerParalyze()) return;

        // 先制攻撃割り込み
        if (ExecutePreemptiveIfNeeded()) return;

        // =========================================================
        // 非ダメージスキル
        // =========================================================
        if (magic.IsNonDamage)
        {
            if (magic.IsHostileNonDamage)
            {
                if (!CheckPlayerHitWithBlind(magic.baseHitRate))
                {
                    AddLog($"You は {magic.skillName}！ …しかし外れた！ MP-{magic.mpCost}");
                    FlushLogsAndThen(() => EnemyTurn());
                    return;
                }
            }

            AddLog($"You は {magic.skillName}！ MP-{magic.mpCost}");
            ProcessPlayerSkillEffects(magic);

            if (enemyCurrentHp <= 0) { FlushLogsAndThen(() => OnVictory()); return; }
            FlushLogsAndThen(() => EnemyTurn());
            return;
        }

        // --- ここから先はダメージスキルのみ ---

        // =========================================================
        // HP依存ダメージスキル（追加）
        // =========================================================
        if (magic.IsHpDependent)
        {
            if (magic.hpDependentType == HpDependentType.CurrentHpDamage)
            {
                if (!CheckPlayerHitWithBlind(magic.baseHitRate))
                {
                    AddLog($"You は {magic.skillName}！ …しかし外れた！ MP-{magic.mpCost}");
                    FlushLogsAndThen(() => EnemyTurn());
                    return;
                }

                int baseDamage = (GameState.I != null) ? GameState.I.currentHp : 1;
                if (baseDamage < 1) baseDamage = 1;

                string resistLog;
                int afterResist = ApplyEnemyAttributeResistance(baseDamage, magic.skillAttribute, out resistLog);

                int enemyDef = GetEnemyDefense(magic.damageCategory);
                if (magic.defenseIgnoreRate > 0f)
                {
                    enemyDef = Mathf.FloorToInt(enemyDef * (1f - magic.defenseIgnoreRate) + 0.5f);
                }
                int enemyBlocked = RollDefenseDice(enemyDef);
                int finalDamage = afterResist - enemyBlocked;
                if (finalDamage < 1) finalDamage = 1;
                if (afterResist <= 0) finalDamage = 0;

                finalDamage = ApplyCharmDamageReduction(finalDamage);

                enemyCurrentHp -= finalDamage;
                if (enemyCurrentHp < 0) enemyCurrentHp = 0;

                string blockLog = enemyBlocked > 0 ? $"（防御{enemyBlocked}軽減）" : "";
                AddLog($"You は {magic.skillName}！ {finalDamage}ダメージ！（残りHP{baseDamage}）{resistLog}{blockLog} MP-{magic.mpCost}");

                Debug.Log($"[Battle] CurrentHpDamage(Player→Enemy/Magic): userHp={baseDamage} " +
                          $"afterResist={afterResist} blocked={enemyBlocked} final={finalDamage}");

                ProcessPlayerSkillEffects(magic, finalDamage);

                if (GameState.I != null && GameState.I.currentHp <= 0)
                {
                    if (enemyCurrentHp <= 0) { FlushLogsAndThen(() => OnVictory()); }
                    else { FlushLogsAndThen(() => OnDefeat()); }
                    return;
                }

                if (enemyCurrentHp <= 0) { FlushLogsAndThen(() => OnVictory()); return; }
                FlushLogsAndThen(() => EnemyTurn());
                return;
            }


            // 単発前提: 命中判定
            if (!CheckPlayerHitWithBlind(magic.baseHitRate))
            {
                AddLog($"You は {magic.skillName}！ …しかし外れた！ MP-{magic.mpCost}");
                FlushLogsAndThen(() => EnemyTurn());
                return;
            }

            // MaxHpPercent: ボス/メタル系には無効
            if (magic.hpDependentType == HpDependentType.MaxHpPercent && IsEnemyImmuneToMaxHpPercent())
            {
                AddLog($"You は {magic.skillName}！ …しかし{enemyMonster.Mname}には効かなかった！ MP-{magic.mpCost}");
                FlushLogsAndThen(() => EnemyTurn());
                return;
            }

            int hpDamage = CalcHpDependentDamage(magic.hpDependentType, enemyCurrentHp,
                enemyMonster != null ? enemyMonster.MaxHp : 0, magic.hpDependentPercent);

            enemyCurrentHp -= hpDamage;
            if (enemyCurrentHp < 0) enemyCurrentHp = 0;

            string hpDepLog;
            switch (magic.hpDependentType)
            {
                case HpDependentType.HalfCurrentHp: hpDepLog = "（HP半減）"; break;
                case HpDependentType.ReduceToOne: hpDepLog = "（HP→1）"; break;
                case HpDependentType.MaxHpPercent: hpDepLog = $"（最大HPの{magic.hpDependentPercent}%）"; break;
                default: hpDepLog = ""; break;
            }
            AddLog($"You は {magic.skillName}！ {hpDamage}ダメージ！{hpDepLog} MP-{magic.mpCost}");

            Debug.Log($"[Battle] HpDependent(Player→Enemy/Magic): type={magic.hpDependentType} " +
                      $"beforeHp={enemyCurrentHp + hpDamage} damage={hpDamage}");

            ProcessPlayerSkillEffects(magic, hpDamage);

            if (GameState.I != null && GameState.I.currentHp <= 0)
            {
                if (enemyCurrentHp <= 0) { FlushLogsAndThen(() => OnVictory()); }
                else { FlushLogsAndThen(() => OnDefeat()); }
                return;
            }

            if (enemyCurrentHp <= 0) { FlushLogsAndThen(() => OnVictory()); return; }
            FlushLogsAndThen(() => EnemyTurn());
            return;
        }


        // =========================================================
        // 多段攻撃対応
        // ★ hitCount > 1: スキル発動は確定、各ヒットで個別に命中判定
        // ★ hitCount == 1: 従来通りスキル発動前に命中判定
        // =========================================================
        int hits = magic.EffectiveHitCount;

        // 単発スキル: 発動前に命中判定（従来動作）
        if (hits <= 1)
        {
            if (!CheckPlayerHitWithBlind(magic.baseHitRate))
            {
                AddLog($"You は {magic.skillName}！ …しかし外れた！ MP-{magic.mpCost}");
                FlushLogsAndThen(() => EnemyTurn());
                return;
            }
        }

        int totalDamage = 0;
        int hitSuccess = 0;

        if (hits > 1)
        {
            AddLog($"You は {magic.skillName}！（{hits}回攻撃） MP-{magic.mpCost}");
        }

        for (int h = 0; h < hits; h++)
        {
            // ★多段攻撃: 全ヒット（1発目含む）で個別に命中判定
            // ★単発攻撃: ループ前で判定済みなのでここはスキップ
            if (hits > 1 && !CheckPlayerHitWithBlind(magic.baseHitRate))
            {
                AddLog($"  {h + 1}撃目 …外れた！");
                continue;
            }

            // ★ OnMagicClicked 固有: MagicAttack ベース
            float hitMul = magic.GetHitDamageMultiplier(h);
            int hitBonus = magic.GetHitBonusDamage(h);
            int damage;
            if (hitMul > 0)
            {
                int magicAttack = (GameState.I != null) ? ApplyPlayerMagicAttackBuffDebuff(GameState.I.MagicAttack) : 1;
                // Phase2: 怒り中は攻撃力1.5倍（魔法攻撃にも適用）
                if (playerRageTurn > 0)
                {
                    magicAttack = Mathf.FloorToInt(magicAttack * StatusEffectSystem.RageAttackMultiplier + 0.5f);
                }
                damage = Mathf.FloorToInt(magicAttack * hitMul + 0.5f);
            }
            else
            {
                damage = 0;
            }
            damage += hitBonus;
            if (damage < 1) damage = 1;

            // 属性耐性によるダメージ軽減
            WeaponAttribute hitAttr = magic.GetHitAttribute(h);
            string resistLog;
            damage = ApplyEnemyAttributeResistance(damage, hitAttr, out resistLog);

            bool isCrit = CheckPlayerCrit();
            int finalDamage;
            if (isCrit) { finalDamage = damage * 2; }
            else
            {
                int enemyDef = GetEnemyDefense(magic.damageCategory);

                if (magic.defenseIgnoreRate > 0f)
                {
                    enemyDef = Mathf.FloorToInt(enemyDef * (1f - magic.defenseIgnoreRate) + 0.5f);
                }

                int enemyBlocked = RollDefenseDice(enemyDef);
                finalDamage = damage - enemyBlocked;
                if (finalDamage < 1) finalDamage = 1;
            }

            // 完全無効（耐性100以上）の場合は0ダメージ
            if (damage <= 0) finalDamage = 0;

            finalDamage = ApplyCharmDamageReduction(finalDamage);

            enemyCurrentHp -= finalDamage;
            if (enemyCurrentHp < 0) enemyCurrentHp = 0;
            totalDamage += finalDamage;
            hitSuccess++;

            if (hits > 1)
            {
                string attrTag = magic.HasMultiHitEntries ? $"（{hitAttr.ToJapanese()}）" : "";
                string hitPrefix = $"  {h + 1}撃目{attrTag}";
                if (isCrit)
                    AddLog($"{hitPrefix} クリティカル！ {finalDamage}ダメージ！{resistLog}");
                else
                    AddLog($"{hitPrefix} {finalDamage}ダメージ！{resistLog}");
            }
            else
            {
                if (isCrit)
                    AddLog($"You は {magic.skillName}！（{magic.skillAttribute.ToJapanese()}属性） クリティカル！ {finalDamage}ダメージ！{resistLog} MP-{magic.mpCost}");
                else
                    AddLog($"You は {magic.skillName}！（{magic.skillAttribute.ToJapanese()}属性） {finalDamage}ダメージ！{resistLog} MP-{magic.mpCost}");
            }

            if (enemyCurrentHp <= 0) break;
        }

        if (hits > 1)
        {
            AddLog($"  → 合計 {totalDamage}ダメージ！（{hitSuccess}/{hits}命中）");
        }

        // 追加効果の実行（全ヒット完了後に1回だけ）
        ProcessPlayerSkillEffects(magic, totalDamage);

        // 反動ダメージでプレイヤーが倒された場合の判定
        if (GameState.I != null && GameState.I.currentHp <= 0)
        {
            if (enemyCurrentHp <= 0)
            {
                FlushLogsAndThen(() => OnVictory());
            }
            else
            {
                FlushLogsAndThen(() => OnDefeat());
            }
            return;
        }

        if (enemyCurrentHp <= 0) { FlushLogsAndThen(() => OnVictory()); return; }
        FlushLogsAndThen(() => EnemyTurn());
    }

    // =========================================================
    // 防御コマンド
    // =========================================================

    /// <summary>
    /// 防御ボタンが押された時の処理（プレイヤーターン・防御）。
    /// Phase2: 怒り中は防御不可。麻痺チェック追加。
    /// </summary>
    private void OnDefendClicked()
    {
        if (battleEnded) return;

        // Phase2: 怒り中は防御不可
        if (playerRageTurn > 0)
        {
            AddLogImmediate("怒りで我を忘れている！ 攻撃しかできない！");
            return;
        }

        BeginPlayerTurn(); // ターン開始ログ（前ターンの防御はここでリセット）
        SetButtonsInteractable(false);
        TickAllWeaponCooldowns();

        // Phase2: 麻痺チェック
        if (CheckPlayerParalyze()) return;

        // 防御フラグをセット（このターンの敵攻撃に対して有効）
        isDefending = true;

        // 先制攻撃割り込み（防御フラグは既にセット済みなので先制攻撃にも防御が適用される）
        if (ExecutePreemptiveIfNeeded()) return;

        AddLog("You は防御の構えを取った！");

        FlushLogsAndThen(() => EnemyTurn());
    }

    /// <summary>
    /// プレイヤースキルの追加効果を実行する共通メソッド。
    /// </summary>
    private void ProcessPlayerSkillEffects(SkillData skill)
    {
        if (!skill.HasAdditionalEffects) return;

        var logs = SkillEffectProcessor.ProcessEffects(
            skill.additionalEffects,
            isPlayerAttack: true,
            enemyMonster,
            ref enemyIsPoisoned,
            ref enemyIsStunned,
            ref enemyCurrentHp,
            0,
            ref enemyIsParalyzed,
            ref enemyIsBlind,
            ref enemyIsSilenced, 
            ref enemyRageTurn,
            ref playerRageTurn,
            ref buffState);

        for (int i = 0; i < logs.Count; i++)
        {
            AddLog(logs[i]);
        }

        RefreshBattleStatusEffectUI(); // ★追加: 状態異常UIを更新
    }

    /// <summary>
    /// プレイヤースキルの追加効果を実行する（与ダメージ付き）。
    /// </summary>
    private void ProcessPlayerSkillEffects(SkillData skill, int lastDamageDealt)
    {
        if (!skill.HasAdditionalEffects) return;

        var logs = SkillEffectProcessor.ProcessEffects(
            skill.additionalEffects,
            isPlayerAttack: true,
            enemyMonster,
            ref enemyIsPoisoned,
            ref enemyIsStunned,
            ref enemyCurrentHp,
            lastDamageDealt,
            ref enemyIsParalyzed,
            ref enemyIsBlind,
            ref enemyIsSilenced,
            ref enemyRageTurn,
            ref playerRageTurn,
            ref buffState);

        for (int i = 0; i < logs.Count; i++)
        {
            AddLog(logs[i]);
        }

        RefreshBattleStatusEffectUI();
    }

    // =========================================================
    // アイテム使用（Itembox シーンへ遷移）
    // =========================================================

    /// <summary>
    /// アイテムボタンが押された時の処理。
    /// Phase2: 怒り中はアイテム使用不可。
    /// </summary>
    private void OnItemClicked()
    {
        if (battleEnded) return;

        // Phase2: 怒り中はアイテム使用不可
        if (playerRageTurn > 0)
        {
            AddLogImmediate("怒りで我を忘れている！ 攻撃しかできない！");
            return;
        }

        if (GameState.I != null)
        {
            GameState.I.isInBattle = true;
            GameState.I.battleTurnConsumed = false;
            GameState.I.battleItemActionLog = "";
            GameState.I.previousSceneName = SceneManager.GetActiveScene().name;
        }
        persistentLogLines = new System.Collections.Generic.List<string>(logLines);
        SceneManager.LoadScene(itemboxSceneName);
    }
}