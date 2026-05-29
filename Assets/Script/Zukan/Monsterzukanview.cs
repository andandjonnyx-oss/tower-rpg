using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// ZukanMシーン（モンスター一覧）のコントローラー。
/// MonsterDatabase から全モンスターを取得し、
/// 通常/ボス切替ボタンで表示リストを切り替える。
/// 未遭遇モンスターは「？」表示でタップ無効。
/// 遭遇済みモンスターをタップすると Mstatus シーンへ遷移。
///
/// レイアウト:
///   左側: 通常/ボス切替ボタン + 戻るボタン
///   右側: ScrollView + GridLayoutGroup でアイコングリッド
/// </summary>
public class MonsterZukanView : MonoBehaviour
{
    // =========================================================
    // Inspector 参照
    // =========================================================

    [Header("Data")]
    [Tooltip("モンスターデータベース（SOアセットをアサイン）")]
    [SerializeField] private MonsterDatabase monsterDatabase;

    [Header("Grid")]
    [Tooltip("アイコンセルの Prefab（MonsterIconCell）")]
    [SerializeField] private MonsterIconCell cellPrefab;

    [Tooltip("GridLayoutGroup がアタッチされた Content Transform")]
    [SerializeField] private Transform gridContent;

    [Header("Buttons")]
    [Tooltip("通常モンスター表示ボタン")]
    [SerializeField] private Button normalButton;

    [Tooltip("ボスモンスター表示ボタン")]
    [SerializeField] private Button bossButton;

    [Tooltip("戻るボタン（Zukan シーンへ）")]
    [SerializeField] private Button backButton;

    [Header("Scene Names")]
    [SerializeField] private string zukanSceneName = "Zukan";
    [SerializeField] private string mstatusSceneName = "Mstatus";

    // =========================================================
    // 内部状態
    // =========================================================

    /// <summary>通常モンスターリスト（IDソート済み）</summary>
    private List<Monster> normalMonsters = new List<Monster>();

    /// <summary>ボスモンスターリスト（IDソート済み）</summary>
    private List<Monster> bossMonsters = new List<Monster>();

    /// <summary>現在表示中がボスリストかどうか</summary>
    private bool showingBoss = false;

    /// <summary>生成済みセル一覧</summary>
    private List<MonsterIconCell> cells = new List<MonsterIconCell>();

    // =========================================================
    // 初期化
    // =========================================================

    private void Start()
    {
        // データベースから分離・ソート
        BuildMonsterLists();

        // ボタン登録
        if (normalButton != null) normalButton.onClick.AddListener(OnNormalClicked);
        if (bossButton != null) bossButton.onClick.AddListener(OnBossClicked);
        if (backButton != null) backButton.onClick.AddListener(OnBackClicked);

        // 初期表示: 通常モンスター
        showingBoss = false;
        RefreshGrid();
        UpdateButtonVisual();
    }

    // =========================================================
    // データ準備
    // =========================================================

    /// <summary>
    /// MonsterDatabase.monsters を通常・ボスに分離し、ID昇順でソートする。
    /// </summary>
    private void BuildMonsterLists()
    {
        normalMonsters.Clear();
        bossMonsters.Clear();

        if (monsterDatabase == null || monsterDatabase.monsters == null) return;

        foreach (var m in monsterDatabase.monsters)
        {
            if (m == null) continue;
            if (m.IsBoss)
                bossMonsters.Add(m);
            else
                normalMonsters.Add(m);
        }

        // ID文字列の自然順ソート（数値部を数値として比較、'_' は 'Z' より先扱い）
        normalMonsters.Sort((a, b) => NaturalCompare(a.ID, b.ID));
        bossMonsters.Sort((a, b) => NaturalCompare(a.ID, b.ID));

        Debug.Log($"[MonsterZukan] 通常:{normalMonsters.Count}体 / ボス:{bossMonsters.Count}体");
    }

    // =========================================================
    // グリッド表示
    // =========================================================

    /// <summary>
    /// 現在のリスト（通常 or ボス）でグリッドを再構築する。
    /// 既存セルを全削除して再生成する。
    /// </summary>
    private void RefreshGrid()
    {
        // 既存セルを破棄
        foreach (var cell in cells)
        {
            if (cell != null) Destroy(cell.gameObject);
        }
        cells.Clear();

        List<Monster> list = showingBoss ? bossMonsters : normalMonsters;

        if (cellPrefab == null || gridContent == null) return;

        foreach (var monster in list)
        {
            MonsterIconCell cell = Instantiate(cellPrefab, gridContent);
            bool encountered = GameState.I != null && GameState.I.IsEncountered(monster.ID);
            cell.Setup(monster, encountered, OnCellClicked);
            cells.Add(cell);
        }
    }

