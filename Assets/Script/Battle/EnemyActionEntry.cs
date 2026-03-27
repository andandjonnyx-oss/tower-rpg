using System;
using UnityEngine;

/// <summary>
/// 敵の行動テーブルの1エントリ。
/// Monster.actions 配列に並べて使う。
///
/// 変更履歴:
///   旧仕様: 行動の内容（ダメージ・属性・DamageCategory）を EnemyActionEntry に直書きしていた。
///   新仕様: 行動の内容は MonsterSkillData（ScriptableObject）に分離した。
///           EnemyActionEntry は「どのスキルを・どの確率で選ぶか」だけを管理する。
///
/// 判定ロジック:
///   0 ～ actionRange の乱数を振り、
///   actions[i].threshold の範囲に入った行動を実行する。
///   例: threshold=30 の「通常攻撃」, threshold=50 の「炎攻撃」, threshold=100 の「何もしない」
///   → 乱数 0-29=通常攻撃, 30-49=炎攻撃, 50-99=何もしない
///
/// actionRange はプレイヤーと敵の LUC 差によって変動する。
/// actionRange が小さくなると、threshold が高い行動（=弱い行動）が選ばれやすくなる。
/// </summary>
[Serializable]
public class EnemyActionEntry
{
    /// <summary>
    /// 使用するスキルのデータ。
    /// null の場合は fallback として通常攻撃（物理、Monster.Attack 依存）を実行する。
    /// </summary>
    [Tooltip("使用するモンスタースキル。null の場合は通常攻撃フォールバック")]
    public MonsterSkillData skill;

    /// <summary>
    /// この行動が選ばれる上限値（累積）。
    /// 0 から threshold-1 までの乱数結果がこの行動に該当する
    /// （前の行動の threshold から、この threshold-1 までの範囲）。
    /// 最後の行動の threshold は actionRange（通常100）と一致させる。
    /// </summary>
    [Tooltip("行動判定テーブルの上限値（累積）\n"
           + "例: 30, 50, 100 → 0-29=1番目, 30-49=2番目, 50-99=3番目")]
    public int threshold;
}

// =========================================================
// 以下の enum は他のファイルから参照されるためここに残す
// =========================================================

/// <summary>
/// 攻撃の物理/魔法区分。
/// 属性（Fire, Ice 等）とは独立した概念。
/// 例: 炎攻撃 = 魔法、火炎斬り = 物理（どちらも Fire 属性）。
/// 防御ダイスの参照先が変わる（物理→Defense、魔法→MagicDefense）。
/// </summary>
public enum DamageCategory
{
    /// <summary>物理攻撃。Defense で防御。</summary>
    Physical,

    /// <summary>魔法攻撃。MagicDefense で防御。</summary>
    Magical,
}