using UnityEngine;

/// <summary>
/// 状態異常ランプ表示コンポーネント。
/// 各状態異常に対応する子オブジェクト（doku/mahi/kurayami/ikari/chinmoku/sekika/miryou/noroi/garasu + バフ/デバフ10個）の
/// SetActive(true/false) を切り替えて状態異常を表示する。
///
/// Inspectorで各ランプオブジェクトを割り当てる。
/// 味方用・敵用のどちらにも使える共通コンポーネント。
///
/// 使い方:
///   lamp.SetPoison(true);   // 毒ランプ点灯
///   lamp.SetAll(poison, paralyze, blind, rage, silence, petrify,
///               charm, curse, glass,
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

    [Tooltip("沈黙ランプ")]
    [SerializeField] private GameObject chinmoku;

    [Tooltip("石化ランプ（Phase C 追加）")]
    [SerializeField] private GameObject sekika;

    [Tooltip("魅了ランプ（ピンク系 #FF69B4）")]
    [SerializeField] private GameObject miryou;

    [Tooltip("呪いランプ（紫系 #8B008B）")]
    [SerializeField] private GameObject noroi;

    [Tooltip("ガラスランプ（水色系 #87CEEB）")]
    [SerializeField] private GameObject garasu;

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

    /// <summary>沈黙ランプの点灯/消灯を設定する。</summary>
    public void SetSilence(bool active)
    {
        if (chinmoku != null) chinmoku.SetActive(active);
    }

    /// <summary>石化ランプの点灯/消灯を設定する。（Phase C 追加）</summary>
    public void SetPetrify(bool active)
    {
        if (sekika != null) sekika.SetActive(active);
    }

    /// <summary>魅了ランプの点灯/消灯を設定する。</summary>
    public void SetCharm(bool active)
    {
        if (miryou != null) miryou.SetActive(active);
    }

    /// <summary>呪いランプの点灯/消灯を設定する。</summary>
    public void SetCurse(bool active)
    {
        if (noroi != null) noroi.SetActive(active);
    }

    /// <summary>ガラスランプの点灯/消灯を設定する。</summary>
    public void SetGlass(bool active)
    {
        if (garasu != null) garasu.SetActive(active);
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
        SetSilence(false);
        SetPetrify(false);
        SetCharm(false);
        SetCurse(false);
        SetGlass(false);
    }

    /// <summary>全ランプをまとめて設定する（Tower用: 5引数 — 沈黙対応）。</summary>
    public void SetAll(bool poison, bool paralyze, bool blind, bool rage, bool silence)
    {
        SetPoison(poison);
        SetParalyze(paralyze);
        SetBlind(blind);
        SetRage(rage);
        SetSilence(silence);
        SetPetrify(false);
        SetCharm(false);
        SetCurse(false);
        SetGlass(false);
    }

    /// <summary>全ランプをまとめて設定する（Tower用: 6引数 — 沈黙+石化対応、Phase C 追加）。</summary>
    public void SetAll(bool poison, bool paralyze, bool blind, bool rage, bool silence, bool petrify)
    {
        SetPoison(poison);
        SetParalyze(paralyze);
        SetBlind(blind);
        SetRage(rage);
        SetSilence(silence);
        SetPetrify(petrify);
        SetCharm(false);
        SetCurse(false);
        SetGlass(false);
    }

    // Phase3 の 6引数版（poison, paralyze, blind, rage, defDown, defUp）は
    // Phase C で追加した 6引数版（poison, paralyze, blind, rage, silence, petrify）と
    // シグネチャが衝突するため削除。旧呼び出し元は 14引数版 or 15引数版に移行済み。

    /// <summary>全ランプをまとめて設定する（Phase4フル版: 14引数 → 後方互換維持、沈黙なし）。</summary>
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
        SetSilence(false);
        SetPetrify(false);
        SetCharm(false);
        SetCurse(false);
        SetGlass(false);
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

    /// <summary>全ランプをまとめて設定する（Phase5: 15引数 — 沈黙対応、石化は false 固定）。後方互換用。</summary>
    public void SetAll(bool poison, bool paralyze, bool blind, bool rage, bool silence,
                       bool defDown, bool defUp,
                       bool atkDown, bool atkUp,
                       bool matkDown, bool matkUp,
                       bool mdefDown, bool mdefUp,
                       bool lucDown, bool lucUp)
    {
        SetAll(poison, paralyze, blind, rage, silence, false,
               false, false, false,
               defDown, defUp, atkDown, atkUp,
               matkDown, matkUp, mdefDown, mdefUp,
               lucDown, lucUp);
    }

    /// <summary>全ランプをまとめて設定する（Phase C: 16引数 — 沈黙+石化+バフ/デバフ全対応）。</summary>
    public void SetAll(bool poison, bool paralyze, bool blind, bool rage, bool silence, bool petrify,
                       bool defDown, bool defUp,
                       bool atkDown, bool atkUp,
                       bool matkDown, bool matkUp,
                       bool mdefDown, bool mdefUp,
                       bool lucDown, bool lucUp)
    {
        SetAll(poison, paralyze, blind, rage, silence, petrify,
               false, false, false,
               defDown, defUp, atkDown, atkUp,
               matkDown, matkUp, mdefDown, mdefUp,
               lucDown, lucUp);
    }

    /// <summary>全ランプをまとめて設定する（最新フル版: 19引数 — 魅了+呪い+ガラス+バフ/デバフ全対応）。</summary>
    public void SetAll(bool poison, bool paralyze, bool blind, bool rage, bool silence, bool petrify,
                       bool charm, bool curse, bool glass,
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
        SetSilence(silence);
        SetPetrify(petrify);
        SetCharm(charm);
        SetCurse(curse);
        SetGlass(glass);
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
        SetPoison(false);
        SetParalyze(false);
        SetBlind(false);
        SetRage(false);
        SetSilence(false);
        SetPetrify(false);
        SetCharm(false);
        SetCurse(false);
        SetGlass(false);
        SetDefenseDown(false);
        SetDefenseUp(false);
        SetAttackDown(false);
        SetAttackUp(false);
        SetMagicAttackDown(false);
        SetMagicAttackUp(false);
        SetMagicDefenseDown(false);
        SetMagicDefenseUp(false);
        SetLuckDown(false);
        SetLuckUp(false);
    }
}