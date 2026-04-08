using System;
using System.Collections.Generic;
using UnityEngine;

public class GameState : MonoBehaviour
{
    public static GameState I { get; private set; }

    [Header("Progress")]
    public int floor = 1;
    public int step = 1;

    [Header("Talk")]
    public string pendingEventId;
    [NonSerialized] public string talkReturnScene = null; // ★追加: Talk終了後の戻り先シーン

    [Header("Item Exchange")]
    [NonSerialized] public ItemData pendingItemData = null;
    [NonSerialized] public bool isRewardItem = false;

    [Header("Equipment")]
    public string equippedWeaponUid = "";

    // =========================================================
    // 塔の到達階（中間ポイント解放用）
    // =========================================================
    [Header("Tower Checkpoint")]
    // これまでに到達した最高階。初期は1階のみ解放。
    public int reachedFloor = 1;

    /// 現在の階が過去最高を超えていたら更新する。
    /// TowerState.Advance() で階が変わった時に呼ぶ。
    public void UpdateReachedFloor(int currentFloor)
    {
        if (currentFloor > reachedFloor)
        {
            reachedFloor = currentFloor;
            Debug.Log($"[GameState] 到達階を更新: {reachedFloor}階");
            SaveManager.Save(); // 即時セーブ
        }
    }

    // =========================================================
    // ステータス
    // =========================================================
    [Header("Level / EXP")]
    public int level = 1;
    public int currentExp = 0;
    public int expToNext = 100;

    [Header("HP / MP")]
    // -1 = 未初期化フラグ。Awake() で RecalcMaxHp/Mp を呼んだ際に
    // 「初回のみ currentHp/currentMp を maxHp/maxMp に揃える」判定に使う。
    // セーブデータがある場合はロード時に正常な値が上書きされるため影響なし。
    public int maxHp = -1;
    public int maxMp = -1;
    public int currentHp = -1;
    public int currentMp = -1;

    [Header("Base Stats Initial (初期値・リセット先)")]
    public int initialSTR = 1;
    public int initialVIT = 1;
    public int initialINT = 1;
    public int initialDEX = 1;
    public int initialLUC = 1;

    [Header("Base Stats (振り分け対象)")]
    public int baseSTR = 1;
    public int baseVIT = 1;
    public int baseINT = 1;
    public int baseDEX = 1;
    public int baseLUC = 1;

    [Header("Status Point")]
    public int statusPoint = 10;

    [Header("GP (がんばりポイント)")]
    [Tooltip("戦闘勝利で+1。拠点でアイテム交換に使用する（交換機能は後日実装）。")]
    public int gp = 0;

    // =========================================================
    // 状態異常
    // =========================================================
    //
    // 【持続型デバフ】（戦闘終了後も残る、塔移動中にも効果あり、セーブ対象）
    //   isPoisoned   - 毒: ターン/歩行ごとにダメージ、10%自然治癒
    //   isParalyzed  - 麻痺: 20%で行動キャンセル、塔移動が遅くなる、10%自然治癒
    //   isBlind      - 暗闇: 命中/回避に影響、塔の背景が暗転、10%自然治癒
    //
    // 【戦闘限定バフ】（セーブ不要、BattleSceneController側で管理）
    //   怒り（Rage）: 攻撃力UP+通常攻撃のみ、3ターン or 戦闘終了で解除
    //   → rageTurnRemaining は BattleSceneController のフィールドで管理
    //
    // 【戦闘限定デバフ】（セーブ不要、BattleSceneController側で管理）
    //   気絶（Stun）: 1ターン行動不能
    //   → enemyIsStunned は BattleSceneController のフィールドで管理
    //
    // ★ブラッシュアップ:
    //   街に戻る = 全回復（状態異常含む）で統一。
    //   FullRecover() / ClearAllStatusEffects() で一括クリアする。
    // =========================================================

