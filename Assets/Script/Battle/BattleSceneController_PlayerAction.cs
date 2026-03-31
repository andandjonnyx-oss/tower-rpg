using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// BattleSceneController のプレイヤー行動パート（partial class）。
/// 通常攻撃、武器スキル、魔法スキル、アイテム使用を担当する。
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

    /// <summary>
    /// 攻撃ボタンが押された時の処理（プレイヤーターン・通常攻撃）。
    ///
    /// ダメージ計算（改修後）:
    ///   GameState.Attack（= baseSTR + 装備attackPower + パッシブ攻撃ボーナス）
    ///   をそのままダメージとして使用する。
    ///   ※ 装備品の attackPower は GameState.Attack に含まれているため、
    ///     ここでは weaponPower を加算しない（二重加算を防止）。
    ///
    /// 命中判定（追加）:
    ///   基礎命中率（武器の baseHitRate、素手=95）× (1 - (敵回避力 - 命中力)/100)
    ///   最低25%保証。ミス時はダメージ0でターン終了。
    ///
    /// クリティカル判定（追加）:
    ///   命中後に CriticalRate% の確率でクリティカル。
    ///   クリティカル時: 防御無視、ダメージ2倍。
    /// </summary>
    private void OnAttackClicked()
    {
        if (battleEnded) return;
        BeginPlayerTurn(); // ターン開始ログ
        SetButtonsInteractable(false);
        TickAllWeaponCooldowns();

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

        enemyCurrentHp -= finalDamage;
        if (enemyCurrentHp < 0) enemyCurrentHp = 0;

        if (isCrit)
            AddLog($"You は {weaponName} で攻撃！（{weaponAttribute.ToJapanese()}属性） クリティカル！ {finalDamage}ダメージ！");
        else
            AddLog($"You は {weaponName} で攻撃！（{weaponAttribute.ToJapanese()}属性） {finalDamage}ダメージ！");

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
    /// ダメージ計算（改修後）:
    ///   GameState.Attack × skill.damageMultiplier
    ///   ※ Attack に装備攻撃力が含まれているため、weaponPower を別途加算しない。
    ///
    /// 非ダメージスキル:
    ///   IsNonDamage == true の場合はダメージ計算をスキップし、追加効果のみ実行。
    ///
    /// 命中判定（追加）:
    ///   skill.baseHitRate × (1 - (敵回避力 - 命中力)/100)、最低25%。
    ///   クールダウンは命中に関わらず消費する。
    ///
    /// クリティカル判定（追加）:
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

        BeginPlayerTurn(); // ターン開始ログ
        SetButtonsInteractable(false);
        string weaponName; WeaponAttribute weaponAttribute; int weaponPower;
        GetEquippedWeaponInfo(out weaponName, out weaponAttribute, out weaponPower);
        equippedWeaponItem.UseSkill(skill);

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
        bool isCrit = CheckPlayerCrit();
        WeaponAttribute skillAttr = skill.skillAttribute;

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

        enemyCurrentHp -= finalDamage;
        if (enemyCurrentHp < 0) enemyCurrentHp = 0;

        if (isCrit)
            AddLog($"You は {skill.skillName}！（{skillAttr.ToJapanese()}属性） クリティカル！ {finalDamage}ダメージ！");
        else
            AddLog($"You は {skill.skillName}！（{skillAttr.ToJapanese()}属性） {finalDamage}ダメージ！");

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
    /// ダメージ計算（改修後）:
    ///   fixedDamage > 0 → そのまま使用（固定ダメージ）
    ///   damageMultiplier > 0 → GameState.MagicAttack × 倍率
    ///   ※ MagicAttack に装備分・パッシブ分が含まれている。
    ///
    /// 非ダメージスキル:
    ///   IsNonDamage == true の場合はダメージ計算をスキップし、追加効果のみ実行。
    ///
    /// 命中判定（追加）:
    ///   magic.baseHitRate × (1 - (敵回避力 - 命中力)/100)、最低25%。
    ///   ミス時も MP は消費する。
    ///
    /// クリティカル判定（追加）:
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

        BeginPlayerTurn(); // ターン開始ログ
        SetButtonsInteractable(false);
        TickAllWeaponCooldowns();
        if (GameState.I != null) GameState.I.currentMp -= magic.mpCost;

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

        enemyCurrentHp -= finalDamage;
        if (enemyCurrentHp < 0) enemyCurrentHp = 0;

        if (isCrit)
            AddLog($"You は {magic.skillName}！（{magic.skillAttribute.ToJapanese()}属性） クリティカル！ {finalDamage}ダメージ！ MP-{magic.mpCost}");
        else
            AddLog($"You は {magic.skillName}！（{magic.skillAttribute.ToJapanese()}属性） {finalDamage}ダメージ！ MP-{magic.mpCost}");

        // 追加効果の実行
        ProcessPlayerSkillEffects(magic);

        if (enemyCurrentHp <= 0) { OnVictory(); return; }
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