using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// インベントリ内の Magic カテゴリアイテムが持つパッシブ効果を集計する静的クラス。
///
/// 重複ルール:
///   同じ effectType + 同じ対象（属性 or ステータス）の効果が複数ある場合、
///   value が最も大きいものを 100% 適用し、
///   2個目以降は value の 10% ずつ加算する（切り捨て）。
///
/// 例: 火属性耐性 50 のアイテムを 10 個所持
///   → 50 + 5×9 = 95
/// 例: 火属性耐性 70 のアイテム1個 + 火属性耐性 50 のアイテム2個
///   → 70 + 7 + 5 = 82
///   （70 を 100% 適用、残りは各 value の 10% を加算）
///
/// 使い方:
///   int fireRes  = PassiveCalculator.CalcAttributeResistance(WeaponAttribute.Fire);
///   int atkBonus = PassiveCalculator.CalcAttackBonus();
///
/// 属性耐性の合算（装備＋パッシブ）:
///   int totalRes = PassiveCalculator.CalcTotalAttributeResistance(WeaponAttribute.Fire);
///   → EquipmentCalculator.GetAttributeResistance(Fire) + CalcAttributeResistance(Fire)
/// </summary>
public static class PassiveCalculator
{
    // =========================================================
    // 公開メソッド ── ターゲット指定あり（属性）
    // =========================================================

    /// <summary>
    /// 指定属性の耐性合計値を返す（パッシブのみ）。
    /// インベントリ内の全 Magic アイテムから
    /// PassiveType.AttributeResistance かつ対象属性が一致するものを収集して計算する。
    /// </summary>
    public static int CalcAttributeResistance(WeaponAttribute attr)
    {
        var values = CollectValues(PassiveType.AttributeResistance, attr, default);
        return CalcWithDiminishing(values);
    }

    /// <summary>
    /// 指定属性の耐性合計値を返す（装備＋パッシブの合算）。
    ///
    /// 計算式:
    ///   EquipmentCalculator.GetAttributeResistance(attr)  ← 装備品分（100%反映）
    ///   + CalcAttributeResistance(attr)                    ← パッシブ分（重複ルール適用）
    ///
    /// BattleSceneController の敵スキル攻撃ダメージ計算で使用する。
    ///
    /// 例: 炎耐性50の武器 + 炎耐性50のパッシブアイテム1個
    ///   → 50(装備) + 50(パッシブ) = 100（完全耐性）
    /// </summary>
    public static int CalcTotalAttributeResistance(WeaponAttribute attr)
    {
        int equipRes = EquipmentCalculator.GetAttributeResistance(attr);
        int passiveRes = CalcAttributeResistance(attr);
        return equipRes + passiveRes;
    }

    /// <summary>
    /// 指定属性の攻撃力ボーナス合計値を返す。
    /// </summary>
    public static int CalcAttributeAttackBonus(WeaponAttribute attr)
    {
        var values = CollectValues(PassiveType.AttributeAttackBonus, attr, default);
        return CalcWithDiminishing(values);
    }

    // =========================================================
    // 公開メソッド ── ターゲット指定あり（ステータス）
    // =========================================================

    /// <summary>
    /// 指定ステータスのパッシブボーナス合計値を返す。
    /// インベントリ内の全 Magic アイテムから
    /// PassiveType.StatBonus かつ対象ステータスが一致するものを収集して計算する。
    /// </summary>
    public static int CalcStatBonus(StatType stat)
    {
        var values = CollectValues(PassiveType.StatBonus, default, stat);
        return CalcWithDiminishing(values);
    }

    // =========================================================
    // 公開メソッド ── ターゲット指定なし（各ステータス専用）
    // =========================================================

    /// <summary>最大HPボーナス合計値を返す。</summary>
    public static int CalcMaxHpBonus()
    {
        var values = CollectValuesNoTarget(PassiveType.MaxHpBonus);
        return CalcWithDiminishing(values);
    }

    /// <summary>最大MPボーナス合計値を返す。</summary>
    public static int CalcMaxMpBonus()
    {
        var values = CollectValuesNoTarget(PassiveType.MaxMpBonus);
        return CalcWithDiminishing(values);
    }

    /// <summary>攻撃力ボーナス合計値を返す。</summary>
    public static int CalcAttackBonus()
    {
        var values = CollectValuesNoTarget(PassiveType.AttackBonus);
        return CalcWithDiminishing(values);
    }

    /// <summary>防御力ボーナス合計値を返す。</summary>
    public static int CalcDefenseBonus()
    {
        var values = CollectValuesNoTarget(PassiveType.DefenseBonus);
        return CalcWithDiminishing(values);
    }

    /// <summary>魔法攻撃力ボーナス合計値を返す。</summary>
    public static int CalcMagicAttackBonus()
    {
        var values = CollectValuesNoTarget(PassiveType.MagicAttackBonus);
        return CalcWithDiminishing(values);
    }

