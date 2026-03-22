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
            played.Add(eventId);
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