    // =========================================================
    // セルタップコールバック
    // =========================================================

    /// <summary>
    /// 遭遇済みモンスターのセルをタップした時のコールバック。
    /// ZukanContext にモンスターと閲覧可能リストをセットして Mstatus シーンへ遷移。
    /// </summary>
    private void OnCellClicked(Monster monster)
    {
        if (monster == null) return;

        // 現在表示中のリストから遭遇済みだけを抽出（↑↓切替用）
        List<Monster> fullList = showingBoss ? bossMonsters : normalMonsters;
        var encounteredList = new List<Monster>();
        foreach (var m in fullList)
        {
            if (m != null && GameState.I != null && GameState.I.IsEncountered(m.ID))
                encounteredList.Add(m);
        }

        ZukanContext.SelectedMonster = monster;
        ZukanContext.EncounteredList = encounteredList;
        ZukanContext.CurrentIndex = encounteredList.IndexOf(monster);

        SceneManager.LoadScene(mstatusSceneName);
    }

    // =========================================================
    // ボタンハンドラ
    // =========================================================

    private void OnNormalClicked()
    {
        if (showingBoss)
        {
            showingBoss = false;
            RefreshGrid();
            UpdateButtonVisual();
        }
    }

    private void OnBossClicked()
    {
        if (!showingBoss)
        {
            showingBoss = true;
            RefreshGrid();
            UpdateButtonVisual();
        }
    }

    private void OnBackClicked()
    {
        SceneManager.LoadScene(zukanSceneName);
    }

    /// <summary>
    /// 通常/ボスボタンの見た目を更新する。
    /// 選択中のボタンを非インタラクティブにすることで「選択中」を表現。
    /// </summary>
    private void UpdateButtonVisual()
    {
        if (normalButton != null) normalButton.interactable = showingBoss;
        if (bossButton != null) bossButton.interactable = !showingBoss;
    }

    // =========================================================
    // 自然順比較（追加）
    // =========================================================
    //
    // 文字列を「数字ブロック」と「非数字ブロック」に分解し、
    //   - 数字ブロックは数値として比較（"010" < "020" < "100"）
    //   - 非数字ブロックは ASCII 比較
    // することで、ファイル名の自然な並び順を再現する。
    //
    // 加えて、第二形態の 'Z' サフィックス（例: F070BZ_kyuubi）が
    // 第一形態（F070B_dakki）より後に来るよう、'_' を ASCII 値 1 として
    // 扱い、'Z'(90) より小さくする特殊処理を入れる。
    // これにより F070B_dakki < F070BZ_kyuubi の順になる。
    //
    // 入力が null の場合は空文字扱い。
    // =========================================================
    private static int NaturalCompare(string a, string b)
    {
        if (a == null) a = "";
        if (b == null) b = "";

        int i = 0, j = 0;
        while (i < a.Length && j < b.Length)
        {
            bool aIsDigit = char.IsDigit(a[i]);
            bool bIsDigit = char.IsDigit(b[j]);

            if (aIsDigit && bIsDigit)
            {
                // 数字ブロックを切り出して数値比較
                int aStart = i;
                while (i < a.Length && char.IsDigit(a[i])) i++;
                int bStart = j;
                while (j < b.Length && char.IsDigit(b[j])) j++;

                // 先頭ゼロを除いた長さで大小比較（桁数優先）
                string aNum = a.Substring(aStart, i - aStart).TrimStart('0');
                string bNum = b.Substring(bStart, j - bStart).TrimStart('0');
                if (aNum.Length == 0) aNum = "0";
                if (bNum.Length == 0) bNum = "0";

                if (aNum.Length != bNum.Length)
                    return aNum.Length - bNum.Length;

                int numCmp = string.CompareOrdinal(aNum, bNum);
                if (numCmp != 0) return numCmp;

                // 数値が同じ場合は元の桁数（先頭ゼロの数）でも揃える: "010" vs "10" → "010" を先に
                int aRawLen = i - aStart;
                int bRawLen = j - bStart;
                if (aRawLen != bRawLen) return bRawLen - aRawLen;
            }
            else if (aIsDigit != bIsDigit)
            {
                // 片方だけ数字: 数字を先に
                return aIsDigit ? -1 : 1;
            }
            else
            {
                // 非数字ブロックを1文字ずつ比較（'_' を ASCII 1 として扱う）
                int ca = a[i] == '_' ? 1 : a[i];
                int cb = b[j] == '_' ? 1 : b[j];
                if (ca != cb) return ca - cb;
                i++;
                j++;
            }
        }

        // 残り長さで比較（短い方が先）
        return (a.Length - i) - (b.Length - j);
    }
}