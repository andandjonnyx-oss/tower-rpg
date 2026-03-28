using UnityEngine;

/// <summary>
/// 状態異常の付与判定・ダメージ計算を担う静的ユーティリティクラス。
///
/// 毒（Poison）の仕様:
///   戦闘中: ターン終了時に最大HPの5%ダメージ（四捨五入、最低1）
///   塔移動中: 1歩ごとに最大HPの3%ダメージ（四捨五入、最低1）、10%で自然治癒
///   戦闘終了後もプレイヤーの毒状態は残る
///
/// 付与判定:
///   実質命中率 = 基礎命中率 × (1 - 耐性/100)
///   耐性50なら、ポイズン（80%）の実質命中率は 80 × (1 - 50/100) = 40%
///
/// ★ブラッシュアップ:
///   プレイヤーの毒耐性は装備品＋パッシブの合算で計算する。
///   EquipmentCalculator.GetStatusEffectResistance(Poison)
///   + PassiveCalculator.CalcStatusEffectResistance(Poison)
///
/// 将来的に他の状態異常を追加する場合もこのクラスに集約する。
/// </summary>
public static class StatusEffectSystem
{
    // =========================================================
    // 毒の定数
    // =========================================================

    /// <summary>戦闘中の毒ダメージ: 最大HPの何%か。</summary>
    private const float BattlePoisonPercent = 5f;

    /// <summary>塔移動中の毒ダメージ: 最大HPの何%か。</summary>
    private const float TowerPoisonPercent = 3f;

    /// <summary>塔移動中の毒自然治癒確率（%）。</summary>
    private const float TowerPoisonCureChance = 10f;

    // =========================================================
    // 毒の付与判定
    // =========================================================

    /// <summary>
    /// 状態異常の付与判定を行う。
    ///
    /// 計算式:
    ///   実質命中率 = baseChance × (1 - targetResistance / 100)
    ///   例: baseChance=80, targetResistance=50 → 80 × 0.5 = 40%
    ///
    /// </summary>
    /// <param name="baseChance">状態異常の基礎付与率（%）。例: ポイズン=80</param>
    /// <param name="targetResistance">対象の状態異常耐性値。例: 毒耐性=50</param>
    /// <returns>true: 付与成功、false: 付与失敗</returns>
    public static bool TryInflict(float baseChance, int targetResistance)
    {
        // 耐性が100以上なら完全耐性
        if (targetResistance >= 100) return false;

        float effectiveChance = baseChance * (1f - targetResistance / 100f);
        if (effectiveChance <= 0f) return false;

        float roll = Random.Range(0f, 100f);
        bool success = roll < effectiveChance;

        Debug.Log($"[StatusEffect] TryInflict: base={baseChance}% resist={targetResistance} " +
                  $"effective={effectiveChance:F2}% roll={roll:F2} success={success}");

        return success;
    }

    // =========================================================
    // 毒ダメージ計算
    // =========================================================

    /// <summary>
    /// 戦闘中の毒ダメージを計算する。
    /// 最大HPの5%（四捨五入）。最低1ダメージ保証。
    /// </summary>
    /// <param name="maxHp">対象の最大HP</param>
    /// <returns>毒ダメージ量</returns>
    public static int CalcBattlePoisonDamage(int maxHp)
    {
        int damage = Mathf.FloorToInt(maxHp * BattlePoisonPercent / 100f + 0.5f);
        if (damage < 1) damage = 1;
        return damage;
    }

    /// <summary>
    /// 塔移動中の毒ダメージを計算する。
    /// 最大HPの3%（四捨五入）。最低1ダメージ保証。
    /// </summary>
    /// <param name="maxHp">対象の最大HP</param>
    /// <returns>毒ダメージ量</returns>
    public static int CalcTowerPoisonDamage(int maxHp)
    {
        int damage = Mathf.FloorToInt(maxHp * TowerPoisonPercent / 100f + 0.5f);
        if (damage < 1) damage = 1;
        return damage;
    }

    /// <summary>
    /// 塔移動中の毒自然治癒判定を行う。
    /// 10%の確率で治癒する。
    /// </summary>
    /// <returns>true: 治癒、false: 継続</returns>
    public static bool TryNaturalCure()
    {
        float roll = Random.Range(0f, 100f);
        bool cured = roll < TowerPoisonCureChance;

        Debug.Log($"[StatusEffect] TryNaturalCure: chance={TowerPoisonCureChance}% " +
                  $"roll={roll:F2} cured={cured}");

        return cured;
    }