    [Header("Status Effects")]
    [Tooltip("プレイヤーが毒状態かどうか。戦闘終了後も持続する。")]
    public bool isPoisoned = false;

    [Tooltip("プレイヤーが麻痺状態かどうか。戦闘終了後も持続する。")]
    public bool isParalyzed = false;

    [Tooltip("プレイヤーが暗闇状態かどうか。戦闘終了後も持続する。")]
    public bool isBlind = false;

    // =========================================================
    // 状態異常の一括クリア
    // =========================================================

    /// <summary>
    /// プレイヤーの全状態異常をクリアする。
    /// 街に戻る（帰還・敗北・ロード復帰）時に FullRecover() から呼ばれる。
    /// 持続型デバフ（毒・麻痺・暗闇）のみ。戦闘限定の状態はここでは扱わない。
    /// </summary>
    public void ClearAllStatusEffects()
    {
        isPoisoned = false;
        isParalyzed = false;
        isBlind = false;
        Debug.Log("[GameState] 全状態異常をクリア");
    }

    // =========================================================
    // レベルアップシステム（追加）
    // =========================================================
    //
    // 必要経験値: レベル × 100（CalcExpToNext で算出）
    // ボーナスステータスポイント: レベル帯ごとに増加（CalcStatusPointGain で算出）
    //   Lv  2～10 → 1pt
    //   Lv 11～20 → 2pt
    //   Lv 21～30 → 3pt
    //   Lv 31～40 → 4pt
    //   Lv 41～50 → 5pt
    //   計算式: (lv - 1) / 10 + 1
    //
    // レベルドレイン:
    //   敵のスキルでレベルを1下げる。
    //   経験値は0にリセット、必要経験値も再計算。
    //   statusPoint は変更しない（減らない）。
    //   → 再度レベルを上げれば同じレベルのボーナスポイントをもう一度貰える。
    //   → 必要経験値が下がる分、ポイント稼ぎの面では得。
    //   レベル1以下にはならない。
    // =========================================================

    /// <summary>
    /// 指定レベルに必要な経験値を返す。
    /// 計算式: lv × 100
    /// 例: Lv1→2 = 100, Lv10→11 = 1000
    /// </summary>
    public static int CalcExpToNext(int lv)
    {
        return lv * 100;
    }

    /// <summary>
    /// 指定レベルに到達した時に獲得するステータスポイントを返す。
    /// 計算式: (lv - 1) / 10 + 1
    ///   Lv  2～10 → 1pt
    ///   Lv 11～20 → 2pt
    ///   Lv 21～30 → 3pt
    ///   Lv 31～40 → 4pt
    ///   Lv 41～50 → 5pt
    /// </summary>
    public static int CalcStatusPointGain(int lv)
    {
        if (lv <= 1) return 0; // Lv1（初期レベル）では獲得なし
        return (lv - 1) / 10 + 1;
    }

    /// <summary>
    /// 経験値を加算し、レベルアップ判定を繰り返す。
    /// 複数レベルアップにも対応（一度に大量のEXPを得た場合）。
    /// 戻り値: レベルアップした回数（0 = レベルアップなし）
    ///
    /// レベルアップ時:
    ///   - statusPoint に CalcStatusPointGain(新レベル) を加算
    ///   - expToNext を CalcExpToNext(新レベル) に更新
    ///   - currentExp から expToNext を差し引いて繰り越し
    /// </summary>
    public int GainExp(int amount)
    {
        if (amount <= 0) return 0;

        currentExp += amount;
        int levelUps = 0;

        while (currentExp >= expToNext)
        {
            currentExp -= expToNext;
            level++;
            levelUps++;

            int pointGain = CalcStatusPointGain(level);
            statusPoint += pointGain;

            Debug.Log($"[GameState] レベルアップ！ Lv{level} (+{pointGain}ステータスポイント, 合計{statusPoint})");

            // 次のレベルに必要な経験値を再計算
            expToNext = CalcExpToNext(level);
        }

        if (levelUps > 0)
        {
            // レベルアップ時は maxHp/maxMp を再計算しない
            // （レベルアップ自体は HP/MP に影響しない。ステ振りで VIT/INT を上げた時に反映される）
            SaveManager.Save();
        }

        return levelUps;
    }

