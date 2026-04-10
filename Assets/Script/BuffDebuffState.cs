/// <summary>
/// バフ/デバフの状態を管理する構造体/クラス群。
/// SkillEffectProcessor と BattleSceneController 間の引数爆発を解消するために導入。
///
/// 【階層構造】
///   BuffDebuffPair     — 1ペア分（debuffTurn/Rate + buffTurn/Rate）[struct]
///   BuffDebuffSet      — 5種分の BuffDebuffPair（DEF/ATK/MATK/MDEF/LUC）[class]
///   BattleBuffState    — player + enemy の BuffDebuffSet [class]
///
/// 【注意】
///   BuffDebuffSet / BattleBuffState は class にしている。
///   これは GetPairRef() で ref return するために必要。
///   struct では自身のメンバーを ref return できない（CS8170）。
/// </summary>

/// <summary>
/// バフ/デバフ1ペア分の状態。
/// 例: 防御ダウン残り3ターン(30%) + 防御アップなし(0ターン)。
/// </summary>
[System.Serializable]
public struct BuffDebuffPair
{
    /// <summary>デバフ残りターン数。0 = なし。</summary>
    public int debuffTurn;
    /// <summary>デバフ効果率（%）。例: 30 = 30%減少。</summary>
    public float debuffRate;
    /// <summary>バフ残りターン数。0 = なし。</summary>
    public int buffTurn;
    /// <summary>バフ効果率（%）。例: 30 = 30%増加。</summary>
    public float buffRate;

    /// <summary>デバフが有効か。</summary>
    public bool IsDebuffed => debuffTurn > 0;
    /// <summary>バフが有効か。</summary>
    public bool IsBuffed => buffTurn > 0;

    /// <summary>全フィールドをリセットする。</summary>
    public void Reset()
    {
        debuffTurn = 0; debuffRate = 0f;
        buffTurn = 0; buffRate = 0f;
    }
}

/// <summary>
/// 5種分のバフ/デバフ状態をまとめるクラス。
/// class にすることで GetPairRef() の ref return が可能。
/// </summary>
[System.Serializable]
public class BuffDebuffSet
{
    public BuffDebuffPair def;   // 防御
    public BuffDebuffPair atk;   // 攻撃
    public BuffDebuffPair matk;  // 魔攻/回避
    public BuffDebuffPair mdef;  // 魔防
    public BuffDebuffPair luc;   // 運

    /// <summary>全種リセット。</summary>
    public void Reset()
    {
        def.Reset();
        atk.Reset();
        matk.Reset();
        mdef.Reset();
        luc.Reset();
    }

    /// <summary>
    /// StatusEffect から対応する BuffDebuffPair への参照を返す。
    /// class のメンバーなので ref return が可能。
    /// </summary>
    public ref BuffDebuffPair GetPairRef(StatusEffect effect)
    {
        switch (effect)
        {
            case StatusEffect.DefenseDown:
            case StatusEffect.DefenseUp:
                return ref def;

            case StatusEffect.AttackDown:
            case StatusEffect.AttackUp:
                return ref atk;

            case StatusEffect.MagicAttackDown:
            case StatusEffect.MagicAttackUp:
                return ref matk;

            case StatusEffect.MagicDefenseDown:
            case StatusEffect.MagicDefenseUp:
                return ref mdef;

            case StatusEffect.LuckDown:
            case StatusEffect.LuckUp:
                return ref luc;

            default:
                // フォールバック（到達しないはず）
                return ref def;
        }
    }
}

/// <summary>
/// 戦闘全体のバフ/デバフ状態。player + enemy の2面。
/// BattleSceneController_BuffDebuff.cs の static フィールドとして使用する。
/// class にすることで BuffDebuffSet（class）を正しく保持できる。
/// </summary>
[System.Serializable]
public class BattleBuffState
{
    public BuffDebuffSet player = new BuffDebuffSet();
    public BuffDebuffSet enemy = new BuffDebuffSet();

    /// <summary>全リセット。</summary>
    public void Reset()
    {
        player.Reset();
        enemy.Reset();
    }
}