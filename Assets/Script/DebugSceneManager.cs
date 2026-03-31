using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// デバッグシーン用コントローラー。
/// メイン画面からボタンで遷移し、各種テスト操作を行う。
/// リリースビルドでは本シーンごと除外する。
///
/// ■ Unity 設定手順:
/// 1. 新規シーン「Debug」を作成し、Build Settings に追加
/// 2. Canvas を配置し、本スクリプトをアタッチ
/// 3. インスペクターで各 UI 要素と Database をアサイン
/// 4. メインシーンに「デバッグ」ボタンを追加し、
///    OnClick で SceneManager.LoadScene("Debug") を呼ぶ
/// </summary>
public class DebugSceneManager : MonoBehaviour
{
    // =========================================================
    // Inspector — Database 参照
    // =========================================================
    [Header("Database")]
    [SerializeField] private ItemDatabase itemDatabase;
    [SerializeField] private MonsterDatabase monsterDatabase;

    // =========================================================
    // Inspector — UI: アイテム入手
    // =========================================================
    [Header("--- アイテム入手 ---")]
    [SerializeField] private TMP_Dropdown itemDropdown;
    [SerializeField] private Button addItemButton;
    [SerializeField] private TextMeshProUGUI itemResultText;

    // =========================================================
    // Inspector — UI: ステータス操作
    // =========================================================
    [Header("--- ステータス操作 ---")]
    [SerializeField] private TMP_InputField levelInput;
    [SerializeField] private Button setLevelButton;
    [SerializeField] private TMP_InputField statusPointInput;
    [SerializeField] private Button addStatusPointButton;
    [SerializeField] private Button fullRecoverButton;
    [SerializeField] private TextMeshProUGUI statusResultText;

    // =========================================================
    // Inspector — UI: モンスター戦闘
    // =========================================================
    [Header("--- モンスター戦闘 ---")]
    [SerializeField] private TMP_Dropdown monsterDropdown;
    [SerializeField] private Button battleButton;

    // =========================================================
    // Inspector — UI: フロアワープ
    // =========================================================
    [Header("--- フロアワープ ---")]
    [SerializeField] private TMP_Dropdown floorDropdown;
    [SerializeField] private Button warpButton;
    [SerializeField] private TextMeshProUGUI warpResultText;

    // =========================================================
    // Inspector — UI: 戻るボタン
    // =========================================================
    [Header("--- ナビゲーション ---")]
    [SerializeField] private Button backButton;
    [SerializeField] private string mainSceneName = "Main";
    [SerializeField] private string battleSceneName = "Battle";
    [SerializeField] private string towerSceneName = "Tower";

    // =========================================================
    // Inspector — 設定
    // =========================================================
    [Header("--- 設定 ---")]
    [Tooltip("フロアドロップダウンの最大階数")]
    [SerializeField] private int maxFloorForDropdown = 100;

    // 内部キャッシュ
    private List<ItemData> sortedItems = new();
    private List<Monster> sortedMonsters = new();

    // =========================================================
    // 初期化
    // =========================================================
    private void Start()
    {
        SetupItemSection();
        SetupStatusSection();
        SetupMonsterSection();
        SetupFloorSection();

        if (backButton != null)
            backButton.onClick.AddListener(() => SceneManager.LoadScene(mainSceneName));
    }

    // =========================================================
    // ① アイテム入手セクション
    // =========================================================
    private void SetupItemSection()
    {
        if (itemDatabase == null || itemDropdown == null) return;

        // ItemDatabase の items リストをそのまま使う
        sortedItems.Clear();
        foreach (var item in itemDatabase.items)
        {
            if (item != null) sortedItems.Add(item);
        }

        // ID 昇順でソート（ItemData.itemId）
        sortedItems.Sort((a, b) => string.Compare(a.itemId, b.itemId, System.StringComparison.Ordinal));

        // ドロップダウン構築
        itemDropdown.ClearOptions();
        var options = new List<string>();
        foreach (var item in sortedItems)
        {
            // 例: "C001_Yakusou - 薬草"
            string label = $"{item.itemId} - {item.itemName}";
            options.Add(label);
        }
        itemDropdown.AddOptions(options);

        // ボタン
        if (addItemButton != null)
            addItemButton.onClick.AddListener(OnAddItem);
    }

    private void OnAddItem()
    {
        if (sortedItems.Count == 0) return;

        int idx = itemDropdown.value;
        if (idx < 0 || idx >= sortedItems.Count) return;

        var selectedItem = sortedItems[idx];

        // ItemBoxManager 経由で所持品に追加
        if (ItemBoxManager.Instance == null)
        {
            ShowItemResult("エラー: ItemBoxManager が見つかりません");
            return;
        }

        if (ItemBoxManager.Instance.IsFull)
        {
            ShowItemResult("所持品がいっぱいです");
            return;
        }

        ItemBoxManager.Instance.AddItem(selectedItem);
        SaveManager.Save();

        ShowItemResult($"入手: {selectedItem.itemName}");
        Debug.Log($"[Debug] アイテム入手: {selectedItem.itemId} ({selectedItem.itemName})");
    }

    private void ShowItemResult(string msg)
    {
        if (itemResultText != null) itemResultText.text = msg;
    }

    // =========================================================
    // ② ステータス操作セクション
    // =========================================================
    private void SetupStatusSection()
    {
        if (setLevelButton != null)
            setLevelButton.onClick.AddListener(OnSetLevel);

        if (addStatusPointButton != null)
            addStatusPointButton.onClick.AddListener(OnAddStatusPoint);

        if (fullRecoverButton != null)
            fullRecoverButton.onClick.AddListener(OnFullRecover);
    }

