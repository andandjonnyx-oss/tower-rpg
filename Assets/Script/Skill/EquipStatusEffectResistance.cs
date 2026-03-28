using System;
using UnityEngine;

/// <summary>
/// 装備品の状態異常耐性1件分のデータ構造。
/// ItemData.equipStatusEffectResistances 配列に入れて使う。
///
/// ★ブラッシュアップ:
///   属性耐性（EquipResistance）と同じ構造で、状態異常耐性を定義する。
///   武器（100%反映）でもパッシブアイテム（重複ルール適用）でも、
///   同じパラメータセットを持てるように統一。
///
/// パッシブ効果の重複ルール（2個目以降10%減衰）とは異なり、
/// 装備品の耐性は常に value を100%そのまま加算する。
/// （装備は1つしか装備できないため重複ルールは不要）
///
/// 例: 毒耐性30の武器 → EquipStatusEffectResistance(Poison, 30) を設定。
/// 例: 毒耐性20 + 麻痺耐性15の武器 → 2件の EquipStatusEffectResistance を設定。
/// </summary>
[Serializable]
public class EquipStatusEffectResistance
{
    [Tooltip("耐性の対象状態異常")]
    public StatusEffect statusEffect;

    [Tooltip("耐性値（100で完全耐性）。パッシブとは別計算で100%反映される。")]
    public int value;
}