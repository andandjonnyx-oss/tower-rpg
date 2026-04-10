using UnityEngine;

/// <summary>
/// 状態異常ランプ表示コンポーネント。
/// 各状態異常に対応する子オブジェクト（doku/mahi/kurayami/ikari/bougyoDown/bougyoUp）の
/// SetActive(true/false) を切り替えて状態異常を表示する。
///
/// Inspectorで各ランプオブジェクトを割り当てる。
/// 味方用・敵用のどちらにも使える共通コンポーネント。
///
/// 使い方:
///   lamp.SetPoison(true);   // 毒ランプ点灯
///   lamp.SetAll(false, true, false, false, false, false);  // まとめて設定
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
    // バフ/デバフランプ（追加）
    // =========================================================

    [Tooltip("防御ダウンランプ")]
    [SerializeField] private GameObject bougyoDown;

    [Tooltip("防御アップランプ")]
    [SerializeField] private GameObject bougyoUp;

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

    /// <summary>全ランプをまとめて設定する（旧互換: 4引数）。</summary>
    public void SetAll(bool poison, bool paralyze, bool blind, bool rage)
    {
        SetPoison(poison);
        SetParalyze(paralyze);
        SetBlind(blind);
        SetRage(rage);
    }

    /// <summary>全ランプをまとめて設定する（拡張版: 6引数）。</summary>
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

    /// <summary>全ランプを消灯する。</summary>
    public void ClearAll()
    {
        SetAll(false, false, false, false, false, false);
    }
}