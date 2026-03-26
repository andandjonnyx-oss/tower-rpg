using System;
using UnityEngine;

/// <summary>
/// 敵の行動1つ分を定義するデータクラス。
/// Monster の actions 配列に入れて使う。
///
/// 判定ロジック:
///   0 ～ actionRange の乱数を振り、
///   actions[i].threshold の範囲に入った行動を実行する。
///   例: threshold=30 の「物理攻撃」, threshold=50 の「炎攻撃」, threshold=100 の「何もしない」
///   → 乱数 0-29=物理攻撃, 30-49=炎攻撃, 50-99=何もしない
///
/// actionRange はプレイヤーと敵の LUC 差によって変動する。
/// actionRange が小さくなると、threshold が高い行動（=弱い行動）が選ばれやすくなる。
/// </summary>
[Serializable]
public class EnemyActionEntry
{
    /// <summary>行動の種類。</summary>
    [Tooltip("この行動の種類")]
    public EnemyActionType actionType;

    /// <summary>
    /// この行動が選ばれる上限値。
    /// 0 から threshold-1 までの乱数結果がこの行動に該当する
    /// （前の行動の threshold から、この threshold-1 までの範囲）。
    /// 最後の行動の threshold は actionRange（通常100）と一致させる。
    /// </summary>
    [Tooltip("行動判定テーブルの上限値（累積）")]
    public int threshold;

    [Header("攻撃パラメータ（actionType が Attack 以外の場合に使用）")]

    /// <summary>固定ダメージ値。0 の場合は Monster.Attack を使用。</summary>
    [Tooltip("固定ダメージ。0 なら Monster.Attack 依存")]
    public int fixedDamage;

    /// <summary>攻撃の属性。Strike=物理属性。</summary>
    [Tooltip("攻撃の属性（Fire=炎攻撃 等）")]
    public WeaponAttribute attackAttribute;

    /// <summary>ログに表示する行動名。空の場合はデフォルト名を使用。</summary>
    [Tooltip("戦闘ログに表示する行動名（空=デフォルト）")]
    public string actionName;
}

/// <summary>
/// 敵の行動の種類。
/// </summary>
public enum EnemyActionType
{
    /// <summary>通常攻撃（Monster.Attack 依存ダメージ）。</summary>
    Attack,

    /// <summary>特殊攻撃（固定ダメージ + 属性付き）。</summary>
    SpecialAttack,

    /// <summary>何もしない（ターン終了）。</summary>
    Idle,
}