    /// <summary>魔法防御力ボーナス合計値を返す。</summary>
    public static int CalcMagicDefenseBonus()
    {
        var values = CollectValuesNoTarget(PassiveType.MagicDefenseBonus);
        return CalcWithDiminishing(values);
    }

    /// <summary>運の良さボーナス合計値を返す。</summary>
    public static int CalcLuckBonus()
    {
        var values = CollectValuesNoTarget(PassiveType.LuckBonus);
        return CalcWithDiminishing(values);
    }

    // =========================================================
    // 公開メソッド ── 命中力・回避率・クリティカル率（追加）
    // =========================================================

    /// <summary>
    /// 命中力ボーナス合計値（int）を返す。
    /// PassiveType.AccuracyBonus の value を収集して重複ルール適用。
    /// </summary>
    public static int CalcAccuracyBonus()
    {
        var values = CollectValuesNoTarget(PassiveType.AccuracyBonus);
        return CalcWithDiminishing(values);
    }

    /// <summary>
    /// 回避率ボーナス合計値（float 小数点2位精度）を返す。
    /// PassiveType.EvasionBonus の floatValue を収集して重複ルール適用。
    ///
    /// 重複ルール（float版）:
    ///   value を降順ソートし、1個目は100%、2個目以降は10%（小数点3位を四捨五入→2位精度）。
    ///   例: [3.50, 2.00, 2.00] → 3.50 + 0.20 + 0.20 = 3.90
    /// </summary>
    public static float CalcEvasionBonus()
    {
        var values = CollectFloatValuesNoTarget(PassiveType.EvasionBonus);
        return CalcWithDiminishingFloat2(values);
    }

    /// <summary>
    /// クリティカル率ボーナス合計値（float 小数点2位精度）を返す。
    /// PassiveType.CriticalBonus の floatValue を収集して重複ルール適用。
    /// </summary>
    public static float CalcCriticalBonus()
    {
        var values = CollectFloatValuesNoTarget(PassiveType.CriticalBonus);
        return CalcWithDiminishingFloat2(values);
    }

    // =========================================================
    // 魔法スキル一覧の収集
    // =========================================================

    /// <summary>
    /// インベントリ内の Magic アイテムが付与する魔法スキル一覧を返す。
    /// 同じ skillId のスキルは重複しない（最初に見つかったもののみ）。
    /// </summary>
    public static List<SkillData> CollectMagicSkills()
    {
        var result = new List<SkillData>();
        var seenIds = new HashSet<string>();

        var items = GetInventoryItems();
        if (items == null) return result;

        for (int i = 0; i < items.Count; i++)
        {
            var invItem = items[i];
            if (invItem?.data == null) continue;
            if (invItem.data.category != ItemCategory.Magic) continue;
            if (invItem.data.magicSkill == null) continue;

            var skill = invItem.data.magicSkill;

            // 同じ skillId のスキルは重複させない
            if (string.IsNullOrEmpty(skill.skillId)) continue;
            if (seenIds.Contains(skill.skillId)) continue;

            seenIds.Add(skill.skillId);
            result.Add(skill);
        }

        return result;
    }

    // =========================================================
    // 内部: 効果値の収集
    // =========================================================

    /// <summary>
    /// インベントリ内の全 Magic アイテムから、指定条件に合致するパッシブ効果の value を収集する。
    /// targetAttribute または targetStat で対象を絞り込む。
    /// </summary>
    private static List<int> CollectValues(PassiveType type, WeaponAttribute attrFilter, StatType statFilter)
    {
        var values = new List<int>();

        var items = GetInventoryItems();
        if (items == null) return values;

        for (int i = 0; i < items.Count; i++)
        {
            var invItem = items[i];
            if (invItem?.data == null) continue;
            if (invItem.data.category != ItemCategory.Magic) continue;
            if (invItem.data.passiveEffects == null) continue;

            for (int j = 0; j < invItem.data.passiveEffects.Length; j++)
            {
                var pe = invItem.data.passiveEffects[j];
                if (pe == null) continue;
                if (pe.effectType != type) continue;

                // 対象の絞り込み
                switch (type)
                {
                    case PassiveType.AttributeResistance:
                    case PassiveType.AttributeAttackBonus:
                        if (pe.targetAttribute != attrFilter) continue;
                        break;

                    case PassiveType.StatBonus:
                        if (pe.targetStat != statFilter) continue;
                        break;

                        // ターゲット指定なし系は対象フィルタ不要
                }

                if (pe.value > 0)
                    values.Add(pe.value);
            }
        }

        return values;
    }

