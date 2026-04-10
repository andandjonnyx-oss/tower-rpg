using UnityEngine;

/// <summary>
/// 状態異常ランプ表示コンポーネント。
/// 各状態異常に対応する子オブジェクト（doku/mahi/kurayami/ikari + バフ/デバフ10個）の
/// SetActive(true/false) を切り替えて状態異常を表示する。
///
/// Inspectorで各ランプオブジェクトを割り当てる。
/// 味方用・敵用のどちらにも使える共通コンポーネント。
///
/// 使い方:
///   lamp.SetPoison(true);   // 毒ランプ点灯
///   lamp.SetAll(poison, paralyze, blind, rage,
///               defDown, defUp, atkDown, atkUp,
///               matkDown, matkUp, mdefDown, mdefUp,
///               lucDown, lucUp);
///   lamp.ClearAll();        // 全消灯
/// </summary>
public class StatusEffectLamp : MonoBehaviour
{
    [Header("状態異常ランプ（各オブジェクトを割り当て）")]
    [Tooltip("毒ランプ")]
    [SerializeField] private GameObject doku;

    [Tooltip("麻痺ランプ")]
    [SerializeField] private GameObject mahi;

    [Tooltip("暗闇ランプ")]
    [SerializeField] private GameObject kurayami;

    [Tooltip("怒りランプ")]
    [SerializeField] private GameObject ikari;

    // =========================================================
    // バフ/デバフランプ（5ペア = 10個）
    // =========================================================

    [Header("バフ/デバフランプ")]
    [Tooltip("防御ダウンランプ")]
    [SerializeField] private GameObject bougyoDown;

    [Tooltip("防御アップランプ")]
    [SerializeField] private GameObject bougyoUp;

    [Tooltip("攻撃ダウンランプ")]
    [SerializeField] private GameObject kougekiDown;

    [Tooltip("攻撃アップランプ")]
    [SerializeField] private GameObject kougekiUp;

    [Tooltip("魔攻/回避ダウンランプ")]
    [SerializeField] private GameObject makouDown;

    [Tooltip("魔攻/回避アップランプ")]
    [SerializeField] private GameObject makouUp;

    [Tooltip("魔防ダウンランプ")]
    [SerializeField] private GameObject mabouDown;

    [Tooltip("魔防アップランプ")]
    [SerializeField] private GameObject mabouUp;

    [Tooltip("運ダウンランプ")]
    [SerializeField] private GameObject unDown;

    [Tooltip("運アップランプ")]
    [SerializeField] private GameObject unUp;

    // =========================================================
    // 個別セッター
    // =========================================================

    /// <summary>毒ランプの点灯/消灯を設定する。</summary>
    public void SetPoison(bool active)
    {
        if (doku != null) doku.SetActive(active);
    }

    /// <summary>麻痺ランプの点灯/消灯を設定する。</summary>
    public void SetParalyze(bool active)
    {
        if (mahi != null) mahi.SetActive(active);
    }

    /// <summary>暗闇ランプの点灯/消灯を設定する。</summary>
    public void SetBlind(bool active)
    {
        if (kurayami != null) kurayami.SetActive(active);
    }

    /// <summary>怒りランプの点灯/消灯を設定する。</summary>
    public void SetRage(bool active)
    {
        if (ikari != null) ikari.SetActive(active);
    }

    /// <summary>防御ダウンランプの点灯/消灯を設定する。</summary>
    public void SetDefenseDown(bool active)
    {
        if (bougyoDown != null) bougyoDown.SetActive(active);
    }

    /// <summary>防御アップランプの点灯/消灯を設定する。</summary>
    public void SetDefenseUp(bool active)
    {
        if (bougyoUp != null) bougyoUp.SetActive(active);
    }

    public void SetAttackDown(bool active)
    {
        if (kougekiDown != null) kougekiDown.SetActive(active);
    }

    public void SetAttackUp(bool active)
    {
        if (kougekiUp != null) kougekiUp.SetActive(active);
    }

    public void SetMagicAttackDown(bool active)
    {
        if (makouDown != null) makouDown.SetActive(active);
    }

    public void SetMagicAttackUp(bool active)
    {
        if (makouUp != null) makouUp.SetActive(active);
    }

    public void SetMagicDefenseDown(bool active)
    {
        if (mabouDown != null) mabouDown.SetActive(active);
    }

    public void SetMagicDefenseUp(bool active)
    {
        if (mabouUp != null) mabouUp.SetActive(active);
    }

    public void SetLuckDown(bool active)
    {
        if (unDown != null) unDown.SetActive(active);
    }

    public void SetLuckUp(bool active)
    {
        if (unUp != null) unUp.SetActive(active);
    }

    // =========================================================
    // 一括セッター
    // =========================================================

    /// <summary>全ランプをまとめて設定する（旧互換: 4引数）。</summary>
    public void SetAll(bool poison, bool paralyze, bool blind, bool rage)
    {
        SetPoison(poison);
        SetParalyze(paralyze);
        SetBlind(blind);
        SetRage(rage);
    }

    /// <summary>全ランプをまとめて設定する（Phase3互換: 6引数）。</summary>
    public void SetAll(bool poison, bool paralyze, bool blind, bool rage,
                       bool defDown, bool defUp)
    {
        SetPoison(poison);
        SetParalyze(paralyze);
        SetBlind(blind);
        SetRage(rage);
        SetDefenseDown(defDown);
        SetDefenseUp(defUp);
    }

    /// <summary>全ランプをまとめて設定する（Phase4フル版: 14引数）。</summary>
    public void SetAll(bool poison, bool paralyze, bool blind, bool rage,
                       bool defDown, bool defUp,
                       bool atkDown, bool atkUp,
                       bool matkDown, bool matkUp,
                       bool mdefDown, bool mdefUp,
                       bool lucDown, bool lucUp)
    {
        SetPoison(poison);
        SetParalyze(paralyze);
        SetBlind(blind);
        SetRage(rage);
        SetDefenseDown(defDown);
        SetDefenseUp(defUp);
        SetAttackDown(atkDown);
        SetAttackUp(atkUp);
        SetMagicAttackDown(matkDown);
        SetMagicAttackUp(matkUp);
        SetMagicDefenseDown(mdefDown);
        SetMagicDefenseUp(mdefUp);
        SetLuckDown(lucDown);
        SetLuckUp(lucUp);
    }

    /// <summary>全ランプを消灯する。</summary>
    public void ClearAll()
    {
        SetAll(false, false, false, false,
               false, false,
               false, false,
               false, false,
               false, false,
               false, false);
    }
}