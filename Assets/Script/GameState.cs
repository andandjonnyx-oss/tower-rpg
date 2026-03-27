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

    [Header("Item Exchange")]
    [NonSerialized] public ItemData pendingItemData = null;

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
    public int maxHp = 50;
    public int maxMp = 20;
    public int currentHp = 50;
    public int currentMp = 20;

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

    public int Power => baseSTR * 1;
    public int Stamina => baseVIT * 2;
    public int Dexterity => baseDEX * 3;
    public int MagicPower => baseINT * 4;
    public int Luck => baseLUC * 5;

    // =========================================================
    // HP 計算（VIT ベース + パッシブ）
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
    ///
    /// 呼び出しタイミング:
    ///   - ステータス振り分け後
    ///   - アイテム取得/破棄後
    ///   - シーン遷移時（Towerシーン開始時など）
    ///   - セーブデータロード後
    /// </summary>
    public void RecalcMaxHp()
    {
        int newMaxHp = CalcMaxHp();

        if (newMaxHp != maxHp)
        {
            Debug.Log($"[GameState] maxHp 再計算: {maxHp} → {newMaxHp}");
            maxHp = newMaxHp;

            // currentHp が新しい maxHp を超えていたらクランプ
            if (currentHp > maxHp)
            {
                currentHp = maxHp;
            }
        }
    }

    /// <summary>
    /// 最大HPを計算する（値を返すだけで、maxHp フィールドは変更しない）。
    /// 計算式: BaseHpInitial + baseVIT × HpPerVit + PassiveCalculator.CalcMaxHpBonus()
    /// </summary>
    public int CalcMaxHp()
    {
        int vitBonus = baseVIT * HpPerVit;
        int passiveBonus = PassiveCalculator.CalcMaxHpBonus();
        int result = BaseHpInitial + vitBonus + passiveBonus;
        if (result < 1) result = 1;
        return result;
    }

    // =========================================================
    // 防御力（VIT ベース + パッシブ）
    // =========================================================

    /// <summary>
    /// VIT 1ポイントあたりの防御力。
    /// </summary>
    private const int DefPerVit = 2;

    /// <summary>
    /// プレイヤーの現在の防御力を返す。
    /// 計算式: baseVIT × DefPerVit + PassiveCalculator.CalcDefenseBonus()
    /// 今後装備やスキルで変動する場合もこのプロパティを参照する。
    /// 四捨五入が必要な計算は現時点では発生しないが、
    /// 将来の乗算効果に備えて int のまま返す。
    /// </summary>
    public int Defense
    {
        get
        {
            int baseDef = baseVIT * DefPerVit;
            int passiveBonus = PassiveCalculator.CalcDefenseBonus();
            int total = baseDef + passiveBonus;
            if (total < 0) total = 0;
            return total;
        }
    }

    // =========================================================
    // MP 計算（INT ベース + パッシブ）
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
    /// </summary>
    public void RecalcMaxMp()
    {
        int newMaxMp = CalcMaxMp();

        if (newMaxMp != maxMp)
        {
            Debug.Log($"[GameState] maxMp 再計算: {maxMp} → {newMaxMp}");
            maxMp = newMaxMp;

            if (currentMp > maxMp)
            {
                currentMp = maxMp;
            }
        }
    }

    /// <summary>
    /// 最大MPを計算する（値を返すだけで、maxMp フィールドは変更しない）。
    /// 計算式: BaseMpInitial + baseINT × MpPerInt + PassiveCalculator.CalcMaxMpBonus()
    /// </summary>
    public int CalcMaxMp()
    {
        int intBonus = baseINT * MpPerInt;
        int passiveBonus = PassiveCalculator.CalcMaxMpBonus();
        int result = BaseMpInitial + intBonus + passiveBonus;
        if (result < 1) result = 1;
        return result;
    }

    // =========================================================
    // 魔法攻撃力・魔法防御力（INT ベース + パッシブ）
    // =========================================================

    /// <summary>
    /// プレイヤーの魔法攻撃力。
    /// 計算式: baseINT × 1
    /// 今後パッシブで変動する場合はここに加算する。
    /// </summary>
    public int MagicAttack
    {
        get
        {
            int total = baseINT * 1;
            if (total < 0) total = 0;
            return total;
        }
    }

    /// <summary>
    /// INT 1ポイントあたりの魔法防御力。
    /// </summary>
    private const int MagicDefPerInt = 2;

    /// <summary>
    /// プレイヤーの魔法防御力。
    /// 計算式: baseINT × MagicDefPerInt + PassiveCalculator.CalcMagicDefenseBonus()
    /// </summary>
    public int MagicDefense
    {
        get
        {
            int baseMDef = baseINT * MagicDefPerInt;
            int passiveBonus = PassiveCalculator.CalcMagicDefenseBonus();
            int total = baseMDef + passiveBonus;
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

        // 初回起動時に VIT/INT とパッシブを反映した maxHp/maxMp を計算する
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