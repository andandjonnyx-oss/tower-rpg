using UnityEngine;

/// <summary>
/// BattleSceneController の戦闘計算ユーティリティパート（partial class）。
/// 命中判定、クリティカル判定、防御ダイス、ダメージ適用、防御力取得、
/// 属性耐性によるダメージ軽減を担当する。
/// </summary>
public partial class BattleSceneController
{
    // =========================================================
    // 命中判定・クリティカル判定（プレイヤー攻撃用）（追加）
    // =========================================================

    /// <summary>
    /// プレイヤーの攻撃が命中するかどうかを判定する。
    ///
    /// 計算式:
    ///   最終命中率 = baseHitRate × (1 - (敵回避力 - 命中力) / 100)
    ///   ただし最低25%保証。
    ///
    /// 例: 基礎命中率90%、命中力10、敵回避力20 の場合
    ///   90 × (1 - (20-10)/100) = 90 × 0.9 = 81%
    ///
    /// 例: 基礎命中率95%、命中力30、敵回避力10 の場合
    ///   95 × (1 - (10-30)/100) = 95 × 1.2 = 114% → 95%にクランプ
    /// </summary>
    /// <param name="baseHitRate">スキルまたは武器の基礎命中率（%）</param>
    /// <returns>true: 命中、false: ミス</returns>
    private bool CheckPlayerHit(int baseHitRate)
    {
        int playerAccuracy = (GameState.I != null) ? GameState.I.Accuracy : 0;
        int enemyEvasion = (enemyMonster != null) ? enemyMonster.Evasion : 0;

        float hitChance = baseHitRate * (1f - (enemyEvasion - playerAccuracy) / 100f);

        if (hitChance < 25f) hitChance = 25f;
        if (hitChance > 100f) hitChance = 100f;

        float roll = Random.Range(0f, 100f);
        bool hit = roll < hitChance;

        Debug.Log($"[Battle] PlayerHitCheck: baseHit={baseHitRate} accuracy={playerAccuracy} " +
                  $"enemyEvasion={enemyEvasion} hitChance={hitChance:F2}% roll={roll:F2} hit={hit}");

        return hit;
    }

    /// <summary>
    /// プレイヤーの攻撃がクリティカルになるかどうかを判定する。
    /// CriticalRate（float、小数点2位精度）% の確率で発動。
    /// クリティカル時: 防御無視、ダメージ2倍。
    /// </summary>
    /// <returns>true: クリティカル、false: 通常</returns>
    private bool CheckPlayerCrit()
    {
        float critChance = (GameState.I != null) ? GameState.I.CriticalRate : 5f;
        if (critChance < 0f) critChance = 0f;

        float roll = Random.Range(0f, 100f);
        bool crit = roll < critChance;

        Debug.Log($"[Battle] PlayerCritCheck: critChance={critChance:F2}% roll={roll:F2} crit={crit}");

        return crit;
    }

    // =========================================================
    // 命中判定（敵攻撃用）（追加）
    // =========================================================

    /// <summary>
    /// 敵の攻撃が命中するかどうかを判定する。
    ///
    /// 計算式:
    ///   最終命中率 = 敵基礎命中率 × (1 - プレイヤー回避率 / 100)
    ///   ただし最低10%保証。
    ///
    /// 敵はクリティカルを行わない。
    /// </summary>
    /// <param name="enemyBaseHitRate">敵の基礎命中率（%）</param>
    /// <returns>true: 命中、false: ミス</returns>
    private bool CheckEnemyHit(int enemyBaseHitRate)
    {
        float playerEvasion = (GameState.I != null) ? GameState.I.Evasion : 0f;

        float hitChance = enemyBaseHitRate * (1f - playerEvasion / 100f);

        if (hitChance < 10f) hitChance = 10f;
        if (hitChance > 100f) hitChance = 100f;

        float roll = Random.Range(0f, 100f);
        bool hit = roll < hitChance;

        Debug.Log($"[Battle] EnemyHitCheck: baseHit={enemyBaseHitRate} playerEvasion={playerEvasion:F2} " +
                  $"hitChance={hitChance:F2}% roll={roll:F2} hit={hit}");

        return hit;
    }

    // =========================================================
    // 属性耐性によるダメージ軽減（追加）
    // =========================================================

    /// <summary>
    /// プレイヤー→敵攻撃時に、敵の属性耐性でダメージを軽減する。
    ///
    /// 計算式:
    ///   最終ダメージ = baseDamage × (100 - 耐性値) / 100
    ///   耐性値が負（弱点）の場合はダメージ増加。
    ///   結果は最低1を保証する（元ダメージが1以上の場合）。
    ///
    /// 例: baseDamage=10, 耐性50 → 10 × 50/100 = 5
    /// 例: baseDamage=10, 耐性-50 → 10 × 150/100 = 15
    /// </summary>
    /// <param name="baseDamage">耐性適用前のダメージ</param>
    /// <param name="attackAttribute">攻撃の属性</param>
    /// <param name="resistanceLog">耐性適用のログ文字列（呼び出し側で使用）</param>
    /// <returns>耐性適用後のダメージ</returns>
    private int ApplyEnemyAttributeResistance(int baseDamage, WeaponAttribute attackAttribute, out string resistanceLog)
    {
        resistanceLog = "";
        if (enemyMonster == null) return baseDamage;

        int resistance = enemyMonster.GetAttributeResistance(attackAttribute);
        if (resistance == 0) return baseDamage;

        float reductionRate = resistance / 100f;
        int afterResist = Mathf.FloorToInt(baseDamage * (1f - reductionRate) + 0.5f);
        if (afterResist < 1 && baseDamage >= 1) afterResist = 1;
        // 完全無効（耐性100以上）の場合は0ダメージを許容
        if (resistance >= 100) afterResist = 0;

        if (resistance > 0)
            resistanceLog = "（耐性で軽減）";
        else if (resistance < 0)
            resistanceLog = "（弱点で増加）";

        Debug.Log($"[Battle] EnemyAttrResist: attr={attackAttribute} resistance={resistance} " +
                  $"baseDmg={baseDamage} afterResist={afterResist}");

        return afterResist;
    }

