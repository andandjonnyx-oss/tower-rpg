using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// デバッグシーン用コントローラー。
/// メイン画面からボタンで遷移し、各種テスト操作を行う。
/// リリースビルドでは本シーンごと除外する。
/// </summary>
public class DebugSceneManager : MonoBehaviour
{
    [Header("Database")]
    [SerializeField] private ItemDatabase itemDatabase;
    [SerializeField] private MonsterDatabase monsterDatabase;

    [Header("--- アイテム入手 ---")]
    [SerializeField] private TMP_Dropdown itemDropdown;
    [SerializeField] private Button addItemButton;
    [SerializeField] private TextMeshProUGUI itemResultText;

    [Header("--- ステータス操作 ---")]
    [SerializeField] private TMP_InputField levelInput;
    [SerializeField] private Button setLevelButton;
    [SerializeField] private TMP_InputField statusPointInput;
    [SerializeField] private Button addStatusPointButton;
    [SerializeField] private Button fullRecoverButton;
    [SerializeField] private TextMeshProUGUI statusResultText;

    [Header("--- モンスター戦闘 ---")]
    [SerializeField] private TMP_Dropdown monsterDropdown;
    [SerializeField] private Button battleButton;

    [Header("--- フロアワープ ---")]
    [SerializeField] private TMP_Dropdown floorDropdown;
    [SerializeField] private Button warpButton;
    [SerializeField] private TextMeshProUGUI warpResultText;

    [Header("--- ナビゲーション ---")]
    [SerializeField] private Button backButton;
    [SerializeField] private string mainSceneName = "Main";
    [SerializeField] private string battleSceneName = "Battle";
    [SerializeField] private string towerSceneName = "Tower";
    [SerializeField] private string debugSceneName = "Debug";

    [Header("--- 設定 ---")]
    [Tooltip("フロアドロップダウンの最大階数")]
    [SerializeField] private int maxFloorForDropdown = 100;

    private List<ItemData> sortedItems = new();
    private List<Monster> sortedMonsters = new();

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
    // ① アイテム入手
    // =========================================================
    private void SetupItemSection()
    {
        if (itemDatabase == null || itemDropdown == null) return;

        sortedItems.Clear();
        foreach (var item in itemDatabase.items)
            if (item != null) sortedItems.Add(item);

        sortedItems.Sort((a, b) => string.Compare(a.itemId, b.itemId, System.StringComparison.Ordinal));

        itemDropdown.ClearOptions();
        var options = new List<string>();
        foreach (var item in sortedItems)
            options.Add($"{item.itemId} - {item.itemName}");
        itemDropdown.AddOptions(options);

        if (addItemButton != null)
            addItemButton.onClick.AddListener(OnAddItem);
    }

    private void OnAddItem()
    {
        if (sortedItems.Count == 0) return;
        int idx = itemDropdown.value;
        if (idx < 0 || idx >= sortedItems.Count) return;
        var selectedItem = sortedItems[idx];

        if (ItemBoxManager.Instance == null) { ShowItemResult("エラー: ItemBoxManager が見つかりません"); return; }
        if (ItemBoxManager.Instance.IsFull) { ShowItemResult("所持品がいっぱいです"); return; }

        ItemBoxManager.Instance.AddItem(selectedItem);
        SaveManager.Save();
        ShowItemResult($"入手: {selectedItem.itemName}");
    }

    private void ShowItemResult(string msg) { if (itemResultText != null) itemResultText.text = msg; }

    // =========================================================
    // ② ステータス操作
    // =========================================================
    private void SetupStatusSection()
    {
        if (setLevelButton != null) setLevelButton.onClick.AddListener(OnSetLevel);
        if (addStatusPointButton != null) addStatusPointButton.onClick.AddListener(OnAddStatusPoint);
        if (fullRecoverButton != null) fullRecoverButton.onClick.AddListener(OnFullRecover);
    }

