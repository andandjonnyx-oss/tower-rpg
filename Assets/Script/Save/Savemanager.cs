using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// ゲームデータのセーブ/ロード/削除を担当する。
/// JSON 形式で Application.persistentDataPath に保存する。
/// 状態が変わるたびに Save() が呼ばれる即時セーブ方式。
/// </summary>
public static class SaveManager
{
    // セーブファイル名
    private const string SaveFileName = "savedata.json";

    /// <summary>セーブファイルのフルパスを返す。</summary>
    private static string SaveFilePath
        => Path.Combine(Application.persistentDataPath, SaveFileName);

    /// <summary>セーブデータが存在するかどうか。</summary>
    public static bool HasSaveData()
    {
        return File.Exists(SaveFilePath);
    }

    // =========================================================
    // セーブ
    // =========================================================

    /// <summary>
    /// 現在の GameState・ItemBoxManager・StorageManager の内容をファイルに保存する。
    /// 状態が変わるたび（アイテム取得、装備変更、階層進行など）に呼ばれる。
    /// </summary>
    public static void Save()
    {
        var data = new SaveData();

        // --- GameState から収集 ---
        if (GameState.I != null)
        {
            data.floor = GameState.I.floor;
            data.step = GameState.I.step;
            data.reachedFloor = GameState.I.reachedFloor;

            data.level = GameState.I.level;
            data.currentExp = GameState.I.currentExp;
            data.expToNext = GameState.I.expToNext;

            data.maxHp = GameState.I.maxHp;
            data.maxMp = GameState.I.maxMp;
            data.currentHp = GameState.I.currentHp;
            data.currentMp = GameState.I.currentMp;

            data.baseSTR = GameState.I.baseSTR;
            data.baseVIT = GameState.I.baseVIT;
            data.baseINT = GameState.I.baseINT;
            data.baseDEX = GameState.I.baseDEX;
            data.baseLUC = GameState.I.baseLUC;

            data.initialSTR = GameState.I.initialSTR;
            data.initialVIT = GameState.I.initialVIT;
            data.initialINT = GameState.I.initialINT;
            data.initialDEX = GameState.I.initialDEX;
            data.initialLUC = GameState.I.initialLUC;

            data.statusPoint = GameState.I.statusPoint;
            data.gp = GameState.I.gp;

            data.equippedWeaponUid = GameState.I.equippedWeaponUid;

            // 既読イベントID一覧
            data.playedEventIds = GameState.I.GetAllPlayedIds();

            // 遭遇モンスターID一覧
            data.encounteredMonsterIds = GameState.I.GetAllEncounteredIds();

            // 状態異常（追加）
            data.isPoisoned = GameState.I.isPoisoned;
            data.isSilenced = GameState.I.isSilenced;

            // 石化（Phase C 追加）
            data.playerIsPetrified = GameState.I.isPetrified;
            data.playerPetrifyTurns = GameState.I.playerPetrifyTurns;
            data.playerPetrifyMaxTurns = GameState.I.playerPetrifyMaxTurns;

        }

        // --- ItemBoxManager（所持品）から収集 ---
        if (ItemBoxManager.Instance != null)
        {
            var items = ItemBoxManager.Instance.GetItems();
            data.inventoryItems = new List<SavedItem>();
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] != null && items[i].data != null)
                {
                    data.inventoryItems.Add(new SavedItem
                    {
                        itemId = items[i].data.itemId,
                        uid = items[i].uid
                    });
                }
            }
        }

        // --- StorageManager（倉庫）から収集 ---
        if (StorageManager.Instance != null)
        {
            var storageItems = StorageManager.Instance.GetItems();
            data.storageItems = new List<SavedItem>();
            for (int i = 0; i < storageItems.Count; i++)
            {
                if (storageItems[i] != null && storageItems[i].data != null)
                {
                    data.storageItems.Add(new SavedItem
                    {
                        itemId = storageItems[i].data.itemId,
                        uid = storageItems[i].uid
                    });
                }
            }
        }

        // --- JSON に変換して書き出し ---
        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(SaveFilePath, json);
        Debug.Log($"[SaveManager] セーブ完了: {SaveFilePath}");
    }

    // =========================================================
    // ロード
    // =========================================================

    /// <summary>
    /// セーブファイルから GameState・ItemBoxManager・StorageManager にデータを復元する。
    /// タイトルのスタートボタン（セーブデータあり時）で呼ばれる。
    ///
    /// ★ブラッシュアップ:
    ///   ロード後は Main シーンに遷移し、HP/MP は全回復＋状態異常クリア。
    ///   「街に戻る = 全回復（状態異常含む）」で統一。
    ///   セーブデータには isPoisoned を保持するが、
    ///   ロード復帰時は全クリアする。
    /// </summary>
    public static bool Load()
    {
        if (!HasSaveData())
        {
            Debug.LogWarning("[SaveManager] セーブデータが見つかりません");
            return false;
        }

        string json = File.ReadAllText(SaveFilePath);
        var data = JsonUtility.FromJson<SaveData>(json);
        if (data == null)
        {
            Debug.LogError("[SaveManager] セーブデータの読み込みに失敗");
            return false;
        }

        // --- GameState に反映 ---
        if (GameState.I != null)
        {
            GameState.I.floor = data.floor;
            GameState.I.step = data.step;
            GameState.I.reachedFloor = data.reachedFloor;

            GameState.I.level = data.level;
            GameState.I.currentExp = data.currentExp;
            GameState.I.expToNext = data.expToNext;

            GameState.I.maxHp = data.maxHp;
            GameState.I.maxMp = data.maxMp;
            // ★ブラッシュアップ: ロード時は全回復状態で開始（どのシーンで中断しても安全に復帰）
            GameState.I.currentHp = data.maxHp;
            GameState.I.currentMp = data.maxMp;

            GameState.I.baseSTR = data.baseSTR;
            GameState.I.baseVIT = data.baseVIT;
            GameState.I.baseINT = data.baseINT;
            GameState.I.baseDEX = data.baseDEX;
            GameState.I.baseLUC = data.baseLUC;

            GameState.I.initialSTR = data.initialSTR;
            GameState.I.initialVIT = data.initialVIT;
            GameState.I.initialINT = data.initialINT;
            GameState.I.initialDEX = data.initialDEX;
            GameState.I.initialLUC = data.initialLUC;

            GameState.I.statusPoint = data.statusPoint;
            GameState.I.gp = data.gp;

            GameState.I.equippedWeaponUid = data.equippedWeaponUid ?? "";

            // 既読イベント復元
            GameState.I.RestorePlayedIds(data.playedEventIds);

            // 遭遇モンスター復元
            GameState.I.RestoreEncounteredIds(data.encounteredMonsterIds);

            // バトル中フラグをクリア（中断復帰なので安全な状態にする）
            GameState.I.isInBattle = false;
            GameState.I.battleTurnConsumed = false;
            GameState.I.battleItemActionLog = "";
            GameState.I.pendingItemData = null;
            GameState.I.pendingEventId = "";

            // ★ブラッシュアップ: ロード時は全状態異常をクリア
            // 「街に戻る = 全回復（状態異常含む）」で統一。
            // 以前は isPoisoned を復元していたが、
            // ロードは常に街（Main）スタートなので全クリアが正しい。
            // 石化もここでクリアされる（ClearAllStatusEffects が石化リセットを含む）。
            GameState.I.ClearAllStatusEffects();
        }

        // --- ItemBoxManager（所持品）に反映 ---
        if (ItemBoxManager.Instance != null)
        {
            ItemBoxManager.Instance.RestoreFromSave(data.inventoryItems);
        }

        // --- StorageManager（倉庫）に反映 ---
        if (StorageManager.Instance != null)
        {
            StorageManager.Instance.RestoreFromSave(data.storageItems);
        }

        Debug.Log($"[SaveManager] ロード完了: Floor={data.floor} Step={data.step} (全回復+状態異常クリア)");
        return true;
    }

    // =========================================================
    // 削除（初期化）
    // =========================================================

    /// <summary>
    /// セーブデータを削除する。タイトルの「初期化」ボタンで呼ばれる。
    /// </summary>
    public static void DeleteSave()
    {
        if (File.Exists(SaveFilePath))
        {
            File.Delete(SaveFilePath);
            Debug.Log("[SaveManager] セーブデータを削除しました");
        }
    }
}

