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

    // =========================================================
    // サブステータス（パッシブ込みの実効値）
    // =========================================================

    /// <summary>
    /// 攻撃力。計算式: baseSTR × 1 + PassiveCalculator.CalcAttackBonus()
    /// 将来的に攻撃力+X のパッシブが追加された場合は CalcAttackBonus() に実装する。
    /// </summary>
    public int Attack
    {
        get
        {
            int total = baseSTR * 1 + PassiveCalculator.CalcAttackBonus();
            if (total < 0) total = 0;
            return total;
        }
    }

    /// <summary>
    /// 魔法攻撃力。計算式: baseINT × 1
    /// 将来的にパッシブで変動する場合はここに加算する。
    /// </summary>
    public int MagicAttack
    {
        get
        {
            int total = baseINT * 1 + PassiveCalculator.CalcMagicAttackBonus();
            if (total < 0) total = 0;
            return total;
        }
    }

    /// <summary>
    /// 運の良さ。baseLUC をそのまま返す。
    /// 敵行動のLUC判定では baseLUC を直接参照するが、
    /// UI表示などはこのプロパティを使う。
    /// </summary>
    public int Luck
    {
        get
        {
            int total = baseLUC + PassiveCalculator.CalcLuckBonus();
            if (total < 0) total = 0;
            return total;
        }
    }

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
    /// currentHp が -1（未初期化）の場合のみ maxHp に揃える（ゲーム初回起動時）。
    ///
    /// 呼び出しタイミング:
    ///   - Awake()（初回起動 or セーブロード後）
    ///   - ステータス振り分け後
    ///   - アイテム取得/破棄後
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
    // 魔法防御力（INT ベース + パッシブ）
    // =========================================================

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