    /// <summary>
    /// レベルドレインを適用する。
    /// - レベルを1下げる（レベル1以下にはならない）
    /// - 現在の経験値を0にリセット
    /// - 必要経験値を再計算
    /// - statusPoint は変更しない（減らない）
    ///
    /// 戻り値: true = レベルが下がった, false = レベル1のため効果なし
    /// </summary>
    public bool ApplyLevelDrain()
    {
        if (level <= 1)
        {
            Debug.Log("[GameState] レベルドレイン: レベル1のため効果なし");
            return false;
        }

        int oldLevel = level;
        level--;
        currentExp = 0;
        expToNext = CalcExpToNext(level);
        // statusPoint は変えない（減らない）
        // maxHp/maxMp も変えない（ステ振りはそのまま）

        Debug.Log($"[GameState] レベルドレイン！ Lv{oldLevel} → Lv{level} (EXP=0, expToNext={expToNext}, statusPoint変更なし)");
        SaveManager.Save();
        return true;
    }

    // =========================================================
    // サブステータス（装備＋パッシブ込みの実効値）
    // =========================================================
    //
    // 計算の構成:
    //   基礎値（baseXxx × 倍率）
    //   + EquipmentCalculator.GetXxx()    ← 装備品補正（100%反映）
    //   + PassiveCalculator.CalcXxxBonus() ← パッシブ補正（重複ルール適用）
    //
    // バトルコントローラーは baseSTR/baseINT を直接参照せず、
    // このプロパティ群を使うことで装備＋パッシブが自動的に反映される。
    // =========================================================

    /// <summary>
    /// 攻撃力。
    /// 計算式: baseSTR × 1 + EquipmentCalculator.GetAttackPower()
    ///                      + PassiveCalculator.CalcAttackBonus()
    ///
    /// 装備品の attackPower は 100% そのまま加算される。
    /// パッシブの AttackBonus は重複ルール（2個目以降10%減衰）を適用。
    /// </summary>
    public int Attack
    {
        get
        {
            int total = baseSTR * 1
                      + EquipmentCalculator.GetAttackPower()
                      + PassiveCalculator.CalcAttackBonus();
            if (total < 0) total = 0;
            return total;
        }
    }

    /// <summary>
    /// 魔法攻撃力。
    /// 計算式: baseINT × 1 + EquipmentCalculator.GetMagicAttack()
    ///                      + PassiveCalculator.CalcMagicAttackBonus()
    /// </summary>
    public int MagicAttack
    {
        get
        {
            int total = baseINT * 1
                      + EquipmentCalculator.GetMagicAttack()
                      + PassiveCalculator.CalcMagicAttackBonus();
            if (total < 0) total = 0;
            return total;
        }
    }

    /// <summary>
    /// 運の良さ。
    /// 計算式: baseLUC + EquipmentCalculator.GetLuck()
    ///                 + PassiveCalculator.CalcLuckBonus()
    ///
    /// 注意: 敵行動のLUC判定（SelectEnemyAction）では baseLUC を直接参照する。
    /// これは「LUC判定は振り分けた生値で比較する」という設計意図のため。
    /// UI表示やその他の用途ではこのプロパティを使う。
    /// </summary>
    public int Luck
    {
        get
        {
            int total = baseLUC
                      + EquipmentCalculator.GetLuck()
                      + PassiveCalculator.CalcLuckBonus();
            if (total < 0) total = 0;
            return total;
        }
    }

