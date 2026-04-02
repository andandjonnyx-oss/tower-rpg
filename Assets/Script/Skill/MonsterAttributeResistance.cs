using System;
using UnityEngine;

/// <summary>
/// モンスターの属性耐性1件分のデータ構造。
/// Monster.attributeResistances 配列に入れて使う。
///
/// 耐性値の仕様:
///   0   = 通常ダメージ（耐性なし）
///   50  = ダメージ50%軽減
///   100 = 完全無効
///   -50 = ダメージ50%増加（弱点）
///
/// 計算式:
///   最終ダメージ = 基礎ダメージ × (100 - resistance) / 100
///
/// 例: スライムに Strike耐性50, Pierce耐性50 を設定
///   → 殴・突攻撃は半減、斬(Slash)は通常ダメージ
/// </summary>
[Serializable]
public class MonsterAttributeResistance
{
    [Tooltip("耐性の対象属性")]
    public WeaponAttribute attribute;

    [Tooltip("耐性値（0=通常, 50=半減, 100=無効, 負値=弱点）")]
    public int value;
}