// =========================================================
// セーブデータ構造体
// =========================================================

/// <summary>
/// JSON シリアライズ用のセーブデータ構造。
/// GameState・ItemBoxManager・StorageManager の永続化が必要なフィールドをまとめる。
/// </summary>
[Serializable]
public class SaveData
{
    // --- 進行状況 ---
    public int floor = 1;
    public int step = 1;
    public int reachedFloor = 1;

    // --- レベル/経験値 ---
    public int level = 1;
    public int currentExp = 0;
    public int expToNext = 100;

    // --- HP/MP ---
    public int maxHp = 50;
    public int maxMp = 20;
    public int currentHp = 50;
    public int currentMp = 20;

    // --- ステータス ---
    public int baseSTR = 1;
    public int baseVIT = 1;
    public int baseINT = 1;
    public int baseDEX = 1;
    public int baseLUC = 1;

    public int initialSTR = 1;
    public int initialVIT = 1;
    public int initialINT = 1;
    public int initialDEX = 1;
    public int initialLUC = 1;

    public int statusPoint = 10;
    public int gp = 0;

    // --- 装備 ---
    public string equippedWeaponUid = "";

    // --- 状態異常（追加） ---
    // セーブには保存するが、ロード時は ClearAllStatusEffects() でクリアする。
    public bool isPoisoned = false;
    public bool isSilenced = false;

    // --- 石化（Phase C 追加） ---
    // セーブには保存するが、ロード時は ClearAllStatusEffects() でクリアする。
    // 既存セーブデータとの互換性: JSON デシリアライズのデフォルト値（false/0）で自動対応。
    public bool playerIsPetrified = false;
    public int playerPetrifyTurns = 0;
    public int playerPetrifyMaxTurns = 0;


    // --- 既読イベントID一覧 ---
    public List<string> playedEventIds = new List<string>();

    // --- 遭遇モンスターID一覧 ---
    public List<string> encounteredMonsterIds = new List<string>();

    // --- 所持アイテム一覧 ---
    public List<SavedItem> inventoryItems = new List<SavedItem>();

    // --- 倉庫アイテム一覧 ---
    public List<SavedItem> storageItems = new List<SavedItem>();
}

/// <summary>
/// アイテム1個分のセーブ用データ。
/// itemId でマスターデータ（ItemData）を特定し、uid で個体を復元する。
/// 所持品と倉庫で共通の構造体。
/// </summary>
[Serializable]
public class SavedItem
{
    public string itemId;
    public string uid;
}