    /// <summary>
    /// targetAttribute / targetStat を使わない効果の value を収集する。
    /// MaxHpBonus / MaxMpBonus / AttackBonus / DefenseBonus /
    /// MagicAttackBonus / MagicDefenseBonus / LuckBonus / AccuracyBonus 等が対象。
    /// </summary>
    private static List<int> CollectValuesNoTarget(PassiveType type)
    {
        var values = new List<int>();

        var items = GetInventoryItems();
        if (items == null) return values;

        for (int i = 0; i < items.Count; i++)
        {
            var invItem = items[i];
            if (invItem?.data == null) continue;
            if (invItem.data.category != ItemCategory.Magic) continue;
            if (invItem.data.passiveEffects == null) continue;

            for (int j = 0; j < invItem.data.passiveEffects.Length; j++)
            {
                var pe = invItem.data.passiveEffects[j];
                if (pe == null) continue;
                if (pe.effectType != type) continue;

                if (pe.value > 0)
                    values.Add(pe.value);
            }
        }

        return values;
    }

    /// <summary>
    /// float 精度が必要なパッシブ効果の floatValue を収集する。
    /// EvasionBonus / CriticalBonus 等が対象。
    /// floatValue が 0 で value が設定されている場合は value を float に変換して使う。
    /// </summary>
    private static List<float> CollectFloatValuesNoTarget(PassiveType type)
    {
        var values = new List<float>();

        var items = GetInventoryItems();
        if (items == null) return values;

        for (int i = 0; i < items.Count; i++)
        {
            var invItem = items[i];
            if (invItem?.data == null) continue;
            if (invItem.data.category != ItemCategory.Magic) continue;
            if (invItem.data.passiveEffects == null) continue;

            for (int j = 0; j < invItem.data.passiveEffects.Length; j++)
            {
                var pe = invItem.data.passiveEffects[j];
                if (pe == null) continue;
                if (pe.effectType != type) continue;

                // floatValue を優先、未設定（0）なら value を float に変換
                float v = (pe.floatValue != 0f) ? pe.floatValue : (float)pe.value;
                if (v > 0f)
                    values.Add(v);
            }
        }

        return values;
    }

    // =========================================================
    // 内部: 重複ルール計算
    // =========================================================

    /// <summary>
    /// 効果値リストに対して重複ルールを適用し、合計値を返す。
    /// ルール: value を降順にソートし、1個目は 100% 適用、2個目以降は各 value の 10%（切り捨て）を加算。
    /// 例: [70, 50, 50] → 70 + 7 + 5 = 82
    /// 例: [50, 50, 50, 50, 50, 50, 50, 50, 50, 50] → 50 + 5×9 = 95
    /// </summary>
    private static int CalcWithDiminishing(List<int> values)
    {
        if (values == null || values.Count == 0) return 0;

        // 降順ソート（大きい値から適用）
        values.Sort((a, b) => b.CompareTo(a));

        // 1個目は 100% 適用
        int total = values[0];

        // 2個目以降は各 value の 10%（切り捨て）を加算
        for (int i = 1; i < values.Count; i++)
        {
            total += values[i] / 10;
        }

        return total;
    }

    /// <summary>
    /// float 版の重複ルール計算（小数点2位精度）。
    /// EvasionBonus / CriticalBonus で使用する。
    ///
    /// ルール: value を降順ソートし、1個目は100%、2個目以降は各 value の10%を加算。
    /// 各ステップの結果を小数点3位で四捨五入して小数点2位精度を維持する。
    ///
    /// 例: [3.50, 2.00, 2.00]
    ///   → 3.50 + Round(0.200, 2) + Round(0.200, 2) = 3.50 + 0.20 + 0.20 = 3.90
    /// 例: [5.75, 3.25, 1.50]
    ///   → 5.75 + Round(0.325, 2) + Round(0.150, 2) = 5.75 + 0.33 + 0.15 = 6.23
    /// </summary>
    private static float CalcWithDiminishingFloat2(List<float> values)
    {
        if (values == null || values.Count == 0) return 0f;

        // 降順ソート（大きい値から適用）
        values.Sort((a, b) => b.CompareTo(a));

        // 1個目は 100% 適用
        float total = values[0];

        // 2個目以降は各 value の 10% を加算（小数点3位を四捨五入→2位精度）
        for (int i = 1; i < values.Count; i++)
        {
            float bonus = values[i] * 0.1f;
            // 小数点3位を四捨五入して小数点2位精度にする
            bonus = Mathf.Floor(bonus * 100f + 0.5f) / 100f;
            total += bonus;
        }

        // 最終結果も小数点2位に丸める
        total = Mathf.Floor(total * 100f + 0.5f) / 100f;

        return total;
    }

    // =========================================================
    // 内部: インベントリアクセス
    // =========================================================

    /// <summary>
    /// ItemBoxManager からインベントリのアイテム一覧を取得する。
    /// </summary>
    private static System.Collections.Generic.IReadOnlyList<InventoryItem> GetInventoryItems()
    {
        if (ItemBoxManager.Instance == null) return null;
        return ItemBoxManager.Instance.GetItems();
    }
}