    private void OnSetLevel()
    {
        var gs = GameState.I;
        if (gs == null) { ShowStatusResult("GameState が見つかりません"); return; }

        if (levelInput == null || !int.TryParse(levelInput.text, out int newLevel))
        {
            ShowStatusResult("数値を入力してください");
            return;
        }

        if (newLevel < 1) newLevel = 1;

        int oldLevel = gs.level;
        gs.level = newLevel;
        gs.currentExp = 0;
        gs.expToNext = GameState.CalcExpToNext(newLevel);

        // maxHp/maxMp 再計算
        gs.RecalcMaxHp();
        gs.RecalcMaxMp();

        SaveManager.Save();
        ShowStatusResult($"レベル変更: Lv{oldLevel} → Lv{newLevel}");
        Debug.Log($"[Debug] レベル変更: {oldLevel} → {newLevel}");
    }

    private void OnAddStatusPoint()
    {
        var gs = GameState.I;
        if (gs == null) { ShowStatusResult("GameState が見つかりません"); return; }

        if (statusPointInput == null || !int.TryParse(statusPointInput.text, out int addPoint))
        {
            ShowStatusResult("数値を入力してください");
            return;
        }

        if (addPoint <= 0)
        {
            ShowStatusResult("1以上を入力してください");
            return;
        }

        gs.statusPoint += addPoint;
        SaveManager.Save();

        ShowStatusResult($"ステータスポイント +{addPoint} (合計: {gs.statusPoint})");
        Debug.Log($"[Debug] ステータスポイント追加: +{addPoint} → 合計{gs.statusPoint}");
    }

    private void OnFullRecover()
    {
        var gs = GameState.I;
        if (gs == null) { ShowStatusResult("GameState が見つかりません"); return; }

        gs.currentHp = gs.maxHp;
        gs.currentMp = gs.maxMp;
        gs.ClearAllStatusEffects();
        SaveManager.Save();

        ShowStatusResult($"全回復！ HP:{gs.currentHp}/{gs.maxHp} MP:{gs.currentMp}/{gs.maxMp}");
        Debug.Log("[Debug] HP/MP 全回復 + 状態異常クリア");
    }

    private void ShowStatusResult(string msg)
    {
        if (statusResultText != null) statusResultText.text = msg;
    }

    // =========================================================
    // ③ モンスター戦闘セクション
    // =========================================================
    private void SetupMonsterSection()
    {
        if (monsterDatabase == null || monsterDropdown == null) return;

        // MonsterDatabase の monsters リストをそのまま使う
        sortedMonsters.Clear();
        foreach (var m in monsterDatabase.monsters)
        {
            if (m != null) sortedMonsters.Add(m);
        }

        // ID 昇順でソート（Monster.ID）
        sortedMonsters.Sort((a, b) => string.Compare(a.ID, b.ID, System.StringComparison.Ordinal));

        // ドロップダウン構築
        monsterDropdown.ClearOptions();
        var options = new List<string>();
        foreach (var m in sortedMonsters)
        {
            // 例: "001_Slime - スライム"
            string label = $"{m.ID} - {m.Mname}";
            options.Add(label);
        }
        monsterDropdown.AddOptions(options);

        // ボタン
        if (battleButton != null)
            battleButton.onClick.AddListener(OnStartBattle);
    }

    private void OnStartBattle()
    {
        if (sortedMonsters.Count == 0) return;

        int idx = monsterDropdown.value;
        if (idx < 0 || idx >= sortedMonsters.Count) return;

        var selectedMonster = sortedMonsters[idx];

        // BattleContext にセット（通常エンカウントと同じ流れ）
        BattleContext.EnemyMonster = selectedMonster;
        BattleContext.IsBossBattle = false; // デバッグ戦闘はボス扱いしない
        BattleContext.Floor = GameState.I != null ? GameState.I.floor : 1;

        Debug.Log($"[Debug] バトル開始: {selectedMonster.ID} ({selectedMonster.Mname})");

        // Battle シーンへ遷移
        SceneManager.LoadScene(battleSceneName, LoadSceneMode.Single);
    }

    // =========================================================
    // ④ フロアワープセクション
    // =========================================================
    private void SetupFloorSection()
    {
        if (floorDropdown == null) return;

        // ドロップダウン構築: 1階 ～ maxFloorForDropdown階
        floorDropdown.ClearOptions();
        var options = new List<string>();
        for (int f = 1; f <= maxFloorForDropdown; f++)
        {
            options.Add($"{f}階");
        }
        floorDropdown.AddOptions(options);

        // ボタン
        if (warpButton != null)
            warpButton.onClick.AddListener(OnWarp);
    }

    private void OnWarp()
    {
        var gs = GameState.I;
        if (gs == null) { ShowWarpResult("GameState が見つかりません"); return; }

        int targetFloor = floorDropdown.value + 1; // ドロップダウンは0始まり

        gs.floor = targetFloor;
        gs.step = 1;
        gs.UpdateReachedFloor(targetFloor);
        SaveManager.Save();

        ShowWarpResult($"{targetFloor}階 1STEP にワープ！");
        Debug.Log($"[Debug] ワープ: {targetFloor}階 1STEP");

        // Tower シーンへ遷移
        SceneManager.LoadScene(towerSceneName, LoadSceneMode.Single);
    }

    private void ShowWarpResult(string msg)
    {
        if (warpResultText != null) warpResultText.text = msg;
    }
}