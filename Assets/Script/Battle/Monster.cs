using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Battle/Monster")]
public class Monster : ScriptableObject
{
    [Header("ID")]
    public string ID;

    [Header("Name")]
    public string Mname;

    [Header("Image")]
    public Sprite Image;

    [Header("Range")]
    public int Minfloor;
    public int Minstep;
    public int Maxfloor;
    public int Maxstep;


    [Header("出現制御")]
    public int Weight = 1;
    public bool IsBoss;
    public bool IsUnique;

    [Header("Stats")]
    public int MaxHp;
    public int Attack;
    public int Defense;
    public int MagicDefense;
    public int Speed;
    public int Luck;

    // =========================================================
    // 命中・回避（追加）
    // =========================================================

    [Header("Hit / Evasion")]
    [Tooltip("敵の回避力（int）。プレイヤーの命中力との差で命中率が変動する。\n"
           + "0 = 回避しない。値が高いほどプレイヤーの攻撃が外れやすくなる。")]
    public int Evasion = 0;

    [Tooltip("敵の通常攻撃の基礎命中率（%）。デフォルト90。\n"
           + "敵の攻撃命中率 = BaseHitRate × (1 - プレイヤー回避率/100)\n"
           + "ただし最低10%保証。")]
    public int BaseHitRate = 90;

    // =========================================================
    // 状態異常耐性（追加）
    // =========================================================

    [Header("Status Effect Resistance")]
    [Tooltip("敵の毒耐性値（0?100）。\n"
           + "毒の実質命中率 = 基礎命中率 × (1 - PoisonResistance/100)\n"
           + "100 = 毒完全耐性。")]
    public int PoisonResistance = 0;

    [Header("Reward")]
    public int Exp;
    public int Gold;

    [Header("説明")]
    [TextArea(3, 6)]
    public string Help;

    // =========================================================
    // 敵の行動パターン
    // =========================================================

    [Header("Action Pattern")]
    [Tooltip("敵の行動テーブル。threshold の昇順に並べる。\n"
           + "最後の threshold を baseActionRange に合わせること。\n"
           + "空の場合は従来通り Attack 依存の通常攻撃のみ行う。")]
    public EnemyActionEntry[] actions;

    [Tooltip("行動判定の乱数上限の基準値（通常は100）。\n"
           + "LUC 差によってこの値が増減する。")]
    public int baseActionRange = 100;

}