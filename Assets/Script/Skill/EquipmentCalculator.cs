using UnityEngine;

/// <summary>
/// 装備中の武器から各種ステータス補正を取得する静的クラス。
///
/// パッシブ効果との違い:
///   パッシブ（PassiveCalculator）は複数アイテムの重複ルール（2個目以降10%減衰）を適用する。
///   装備品（このクラス）は装備が1つしか存在しないため、補正値を常に100%そのまま返す。
///
/// 計算の流れ:
///   GameState.Attack = baseSTR × 1 + EquipmentCalculator.GetAttackPower()
///                                   + PassiveCalculator.CalcAttackBonus()
///
/// 装備品のステータスが変わるケース:
///   - 武器を装備/解除した時
///   - 装備中武器を捨てた時
///   → これらの後に RecalcMaxHp/RecalcMaxMp を呼ぶこと。
///
/// 使い方:
///   int weaponAtk = EquipmentCalculator.GetAttackPower();
///   int weaponDef = EquipmentCalculator.GetDefense();
///   int fireRes   = EquipmentCalculator.GetAttributeResistance(WeaponAttribute.Fire);
/// </summary>
public static class EquipmentCalculator
{
    // =========================================================
    // 装備中武器の ItemData を取得
    // =========================================================

    /// <summary>
    /// 現在装備中の武器の ItemData を返す。未装備なら null。
    /// </summary>
    private static ItemData GetEquippedWeaponData()
    {
        if (GameState.I == null) return null;
        if (string.IsNullOrEmpty(GameState.I.equippedWeaponUid)) return null;
        if (ItemBoxManager.Instance == null) return null;

        var items = ItemBoxManager.Instance.GetItems();
        if (items == null) return null;

        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] != null && items[i].uid == GameState.I.equippedWeaponUid)
            {
                if (items[i].data != null && items[i].data.category == ItemCategory.Weapon)
                    return items[i].data;
                return null;
            }
        }

        return null;
    }

    // =========================================================
    // 各ステータス補正の取得
    // =========================================================

    /// <summary>
    /// 装備中武器の攻撃力を返す。未装備なら0。
    /// ItemData.attackPower をそのまま100%返す。
    /// </summary>
    public static int GetAttackPower()
    {
        var data = GetEquippedWeaponData();
        if (data == null) return 0;
        return data.attackPower;
    }

    /// <summary>
    /// 装備中武器の防御力補正を返す。未装備なら0。
    /// ItemData.equipDefense をそのまま100%返す。
    /// </summary>
    public static int GetDefense()
    {
        var data = GetEquippedWeaponData();
        if (data == null) return 0;
        return data.equipDefense;
    }

    /// <summary>
    /// 装備中武器の魔法攻撃力補正を返す。未装備なら0。
    /// ItemData.equipMagicAttack をそのまま100%返す。
    /// </summary>
    public static int GetMagicAttack()
    {
        var data = GetEquippedWeaponData();
        if (data == null) return 0;
        return data.equipMagicAttack;
    }

    /// <summary>
    /// 装備中武器の魔法防御力補正を返す。未装備なら0。
    /// ItemData.equipMagicDefense をそのまま100%返す。
    /// </summary>
    public static int GetMagicDefense()
    {
        var data = GetEquippedWeaponData();
        if (data == null) return 0;
        return data.equipMagicDefense;
    }

    /// <summary>
    /// 装備中武器の運の良さ補正を返す。未装備なら0。
    /// ItemData.equipLuck をそのまま100%返す。
    /// </summary>
    public static int GetLuck()
    {
        var data = GetEquippedWeaponData();
        if (data == null) return 0;
        return data.equipLuck;
    }

    /// <summary>
    /// 装備中武器の最大HPボーナスを返す。未装備なら0。
    /// ItemData.equipMaxHp をそのまま100%返す。
    /// </summary>
    public static int GetMaxHpBonus()
    {
        var data = GetEquippedWeaponData();
        if (data == null) return 0;
        return data.equipMaxHp;
    }

    /// <summary>
    /// 装備中武器の最大MPボーナスを返す。未装備なら0。
    /// ItemData.equipMaxMp をそのまま100%返す。
    /// </summary>
    public static int GetMaxMpBonus()
    {
        var data = GetEquippedWeaponData();
        if (data == null) return 0;
        return data.equipMaxMp;
    }

    /// <summary>
    /// 装備中武器の指定属性耐性値を返す。未装備 or 該当属性なしなら0。
    /// ItemData.equipResistances から対象属性の value をそのまま100%返す。
    /// </summary>
    public static int GetAttributeResistance(WeaponAttribute attr)
    {
        var data = GetEquippedWeaponData();
        if (data == null) return 0;
        if (data.equipResistances == null) return 0;

        for (int i = 0; i < data.equipResistances.Length; i++)
        {
            if (data.equipResistances[i] != null &&
                data.equipResistances[i].attribute == attr)
            {
                return data.equipResistances[i].value;
            }
        }

        return 0;
    }
}