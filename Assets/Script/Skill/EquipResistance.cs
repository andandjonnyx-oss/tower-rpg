using System;
using UnityEngine;

/// <summary>
/// 装備品の属性耐性1件分のデータ構造。
/// ItemData.equipResistances 配列に入れて使う。
///
/// パッシブ効果の重複ルール（2個目以降10%減衰）とは異なり、
/// 装備品の耐性は常に value を100%そのまま加算する。
/// （装備は1つしか装備できないため重複ルールは不要）
///
/// 例: 炎耐性50の武器 → EquipResistance(Fire, 50) を設定。
/// 例: 氷耐性30 + 雷耐性20の武器 → 2件の EquipResistance を設定。
/// </summary>
[Serializable]
public class EquipResistance
{
    [Tooltip("耐性の対象属性")]
    public WeaponAttribute attribute;

    [Tooltip("耐性値（100で完全耐性）。パッシブとは別計算で100%反映される。")]
    public int value;
}