    // =========================================================
    // 防御ダイス
    // =========================================================

    /// <summary>
    /// DamageCategory に応じたプレイヤーの防御力を返す。
    /// Physical → GameState.Defense（VIT ベース + 装備 + パッシブ）
    /// Magical  → GameState.MagicDefense（INT ベース + 装備 + パッシブ）
    /// </summary>
    private int GetPlayerDefense(DamageCategory category)
    {
        if (GameState.I == null) return 0;
        int baseDef;
        switch (category)
        {
            case DamageCategory.Physical: baseDef = GameState.I.Defense; break;
            case DamageCategory.Magical: baseDef = GameState.I.MagicDefense; break;
            default: baseDef = GameState.I.Defense; break;
        }

        // Phase3: 防御バフ/デバフ適用（物理防御のみ）
        if (category == DamageCategory.Physical)
        {
            baseDef = StatusEffectSystem.ApplyDefenseBuffDebuff(
                baseDef,
                playerDefBuffTurn > 0, playerDefBuffRate,
                playerDefDebuffTurn > 0, playerDefDebuffRate);
        }

        return baseDef;
    }

    /// <summary>
    /// DamageCategory に応じた敵の防御力を返す。
    /// Physical → Monster.Defense
    /// Magical  → Monster.MagicDefense
    /// </summary>
    private int GetEnemyDefense(DamageCategory category)
    {
        if (enemyMonster == null) return 0;
        int baseDef;
        switch (category)
        {
            case DamageCategory.Physical: baseDef = enemyMonster.Defense; break;
            case DamageCategory.Magical: baseDef = enemyMonster.MagicDefense; break;
            default: baseDef = enemyMonster.Defense; break;
        }

        // Phase3: 防御バフ/デバフ適用（物理防御のみ）
        if (category == DamageCategory.Physical)
        {
            baseDef = StatusEffectSystem.ApplyDefenseBuffDebuff(
                baseDef,
                enemyDefBuffTurn > 0, enemyDefBuffRate,
                enemyDefDebuffTurn > 0, enemyDefDebuffRate);
        }

        return baseDef;
    }

    /// <summary>
    /// 防御ダイスの基準乱数範囲（通常時）。
    /// 0 ～ この値の乱数を振り、1未満が出た数の合計がダメージ軽減値。
    /// 通常時: 2.0f → 成功率 50%
    /// 強化時: 1.5f → 成功率 66.7%
    /// 弱体時: 3.0f → 成功率 33.3%
    /// </summary>
    private const float DefaultDefenseDiceRange = 2.0f;

    /// <summary>
    /// 防御ダイスを振り、ダメージ軽減値を返す。
    ///
    /// ルール:
    ///   防御力の数だけ乱数（0 ～ diceRange）を振り、
    ///   1未満が出た回数の合計がダメージ軽減値。
    ///
    /// diceRange パラメータで防御強化/弱体を表現する:
    ///   通常 = 2.0f（成功率50%）
    ///   強化 = 1.5f（成功率67%）
    ///   弱体 = 3.0f（成功率33%）
    ///
    /// 将来的にスキルやバフで diceRange を変化させる場合は、
    /// このメソッドの diceRange 引数を変えるだけで対応可能。
    /// </summary>
    /// <param name="defense">プレイヤーの防御力。</param>
    /// <param name="diceRange">
    /// 防御ダイスの乱数上限。省略時は DefaultDefenseDiceRange（通常時）。
    /// 下限は 1.0f にクランプ（1.0f 未満だと常に成功＝全防御になるため）。
    /// </param>
    /// <returns>ダメージ軽減値。</returns>
    private int RollDefenseDice(int defense, float diceRange = DefaultDefenseDiceRange)
    {
        if (defense <= 0) return 0;
        if (diceRange < 1.0f) diceRange = 1.0f;

        int blocked = 0;
        for (int i = 0; i < defense; i++)
        {
            if (Random.Range(0f, diceRange) < 1f)
                blocked++;
        }

        Debug.Log($"[Battle] DefenseDice: defense={defense} diceRange={diceRange} blocked={blocked}");
        return blocked;
    }

    /// <summary>
    /// プレイヤーにダメージを適用する共通処理。
    /// </summary>
    private void ApplyDamageToPlayer(int damage)
    {
        if (GameState.I == null) return;
        GameState.I.currentHp -= damage;
        if (GameState.I.currentHp < 0) GameState.I.currentHp = 0;
    }
}