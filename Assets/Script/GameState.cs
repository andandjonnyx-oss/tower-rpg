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
    // ステータス
    // =========================================================
    [Header("Level / EXP")]
    public int level = 1;
    public int currentExp = 0;
    public int expToNext = 100;       // 次レベルまでの必要経験値

    [Header("HP / MP")]
    public int maxHp = 50;
    public int maxMp = 20;
    public int currentHp = 50;
    public int currentMp = 20;

    [Header("Base Stats Initial (初期値・リセット先)")]
    // 初期値。レベルアップ等で底上げされた場合はここも更新する。
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

    // --- 派生ステータス（読み取り専用プロパティ） ---
    // 力     = STR × 1
    // 体力   = VIT × 2
    // 器用   = DEX × 3
    // 魔力   = INT × 4
    // 運の良さ = LUC × 5
    public int Power => baseSTR * 1;
    public int Stamina => baseVIT * 2;
    public int Dexterity => baseDEX * 3;
    public int MagicPower => baseINT * 4;
    public int Luck => baseLUC * 5;

    // =========================================================
    // ポイント振り分け / リセット
    // =========================================================

    /// StatusView の Start() から呼ばれる。
    /// 現在は何もしないが、将来の拡張用にメソッドは残しておく。
    public void TakeStatSnapshot()
    {
        // 全リセット方式のため、スナップショットは不要
    }

    /// リセット: 全ステータスを初期値に戻し、振り分けたポイントを全て返却する。
    public void ResetStatAllocation()
    {
        // 現在の振り分け量を計算して返却
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

    /// ポイントを 1 消費してステータスを +1 する。成功したら true。
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

    private void Awake()
    {
        if (I != null) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);
    }
}

/// ステータス種別
public enum StatType
{
    STR,
    VIT,
    INT,
    DEX,
    LUC
}