    // =========================================================
    // 命中力・回避率・クリティカル率（DEX/LUC ベース + 装備 + パッシブ）（追加）
    // =========================================================
    //
    // 命中力（Accuracy）: int
    //   計算式: DEX×10 + LUC×1 + 装備 + パッシブ
    //   BattleSceneController でプレイヤー攻撃の命中判定に使用。
    //
    // 回避率（Evasion）: float（小数点2位精度）
    //   計算式: 5 + DEX×0.05 + LUC×0.02 + 装備 + パッシブ
    //   回避とクリティカルは装備やアイテムで主に増やすため、
    //   パラメータによる増加はおまけ程度に設定されている。
    //   BattleSceneController で敵攻撃の命中判定に使用。
    //
    // クリティカル率（CriticalRate）: float（小数点2位精度）
    //   計算式: 5 + DEX×0.05 + LUC×0.02 + 装備 + パッシブ
    //   命中時に判定し、クリティカルなら防御無視・ダメージ2倍。
    //   敵はクリティカルを行わない。
    // =========================================================

    /// <summary>
    /// 命中力（int）。
    /// 計算式: baseDEX × 10 + baseLUC × 1
    ///         + EquipmentCalculator.GetAccuracy()
    ///         + PassiveCalculator.CalcAccuracyBonus()
    ///
    /// BattleSceneController でのプレイヤー攻撃命中判定:
    ///   最終命中率 = 基礎命中率 × (1 - (敵回避力 - Accuracy) / 100)
    ///   ただし最低25%保証。
    /// </summary>
    public int Accuracy
    {
        get
        {
            int total = baseDEX * 10
                      + baseLUC * 1
                      + EquipmentCalculator.GetAccuracy()
                      + PassiveCalculator.CalcAccuracyBonus();
            if (total < 0) total = 0;
            return total;
        }
    }

    /// <summary>
    /// 回避率（float、小数点2位精度）。単位は %。
    /// 計算式: 5.00 + baseDEX × 0.05 + baseLUC × 0.02
    ///         + EquipmentCalculator.GetEvasion()（int → floatに変換）
    ///         + PassiveCalculator.CalcEvasionBonus()（float、小数点2位精度）
    ///
    /// BattleSceneController での敵攻撃命中判定:
    ///   最終命中率 = 敵基礎命中率 × (1 - Evasion / 100)
    ///   ただし最低10%保証。
    ///
    /// 回避率は装備やアイテムで主に増やすため、
    /// DEX/LUC による増加はおまけ程度（DEX×0.05 + LUC×0.02）。
    /// </summary>
    public float Evasion
    {
        get
        {
            float total = 5.00f
                        + baseDEX * 0.05f
                        + baseLUC * 0.02f
                        + (float)EquipmentCalculator.GetEvasion()
                        + PassiveCalculator.CalcEvasionBonus();
            if (total < 0f) total = 0f;
            // 小数点2位に丸める
            total = Mathf.Floor(total * 100f + 0.5f) / 100f;
            return total;
        }
    }

    /// <summary>
    /// クリティカル率（float、小数点2位精度）。単位は %。
    /// 計算式: 5.00 + baseDEX × 0.05 + baseLUC × 0.02
    ///         + EquipmentCalculator.GetCritical()（int → floatに変換）
    ///         + PassiveCalculator.CalcCriticalBonus()（float、小数点2位精度）
    ///
    /// BattleSceneController でのプレイヤー攻撃クリティカル判定:
    ///   命中後に CriticalRate% の確率でクリティカル。
    ///   クリティカル時: 防御無視、ダメージ2倍。
    ///   敵はクリティカルを行わない。
    ///
    /// クリティカル率は装備やアイテムで主に増やすため、
    /// DEX/LUC による増加はおまけ程度（DEX×0.05 + LUC×0.02）。
    /// </summary>
    public float CriticalRate
    {
        get
        {
            float total = 5.00f
                        + baseDEX * 0.05f
                        + baseLUC * 0.02f
                        + (float)EquipmentCalculator.GetCritical()
                        + PassiveCalculator.CalcCriticalBonus();
            if (total < 0f) total = 0f;
            // 小数点2位に丸める
            total = Mathf.Floor(total * 100f + 0.5f) / 100f;
            return total;
        }
    }

