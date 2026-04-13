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
    [Tooltip("出現重み。値が大きいほど出やすい。\n"
           + "通常モンスター = 1、レアモンスター = 0.1 など。\n"
           + "同じ地点に複数モンスターがいる場合、Weight / 合計Weight の確率で選ばれる。")]
    public float Weight = 1f;
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
    // 状態異常耐性
    // =========================================================

    [Header("Status Effect Resistance")]
    [Tooltip("敵の毒耐性値（0〜100）。\n"
           + "毒の実質命中率 = 基礎命中率 × (1 - PoisonResistance/100)\n"
           + "100 = 毒完全耐性。")]
    public int PoisonResistance = 0;

    [Tooltip("敵の気絶耐性値（0〜100）。\n"
           + "気絶の実質命中率 = 基礎命中率 × (1 - StunResistance/100)\n"
           + "100 = 気絶完全耐性。ボス等に高い値を設定して耐性を持たせる。")]
    public int StunResistance = 0;

    [Tooltip("敵の麻痺耐性値（0〜100）。\n"
           + "麻痺の実質命中率 = 基礎命中率 × (1 - ParalyzeResistance/100)\n"
           + "100 = 麻痺完全耐性。")]
    public int ParalyzeResistance = 0;

    [Tooltip("敵の暗闇耐性値（0〜100）。\n"
           + "暗闘の実質命中率 = 基礎命中率 × (1 - BlindResistance/100)\n"
           + "100 = 暗闇完全耐性。")]
    public int BlindResistance = 0;

    [Tooltip("敵の怒り耐性値（0〜100）。\n"
           + "怒りの実質命中率 = 基礎命中率 × (1 - RageResistance/100)\n"
           + "100 = 怒り完全耐性。\n"
           + "怒りは対象者自身にかかるバフ的効果だが、\n"
           + "敵に強制付与する使い方もあり得るため耐性を用意。")]
    public int RageResistance = 0;

    [Tooltip("敵の沈黙耐性値（0〜100）。\n"
           + "沈黙の実質命中率 = 基礎命中率 × (1 - SilenceResistance/100)\n"
           + "100 = 沈黙完全耐性。")]
    public int SilenceResistance = 0;

    [Header("Status Effect Resistance")]
    [Tooltip("ONにすると全状態異常に完全耐性（100扱い）になる。\n"
       + "メタル系・ボス等の状態異常完全無効に使用。\n"
       + "個別の耐性値フィールドより優先される。")]
    public bool immuneToAllAilments = false;

    // =========================================================
    // 属性耐性（追加）
    // =========================================================

    [Header("Attribute Resistance")]
    [Tooltip("属性ごとの耐性値。配列に含まれない属性は耐性0（通常ダメージ）。\n"
           + "0=通常, 50=半減, 100=無効, 負値=弱点")]
    public MonsterAttributeResistance[] attributeResistances;

    /// <summary>
    /// 指定された属性に対する耐性値を返す。
    /// attributeResistances に該当属性がなければ 0（耐性なし）を返す。
    /// </summary>
    public int GetAttributeResistance(WeaponAttribute attr)
    {
        if (attributeResistances == null) return 0;
        for (int i = 0; i < attributeResistances.Length; i++)
        {
            if (attributeResistances[i].attribute == attr)
                return attributeResistances[i].value;
        }
        return 0;
    }

    // =========================================================
    // 汎用：状態異常耐性値の取得
    // =========================================================

    /// <summary>
    /// 指定された状態異常に対する耐性値を返す。
    /// 個別フィールドを switch で切り替えて返す。
    /// 未定義の状態異常は 0（耐性なし）を返す。
    /// </summary>
    public int GetStatusEffectResistance(StatusEffect effect)
    {
        // 完全耐性フラグ: ON なら全状態異常に 100 を返す
        if (immuneToAllAilments) return 100;

        switch (effect)
        {
            case StatusEffect.Poison: return PoisonResistance;
            case StatusEffect.Stun: return StunResistance;
            case StatusEffect.Paralyze: return ParalyzeResistance;
            case StatusEffect.Blind: return BlindResistance;
            case StatusEffect.Rage: return RageResistance;
            case StatusEffect.Silence: return SilenceResistance;
            default: return 0;
        }
    }

    [Header("Reward")]
    public int Exp;
    public int Gold;

    // =========================================================
    // ドロップアイテム（追加）
    // =========================================================
    //
    // 戦闘勝利時にアイテムをドロップする設定。
    // dropItem が null の場合はドロップなし。
    // dropRate が 1.0 なら確定ドロップ。
    //
    // 例: レアモンスター → dropItem = ステータスアップ薬, dropRate = 1.0
    // 例: 通常モンスター → dropItem = 薬草, dropRate = 0.3（30%で薬草）
    // =========================================================

    [Header("Drop Item")]
    [Tooltip("戦闘勝利時にドロップするアイテム。null ならドロップなし。")]
    public ItemData dropItem;

    [Tooltip("ドロップ確率（0〜1）。1.0 = 確定ドロップ。")]
    [Range(0f, 1f)]
    public float dropRate = 0f;

    // =========================================================
    // 自動回復（追加）
    // =========================================================
    //
    // ONにすると、毎ターン終了時（毒ダメージ判定の後）に
    // autoRegenAmount 分のHPを自動回復する。
    // 毒で倒れた場合は回復しない（復活しない）。
    // 最大HPを超えて回復しない。
    //
    // 例: ボス系の長期戦を促す場合に使用。
    // 例: autoRegenAmount = 50 → 毎ターン50HP回復
    // =========================================================

    [Header("Auto Regen")]
    [Tooltip("ONにすると毎ターン終了時にHPを自動回復する。")]
    public bool autoRegenEnabled;

    [Tooltip("毎ターンの自動回復量（固定値）。")]
    public int autoRegenAmount;

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