    // =========================================================
    // プレイヤーへの毒ダメージ適用（戦闘中）
    // =========================================================

    /// <summary>
    /// 戦闘中のプレイヤー毒ダメージを適用する。
    /// GameState.isPoisoned が true の場合のみダメージを与える。
    /// HPが0以下になっても最低0にクランプする（戦闘不能判定は呼び出し元で行う）。
    /// </summary>
    /// <returns>与えたダメージ量。毒でなければ0。</returns>
    public static int ApplyBattlePoisonToPlayer()
    {
        if (GameState.I == null) return 0;
        if (!GameState.I.isPoisoned) return 0;

        int damage = CalcBattlePoisonDamage(GameState.I.maxHp);
        GameState.I.currentHp -= damage;
        if (GameState.I.currentHp < 0) GameState.I.currentHp = 0;

        Debug.Log($"[StatusEffect] Player poison damage: {damage} (HP: {GameState.I.currentHp}/{GameState.I.maxHp})");
        return damage;
    }

    /// <summary>
    /// 塔移動中のプレイヤー毒ダメージを適用し、自然治癒も判定する。
    /// </summary>
    /// <param name="damage">出力: 受けたダメージ量</param>
    /// <param name="cured">出力: 自然治癒したかどうか</param>
    /// <returns>true: 毒状態だった（ダメージ発生）、false: 毒でなかった</returns>
    public static bool ApplyTowerPoisonToPlayer(out int damage, out bool cured)
    {
        damage = 0;
        cured = false;

        if (GameState.I == null) return false;
        if (!GameState.I.isPoisoned) return false;

        // ダメージ適用
        damage = CalcTowerPoisonDamage(GameState.I.maxHp);
        GameState.I.currentHp -= damage;
        if (GameState.I.currentHp < 1) GameState.I.currentHp = 1; // 塔移動中は最低HP1保証

        // 自然治癒判定
        cured = TryNaturalCure();
        if (cured)
        {
            GameState.I.isPoisoned = false;
            Debug.Log("[StatusEffect] Player poison cured naturally!");
        }

        return true;
    }

    // =========================================================
    // プレイヤーの毒耐性取得
    // =========================================================

    /// <summary>
    /// プレイヤーの毒耐性値を返す。
    /// ★ブラッシュアップ: 装備品（100%反映）＋パッシブ（重複ルール適用）の合算。
    ///
    /// 計算式:
    ///   EquipmentCalculator.GetStatusEffectResistance(Poison)  ← 装備品分
    ///   + PassiveCalculator.CalcStatusEffectResistance(Poison) ← パッシブ分
    ///
    /// 属性耐性の CalcTotalAttributeResistance() と同じ構造。
    ///
    /// 例: 毒耐性30の武器 + 毒耐性50のパッシブアイテム1個
    ///   → 30(装備) + 50(パッシブ) = 80
    /// </summary>
    public static int GetPlayerPoisonResistance()
    {
        int equipRes = EquipmentCalculator.GetStatusEffectResistance(StatusEffect.Poison);
        int passiveRes = PassiveCalculator.CalcStatusEffectResistance(StatusEffect.Poison);
        return equipRes + passiveRes;
    }

    // =========================================================
    // プレイヤーへの毒付与
    // =========================================================

    /// <summary>
    /// プレイヤーに毒を付与する試行。耐性を考慮する。
    /// ★ブラッシュアップ: 既に毒の場合は判定せず false を返す。
    /// </summary>
    /// <param name="baseChance">基礎付与率（%）</param>
    /// <returns>true: 毒を付与した、false: 付与失敗（耐性 or 確率外れ or 既に毒）</returns>
    public static bool TryPoisonPlayer(float baseChance)
    {
        if (GameState.I == null) return false;
        if (GameState.I.isPoisoned) return false; // 既に毒

        int resistance = GetPlayerPoisonResistance();
        bool inflicted = TryInflict(baseChance, resistance);

        if (inflicted)
        {
            GameState.I.isPoisoned = true;
            Debug.Log("[StatusEffect] Player is now poisoned!");
        }

        return inflicted;
    }

    /// <summary>
    /// プレイヤーの毒を治癒する。
    /// </summary>
    public static void CurePlayerPoison()
    {
        if (GameState.I == null) return;
        GameState.I.isPoisoned = false;
        Debug.Log("[StatusEffect] Player poison cured!");
    }
}