    // =========================================================
    // HP 計算（VIT ベース + 装備 + パッシブ）
    // =========================================================

    /// <summary>
    /// HP の基礎初期値（VIT 未振り・パッシブ無しでのHP）。
    /// </summary>
    private const int BaseHpInitial = 50;

    /// <summary>
    /// VIT 1ポイントあたりの最大HP上昇量。
    /// </summary>
    private const int HpPerVit = 5;

    /// <summary>
    /// 現在のステータスとパッシブ効果から最大HPを再計算し、maxHp を更新する。
    /// currentHp が新しい maxHp を超えている場合はクランプする（超過分は消える）。
    /// currentHp が新しい maxHp 以下の場合はそのまま（回復しない）。
    /// currentHp が -1（未初期化）の場合のみ maxHp に揃える（ゲーム初回起動時）。
    ///
    /// 呼び出しタイミング:
    ///   - Awake()（初回起動 or セーブロード後）
    ///   - ステータス振り分け後
    ///   - アイテム取得/破棄後
    ///   - 装備変更後（装備品の equipMaxHp が変わるため）
    /// </summary>
    public void RecalcMaxHp()
    {
        int newMaxHp = CalcMaxHp();

        if (newMaxHp != maxHp)
        {
            Debug.Log($"[GameState] maxHp 再計算: {maxHp} → {newMaxHp}");
            maxHp = newMaxHp;
        }

        if (currentHp < 0)
        {
            // 未初期化（初回起動時）: maxHp に揃える
            currentHp = maxHp;
            Debug.Log($"[GameState] currentHp 初期化: {currentHp}");
        }
        else if (currentHp > maxHp)
        {
            // maxHp を下回ったことで超過: クランプ
            currentHp = maxHp;
        }
    }

    /// <summary>
    /// 最大HPを計算する（値を返すだけで、maxHp フィールドは変更しない）。
    /// 計算式: BaseHpInitial + baseVIT × HpPerVit
    ///         + EquipmentCalculator.GetMaxHpBonus()
    ///         + PassiveCalculator.CalcMaxHpBonus()
    /// </summary>
    public int CalcMaxHp()
    {
        int vitBonus = baseVIT * HpPerVit;
        int equipBonus = EquipmentCalculator.GetMaxHpBonus();
        int passiveBonus = PassiveCalculator.CalcMaxHpBonus();
        int result = BaseHpInitial + vitBonus + equipBonus + passiveBonus;
        if (result < 1) result = 1;
        return result;
    }

    // =========================================================
    // 防御力（VIT ベース + 装備 + パッシブ）
    // =========================================================

    /// <summary>
    /// VIT 1ポイントあたりの防御力。
    /// </summary>
    private const int DefPerVit = 2;

    /// <summary>
    /// プレイヤーの現在の防御力を返す。
    /// 計算式: baseVIT × DefPerVit + EquipmentCalculator.GetDefense()
    ///                              + PassiveCalculator.CalcDefenseBonus()
    /// </summary>
    public int Defense
    {
        get
        {
            int baseDef = baseVIT * DefPerVit;
            int equipBonus = EquipmentCalculator.GetDefense();
            int passiveBonus = PassiveCalculator.CalcDefenseBonus();
            int total = baseDef + equipBonus + passiveBonus;
            if (total < 0) total = 0;
            return total;
        }
    }

    // =========================================================
    // MP 計算（INT ベース + 装備 + パッシブ）
    // =========================================================

    /// <summary>
    /// MP の基礎初期値（INT 未振り・パッシブ無しでの MP）。
    /// </summary>
    private const int BaseMpInitial = 20;