    private void OnSetLevel()
    {
        var gs = GameState.I;
        if (gs == null) { ShowStatusResult("GameState が見つかりません"); return; }
        if (levelInput == null || !int.TryParse(levelInput.text, out int newLevel)) { ShowStatusResult("数値を入力してください"); return; }
        if (newLevel < 1) newLevel = 1;

        int oldLevel = gs.level;
        gs.level = newLevel;
        gs.currentExp = 0;
        gs.expToNext = GameState.CalcExpToNext(newLevel);
        gs.RecalcMaxHp();
        gs.RecalcMaxMp();
        SaveManager.Save();
        ShowStatusResult($"レベル変更: Lv{oldLevel} → Lv{newLevel}");
    }

    private void OnAddStatusPoint()
    {
        var gs = GameState.I;
        if (gs == null) { ShowStatusResult("GameState が見つかりません"); return; }
        if (statusPointInput == null || !int.TryParse(statusPointInput.text, out int addPoint)) { ShowStatusResult("数値を入力してください"); return; }
        if (addPoint <= 0) { ShowStatusResult("1以上を入力してください"); return; }

        gs.statusPoint += addPoint;
        SaveManager.Save();
        ShowStatusResult($"ステータスポイント +{addPoint} (合計: {gs.statusPoint})");
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
    }

    private void ShowStatusResult(string msg) { if (statusResultText != null) statusResultText.text = msg; }

    // =========================================================
    // ③ モンスター戦闘
    // =========================================================
    private void SetupMonsterSection()
    {
        if (monsterDatabase == null || monsterDropdown == null) return;

        sortedMonsters.Clear();
        foreach (var m in monsterDatabase.monsters)
            if (m != null) sortedMonsters.Add(m);

        sortedMonsters.Sort((a, b) => string.Compare(a.ID, b.ID, System.StringComparison.Ordinal));

        monsterDropdown.ClearOptions();
        var options = new List<string>();
        foreach (var m in sortedMonsters)
            options.Add($"{m.ID} - {m.Mname}");
        monsterDropdown.AddOptions(options);

        if (battleButton != null)
            battleButton.onClick.AddListener(OnStartBattle);
    }

    private void OnStartBattle()
    {
        if (sortedMonsters.Count == 0) return;
        int idx = monsterDropdown.value;
        if (idx < 0 || idx >= sortedMonsters.Count) return;
        var selectedMonster = sortedMonsters[idx];

        BattleContext.EnemyMonster = selectedMonster;
        BattleContext.IsBossBattle = false;
        BattleContext.Floor = GameState.I != null ? GameState.I.floor : 1;
        BattleContext.IsDebugBattle = true;
        BattleContext.DebugReturnScene = debugSceneName;

        Debug.Log($"[Debug] バトル開始: {selectedMonster.ID} ({selectedMonster.Mname})");
        SceneManager.LoadScene(battleSceneName, LoadSceneMode.Single);
    }

    // =========================================================
    // ④ フロアワープ
    // =========================================================
    private void SetupFloorSection()
    {
        if (floorDropdown == null) return;

        floorDropdown.ClearOptions();
        var options = new List<string>();
        for (int f = 1; f <= maxFloorForDropdown; f++)
            options.Add($"{f}階");
        floorDropdown.AddOptions(options);

        if (warpButton != null)
            warpButton.onClick.AddListener(OnWarp);
    }

    private void OnWarp()
    {
        var gs = GameState.I;
        if (gs == null) { ShowWarpResult("GameState が見つかりません"); return; }

        int targetFloor = floorDropdown.value + 1;
        gs.floor = targetFloor;
        gs.step = 1;
        gs.UpdateReachedFloor(targetFloor);
        SaveManager.Save();

        ShowWarpResult($"{targetFloor}階 1STEP にワープ！");
        SceneManager.LoadScene(towerSceneName, LoadSceneMode.Single);
    }

    private void ShowWarpResult(string msg) { if (warpResultText != null) warpResultText.text = msg; }
}