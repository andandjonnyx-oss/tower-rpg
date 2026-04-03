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

        // 先制フラグをリセット（この先制は1回きり）
        isEnemyPreemptive = false;
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
                OnVictory();
            }
            else
            {
                OnDefeat();
            }
            return true; // プレイヤー行動スキップ
        }

        // 敵が自爆で倒れた場合
        if (enemyCurrentHp <= 0)
        {
            OnVictory();
            return true; // プレイヤー行動スキップ
        }

        // 先制後もプレイヤーは生存 → 通常通りプレイヤー行動を続行
        return false;
    }

    /// <summary>
    /// 先制技を実行する。SkillAttack と同じダメージ計算を行う。
    /// ターン終了処理（毒ダメージ等）は行わない（プレイヤー行動後に行う）。
    /// </summary>
    private void ExecutePreemptiveAction(EnemyActionEntry action)
    {
        if (action.skill == null)
        {
            Debug.LogWarning("[Battle] 先制攻撃のスキルが null です。スキップします。");
            return;
        }

        SkillData skill = action.skill;

        // 命中判定
        if (!CheckEnemyHit(skill.baseHitRate))
        {
            string missName = !string.IsNullOrEmpty(skill.skillName)
                ? skill.skillName : "先制攻撃";
            AddLog($"{enemyMonster.Mname} の{missName}！ …しかし外れた！");
            return;
        }

        // 非ダメージスキル: 追加効果のみ
        if (skill.IsNonDamage)
        {
            string effectSkillName = !string.IsNullOrEmpty(skill.skillName) ? skill.skillName : "先制スキル";
            AddLog($"{enemyMonster.Mname} の{effectSkillName}！");
            ProcessEnemySkillEffects(skill);
            return;
        }

        // ダメージ計算（SkillAttack と同じ）
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

        Debug.Log($"[Battle] Preemptive: base={baseDamage} resistance={resistance} " +
                  $"afterResist={afterResist} defense={defense} blocked={blocked} final={finalDamage}");

        // 追加効果の実行
        ProcessEnemySkillEffects(skill);
    }

    /// <summary>
    /// 攻撃ボタンが押された時の処理（プレイヤーターン・通常攻撃）。
    ///
    /// ダメージ計算:
    ///   GameState.Attack（= baseSTR + 装備attackPower + パッシブ攻撃ボーナス）
    ///   → 敵の属性耐性で軽減（武器の weaponAttribute を参照）
    ///   → 防御ダイスで軽減（クリティカル時は防御無視）
    ///
    /// 命中判定:
    ///   基礎命中率（武器の baseHitRate、素手=95）× (1 - (敵回避力 - 命中力)/100)
    ///   最低25%保証。ミス時はダメージ0でターン終了。
    ///
    /// クリティカル判定:
    ///   命中後に CriticalRate% の確率でクリティカル。
    ///   クリティカル時: 防御無視、ダメージ2倍（属性耐性は適用済み）。
    /// </summary>
    private void OnAttackClicked()
    {
        if (battleEnded) return;
        BeginPlayerTurn(); // ターン開始ログ（防御フラグリセット + 敵行動事前抽選）
        SetButtonsInteractable(false);
        TickAllWeaponCooldowns();

        // 先制攻撃割り込み
        if (ExecutePreemptiveIfNeeded()) return;

        string weaponName; WeaponAttribute weaponAttribute; int weaponPower;
        GetEquippedWeaponInfo(out weaponName, out weaponAttribute, out weaponPower);
        int baseHit = GetEquippedWeaponBaseHitRate();

        if (!CheckPlayerHit(baseHit))
        {
            AddLog($"You は {weaponName} で攻撃！ …しかし外れた！");
            Invoke(nameof(EnemyTurn), 0.5f);
            return;
        }

        int damage = (GameState.I != null) ? GameState.I.Attack : 1;
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

        enemyCurrentHp -= finalDamage;
        if (enemyCurrentHp < 0) enemyCurrentHp = 0;

        if (isCrit)
            AddLog($"You は {weaponName} で攻撃！（{weaponAttribute.ToJapanese()}属性） クリティカル！ {finalDamage}ダメージ！{resistLog}");
        else
            AddLog($"You は {weaponName} で攻撃！（{weaponAttribute.ToJapanese()}属性） {finalDamage}ダメージ！{resistLog}");

        // 武器の毒付与判定 - 既に毒ならスキップ（ログも出さない）
        if (equippedWeaponItem != null && equippedWeaponItem.data != null
            && equippedWeaponItem.data.weaponInflictEffect == StatusEffect.Poison
            && equippedWeaponItem.data.weaponInflictChance > 0
            && !enemyIsPoisoned)
        {
            int enemyPoisonResist = (enemyMonster != null) ? enemyMonster.PoisonResistance : 0;
            bool poisoned = StatusEffectSystem.TryInflict(
                equippedWeaponItem.data.weaponInflictChance, enemyPoisonResist);
            if (poisoned)
            {
                enemyIsPoisoned = true;
                AddLog($"{enemyMonster.Mname} は毒を受けた！");
            }
        }

        if (enemyCurrentHp <= 0) { OnVictory(); return; }
        Invoke(nameof(EnemyTurn), 0.5f);
    }

    /// <summary>
    /// スキルボタンが押された時の処理（プレイヤーターン・武器スキル攻撃）。
    /// 装備中武器の最初のスキルを使用する。
    /// skill.damageCategory（Physical/Magical）に応じて敵の防御ダイスを選択する。
    ///
    /// ダメージ計算:
    ///   GameState.Attack × skill.damageMultiplier
    ///   → 敵の属性耐性で軽減（skill.skillAttribute を参照）
    ///   → 防御ダイスで軽減
    ///
    /// 非ダメージスキル:
    ///   IsNonDamage == true の場合はダメージ計算をスキップし、追加効果のみ実行。
    ///
    /// 命中判定:
    ///   skill.baseHitRate × (1 - (敵回避力 - 命中力)/100)、最低25%。
    ///   クールダウンは命中に関わらず消費する。
    ///
    /// クリティカル判定:
    ///   命中後に CriticalRate% でクリティカル（防御無視・2倍ダメージ）。
    /// </summary>
    private void OnSkillClicked()
    {
        if (battleEnded) return;
        SkillData skill = GetFirstSkill();
        if (skill == null) { AddLog("使えるスキルがない！"); return; }

        TickAllWeaponCooldowns();
        if (equippedWeaponItem == null || !equippedWeaponItem.CanUseSkill(skill.skillId))
        {
            AddLog($"{skill.skillName} はまだ使えない！");
            SetButtonsInteractable(true);
            RefreshSkillButton();
            return;
        }

        BeginPlayerTurn(); // ターン開始ログ（防御フラグリセット + 敵行動事前抽選）
        SetButtonsInteractable(false);
        string weaponName; WeaponAttribute weaponAttribute; int weaponPower;
        GetEquippedWeaponInfo(out weaponName, out weaponAttribute, out weaponPower);
        equippedWeaponItem.UseSkill(skill);

        // 先制攻撃割り込み
        if (ExecutePreemptiveIfNeeded()) return;

        if (!CheckPlayerHit(skill.baseHitRate))
        {
            AddLog($"You は {skill.skillName}！ …しかし外れた！");
            Invoke(nameof(EnemyTurn), 0.5f);
            return;
        }

        // 非ダメージスキル: 追加効果のみ実行
        if (skill.IsNonDamage)
        {
            AddLog($"You は {skill.skillName}！");
            ProcessPlayerSkillEffects(skill);

            if (enemyCurrentHp <= 0) { OnVictory(); return; }
            Invoke(nameof(EnemyTurn), 0.5f);
            return;
        }

        int attack = (GameState.I != null) ? GameState.I.Attack : 1;
        int damage = Mathf.FloorToInt(attack * skill.damageMultiplier + 0.5f);
        if (damage < 1) damage = 1;

        // 属性耐性によるダメージ軽減
        WeaponAttribute skillAttr = skill.skillAttribute;
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
            int enemyBlocked = RollDefenseDice(enemyDef);
            finalDamage = damage - enemyBlocked;
            if (finalDamage < 1) finalDamage = 1;
        }

        // 完全無効（耐性100以上）の場合は0ダメージ
        if (damage <= 0) finalDamage = 0;

        enemyCurrentHp -= finalDamage;
        if (enemyCurrentHp < 0) enemyCurrentHp = 0;

        if (isCrit)
            AddLog($"You は {skill.skillName}！（{skillAttr.ToJapanese()}属性） クリティカル！ {finalDamage}ダメージ！{resistLog}");
        else
            AddLog($"You は {skill.skillName}！（{skillAttr.ToJapanese()}属性） {finalDamage}ダメージ！{resistLog}");

        // 追加効果の実行
        ProcessPlayerSkillEffects(skill);

        if (enemyCurrentHp <= 0) { OnVictory(); return; }
        Invoke(nameof(EnemyTurn), 0.5f);
    }

    // =========================================================
    // 魔法スキル発動（ドロップダウン選択 → 魔法ボタン押下）
    // =========================================================

    /// <summary>
    /// 魔法ボタンが押された時の処理（プレイヤーターン・魔法スキル発動）。
    /// ドロップダウンで選択中の魔法スキルを MP 消費して発動する。
    /// skill.damageCategory（通常 Magical）に応じて敵の防御ダイスを選択する。
    ///
    /// ダメージ計算:
    ///   fixedDamage > 0 → そのまま使用（固定ダメージ）
    ///   damageMultiplier > 0 → GameState.MagicAttack × 倍率
    ///   → 敵の属性耐性で軽減（magic.skillAttribute を参照）
    ///   → 防御ダイスで軽減
    ///
    /// 非ダメージスキル:
    ///   IsNonDamage == true の場合はダメージ計算をスキップし、追加効果のみ実行。
    ///
    /// 命中判定:
    ///   magic.baseHitRate × (1 - (敵回避力 - 命中力)/100)、最低25%。
    ///   ミス時も MP は消費する。
    ///
    /// クリティカル判定:
    ///   命中後に CriticalRate% でクリティカル（防御無視・2倍ダメージ）。
    /// </summary>
    private void OnMagicClicked()
    {
        if (battleEnded) return;
        SkillData magic = GetSelectedMagicSkill();
        if (magic == null) { AddLog("魔法が選択されていない！"); return; }

        int currentMp = (GameState.I != null) ? GameState.I.currentMp : 0;
        if (currentMp < magic.mpCost)
        {
            AddLog($"MPが足りない！（必要:{magic.mpCost} 現在:{currentMp}）");
            return;
        }

        BeginPlayerTurn(); // ターン開始ログ（防御フラグリセット + 敵行動事前抽選）
        SetButtonsInteractable(false);
        TickAllWeaponCooldowns();
        if (GameState.I != null) GameState.I.currentMp -= magic.mpCost;

        // 先制攻撃割り込み
        if (ExecutePreemptiveIfNeeded()) return;

        if (!CheckPlayerHit(magic.baseHitRate))
        {
            AddLog($"You は {magic.skillName}！ …しかし外れた！ MP-{magic.mpCost}");
            Invoke(nameof(EnemyTurn), 0.5f);
            return;
        }

        // 非ダメージスキル: 追加効果のみ実行
        if (magic.IsNonDamage)
        {
            AddLog($"You は {magic.skillName}！ MP-{magic.mpCost}");
            ProcessPlayerSkillEffects(magic);

            if (enemyCurrentHp <= 0) { OnVictory(); return; }
            Invoke(nameof(EnemyTurn), 0.5f);
            return;
        }

        int damage;
        if (magic.fixedDamage > 0) { damage = magic.fixedDamage; }
        else if (magic.damageMultiplier > 0)
        {
            int magicAttack = (GameState.I != null) ? GameState.I.MagicAttack : 1;
            damage = Mathf.FloorToInt(magicAttack * magic.damageMultiplier + 0.5f);
        }
        else { damage = 1; }
        if (damage < 1) damage = 1;

        // 属性耐性によるダメージ軽減
        string resistLog;
        damage = ApplyEnemyAttributeResistance(damage, magic.skillAttribute, out resistLog);

        bool isCrit = CheckPlayerCrit();
        int finalDamage;
        if (isCrit) { finalDamage = damage * 2; }
        else
        {
            int enemyDef = GetEnemyDefense(magic.damageCategory);
            int enemyBlocked = RollDefenseDice(enemyDef);
            finalDamage = damage - enemyBlocked;
            if (finalDamage < 1) finalDamage = 1;
        }

        // 完全無効（耐性100以上）の場合は0ダメージ
        if (damage <= 0) finalDamage = 0;

        enemyCurrentHp -= finalDamage;
        if (enemyCurrentHp < 0) enemyCurrentHp = 0;

        if (isCrit)
            AddLog($"You は {magic.skillName}！（{magic.skillAttribute.ToJapanese()}属性） クリティカル！ {finalDamage}ダメージ！{resistLog} MP-{magic.mpCost}");
        else
            AddLog($"You は {magic.skillName}！（{magic.skillAttribute.ToJapanese()}属性） {finalDamage}ダメージ！{resistLog} MP-{magic.mpCost}");

        // 追加効果の実行
        ProcessPlayerSkillEffects(magic);

        if (enemyCurrentHp <= 0) { OnVictory(); return; }
        Invoke(nameof(EnemyTurn), 0.5f);
    }

    // =========================================================
    // 防御コマンド（追加）
    // =========================================================
    //
    // 防御を選択すると、そのターンの敵攻撃に対して以下の効果が適用される:
    //   1. 物理防御力・魔法防御力が 2倍 になる
    //   2. 防御ダイスの diceRange が 1.5f になる（通常2.0f → 成功率50%→67%）
    //
    // 防御フラグ（isDefending）は BeginPlayerTurn() で false にリセットされるため、
    // 防御の効果は選択したターンの敵攻撃1回分のみ。
    //
    // 防御中は行動せずにターン終了するため、クールダウンは進める。
    // =========================================================

    /// <summary>
    /// 防御ボタンが押された時の処理（プレイヤーターン・防御）。
    /// isDefending フラグを true にセットし、敵ターンに移行する。
    /// 防御フラグは次の BeginPlayerTurn() で false にリセットされる。
    /// </summary>
    private void OnDefendClicked()
    {
        if (battleEnded) return;
        BeginPlayerTurn(); // ターン開始ログ（前ターンの防御はここでリセット）
        SetButtonsInteractable(false);
        TickAllWeaponCooldowns();

        // 防御フラグをセット（このターンの敵攻撃に対して有効）
        isDefending = true;

        // 先制攻撃割り込み（防御フラグは既にセット済みなので先制攻撃にも防御が適用される）
        if (ExecutePreemptiveIfNeeded()) return;

        AddLog("You は防御の構えを取った！");

        Invoke(nameof(EnemyTurn), 0.5f);
    }

    /// <summary>
    /// プレイヤースキルの追加効果を実行する共通メソッド。
    /// SkillEffectProcessor を呼び出し、結果のログを追加する。
    /// </summary>
    private void ProcessPlayerSkillEffects(SkillData skill)
    {
        if (!skill.HasAdditionalEffects) return;

        var logs = SkillEffectProcessor.ProcessEffects(
            skill.additionalEffects,
            isPlayerAttack: true,
            enemyMonster,
            ref enemyIsPoisoned,
            ref enemyCurrentHp);

        for (int i = 0; i < logs.Count; i++)
        {
            AddLog(logs[i]);
        }
    }

    // =========================================================
    // アイテム使用（Itembox シーンへ遷移）
    // =========================================================

    /// <summary>
    /// アイテムボタンが押された時の処理。
    /// Itembox シーンへ遷移する（ターン消費なし）。
    /// ※ アイテム使用はターン消費扱いだが、BeginPlayerTurn は
    ///   Itembox から戻った後（battleTurnConsumed 処理）に呼ばれない。
    ///   アイテム使用時のターン開始ログは、Itembox 側でターン消費が
    ///   確定した時点で battleItemActionLog にまとめて記録される。
    ///   → ターン区切り線はアイテム使用時には出ない（仕様）。
    /// </summary>
    private void OnItemClicked()
    {
        if (battleEnded) return;
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