    /// <summary>
    /// INT 1ポイントあたりの最大MP上昇量。
    /// </summary>
    private const int MpPerInt = 3;

    /// <summary>
    /// 現在のステータスとパッシブ効果から最大MPを再計算し、maxMp を更新する。
    /// currentMp が新しい maxMp を超えている場合はクランプする。
    /// currentMp が新しい maxMp 以下の場合はそのまま（回復しない）。
    /// currentMp が -1（未初期化）の場合のみ maxMp に揃える（ゲーム初回起動時）。
    /// </summary>
    public void RecalcMaxMp()
    {
        int newMaxMp = CalcMaxMp();

        if (newMaxMp != maxMp)
        {
            Debug.Log($"[GameState] maxMp 再計算: {maxMp} → {newMaxMp}");
            maxMp = newMaxMp;
        }

        if (currentMp < 0)
        {
            // 未初期化（初回起動時）: maxMp に揃える
            currentMp = maxMp;
            Debug.Log($"[GameState] currentMp 初期化: {currentMp}");
        }
        else if (currentMp > maxMp)
        {
            // maxMp を下回ったことで超過: クランプ
            currentMp = maxMp;
        }
    }

    /// <summary>
    /// 最大MPを計算する（値を返すだけで、maxMp フィールドは変更しない）。
    /// 計算式: BaseMpInitial + baseINT × MpPerInt
    ///         + EquipmentCalculator.GetMaxMpBonus()
    ///         + PassiveCalculator.CalcMaxMpBonus()
    /// </summary>
    public int CalcMaxMp()
    {
        int intBonus = baseINT * MpPerInt;
        int equipBonus = EquipmentCalculator.GetMaxMpBonus();
        int passiveBonus = PassiveCalculator.CalcMaxMpBonus();
        int result = BaseMpInitial + intBonus + equipBonus + passiveBonus;
        if (result < 1) result = 1;
        return result;
    }

    // =========================================================
    // 魔法防御力（INT ベース + 装備 + パッシブ）
    // =========================================================

    /// <summary>
    /// INT 1ポイントあたりの魔法防御力。
    /// </summary>
    private const int MagicDefPerInt = 2;

    /// <summary>
    /// プレイヤーの魔法防御力。
    /// 計算式: baseINT × MagicDefPerInt + EquipmentCalculator.GetMagicDefense()
    ///                                   + PassiveCalculator.CalcMagicDefenseBonus()
    /// </summary>
    public int MagicDefense
    {
        get
        {
            int baseMDef = baseINT * MagicDefPerInt;
            int equipBonus = EquipmentCalculator.GetMagicDefense();
            int passiveBonus = PassiveCalculator.CalcMagicDefenseBonus();
            int total = baseMDef + equipBonus + passiveBonus;
            if (total < 0) total = 0;
            return total;
        }
    }

    // =========================================================
    // ポイント振り分け / リセット
    // =========================================================
    public void TakeStatSnapshot()
    {
    }

    public void ResetStatAllocation()
    {
        int usedPoints = (baseSTR - initialSTR)
                       + (baseVIT - initialVIT)
                       + (baseINT - initialINT)
                       + (baseDEX - initialDEX)
                       + (baseLUC - initialLUC);

        baseSTR = initialSTR;
        baseVIT = initialVIT;
        baseINT = initialINT;
        baseDEX = initialDEX;
        baseLUC = initialLUC;

        statusPoint += usedPoints;

        // VIT/INT が変わるため maxHp/maxMp を再計算
        RecalcMaxHp();
        RecalcMaxMp();

        SaveManager.Save(); // 即時セーブ
    }

    public bool AllocatePoint(StatType stat)
    {
        if (statusPoint <= 0) return false;

        switch (stat)
        {
            case StatType.STR: baseSTR++; break;
            case StatType.VIT: baseVIT++; break;
            case StatType.INT: baseINT++; break;
            case StatType.DEX: baseDEX++; break;
            case StatType.LUC: baseLUC++; break;
            default: return false;
        }

        statusPoint--;

        // VIT に振った場合は maxHp を再計算
        if (stat == StatType.VIT)
        {
            RecalcMaxHp();
        }

        // INT に振った場合は maxMp を再計算
        if (stat == StatType.INT)
        {
            RecalcMaxMp();
        }

        SaveManager.Save(); // 即時セーブ
        return true;
    }

    // =========================================================
    // シーン遷移の戻り先
    // =========================================================
    [Header("Scene Navigation")]
    [NonSerialized] public string previousSceneName = "";

    // =========================================================
    // バトル中アイテム使用
    // =========================================================
    /// <summary>バトル中にItemboxを開いているかどうか。</summary>
    [NonSerialized] public bool isInBattle = false;

    /// <summary>Itembox でアイテム使用/装備変更を行い、ターンを消費すべきかどうか。</summary>
    [NonSerialized] public bool battleTurnConsumed = false;

    /// <summary>Itembox で操作対象となったアイテム名（ログ表示用）。</summary>
    [NonSerialized] public string battleItemActionLog = "";

    // =========================================================
    // バトル中攻撃アイテム: ダメージ情報の一時保存（追加）
    // =========================================================
    //
    // Itembox で攻撃アイテムを使用した際、ダメージ情報を一時保存する。
    // BattleSceneController がシーン復帰時にこれを読み取ってダメージ計算を実行する。
    // ダメージ計算後にリセットされる（pendingBattleItemDamage = 0）。
    //
    // WeaponAttribute / DamageCategory は enum なので、
    // NonSerialized + int でシーン遷移を跨ぐ。
    // =========================================================

    /// <summary>攻撃アイテムの固定ダメージ。0 = 攻撃アイテム未使用。</summary>
    [NonSerialized] public int pendingBattleItemDamage = 0;

    /// <summary>攻撃アイテムの属性（WeaponAttribute の int 値）。</summary>
    [NonSerialized] public int pendingBattleItemAttribute = 0;

    /// <summary>攻撃アイテムの物理/魔法区分（DamageCategory の int 値）。</summary>
    [NonSerialized] public int pendingBattleItemDamageCategory = 0;

    /// <summary>攻撃アイテムの名前（ログ表示用）。</summary>
    [NonSerialized] public string pendingBattleItemName = "";

    // =========================================================
    // イベント既読管理
    // =========================================================
    private HashSet<string> played = new HashSet<string>();

    public bool IsPlayed(string eventId)
        => !string.IsNullOrEmpty(eventId) && played.Contains(eventId);

    public void MarkPlayed(string eventId)
    {
        if (!string.IsNullOrEmpty(eventId))
        {
            played.Add(eventId);
            SaveManager.Save(); // 即時セーブ
        }
    }

    /// <summary>
    /// セーブ用: 既読イベントID一覧を List で返す。
    /// </summary>
    public List<string> GetAllPlayedIds()
    {
        return new List<string>(played);
    }

    /// <summary>
    /// ロード用: 既読イベントID一覧を復元する。
    /// </summary>
    public void RestorePlayedIds(List<string> ids)
    {
        played.Clear();
        if (ids != null)
        {
            foreach (var id in ids)
            {
                if (!string.IsNullOrEmpty(id))
                    played.Add(id);
            }
        }
    }

    private void Awake()
    {
        if (I != null) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);

        // 初回起動時に VIT/INT とパッシブを反映した maxHp/maxMp を計算する。
        // currentHp/currentMp が -1（未初期化）の場合のみ maxHp/maxMp に揃える。
        // セーブロード後はロード処理が currentHp/currentMp を正常値に上書きするため、
        // -1 のまま Awake が走ることはない。
        RecalcMaxHp();
        RecalcMaxMp();
    }
}

public enum StatType
{
    STR,
    VIT,
    INT,
    DEX,
    LUC
}