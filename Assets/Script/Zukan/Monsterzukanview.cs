using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
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

    [Tooltip("グリッドの ScrollRect（詳細から戻った際のスクロール位置復元に使用）")]
    [SerializeField] private ScrollRect scrollRect;

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

        // 詳細から戻ったかどうかで初期表示を分岐
        if (ZukanContext.ReturningFromDetail && ZukanContext.ReturnTargetMonster != null)
        {
            Monster target = ZukanContext.ReturnTargetMonster;

            // 戻り対象がボスならボスタブで開く（通常タブに戻ってしまうバグの修正）
            showingBoss = target.IsBoss;
            RefreshGrid();
            UpdateButtonVisual();

            // グリッドのレイアウト確定後に対象を画面内へスクロール
            StartCoroutine(ScrollToTargetNextFrame(target));

            // フラグは使い切り
            ZukanContext.ReturningFromDetail = false;
            ZukanContext.ReturnTargetMonster = null;
        }
        else
        {
            // トップ(Zukan)から来た場合: 通常タブ・先頭表示
            showingBoss = false;
            RefreshGrid();
            UpdateButtonVisual();
        }
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

        // コントローラー対応: 初期フォーカスを先頭セルへ即時設定し、
        // レイアウト確定後にナビ配線を組み直す（アイテム図鑑と同方式）。
        SetInitialFocusToFirstItem();
        StartCoroutine(RebuildNavAfterLayout());
    }

    // =========================================================
    // コントローラー/キーボード（2026-09-15）
    // =========================================================

    private GameObject lastSelected;

    private void Update()
    {
        var kb = Keyboard.current;
        var pad = Gamepad.current;
        if ((kb != null && kb.escapeKey.wasPressedThisFrame)
            || (pad != null && pad.buttonEast.wasPressedThisFrame))
        {
            OnBackClicked();
            return;
        }

        var es = EventSystem.current;
        if (es == null) return;
        var sel = es.currentSelectedGameObject;
        if (sel != lastSelected)
        {
            lastSelected = sel;
            if (sel != null && sel.GetComponent<MonsterIconCell>() != null)
                CenterOn(sel.transform as RectTransform);
        }
    }

    private void SetInitialFocusToFirstItem()
    {
        if (gridContent == null) return;
        var cell = gridContent.GetComponentInChildren<MonsterIconCell>(false);
        Selectable target = cell != null ? cell.GetComponent<Selectable>() : null;
        if (target == null && backButton != null) target = backButton;
        SelectionHighlighter.PreferredFallback = target;
    }

    private IEnumerator RebuildNavAfterLayout()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
        yield return null;
        RebuildNav();
    }

    /// <summary>
    /// タブ（通常/ボスの操作可能な方）＋戻る と、モンスターグリッドを明示配線する。
    /// 未遭遇(？)も含め全セルを位置から格子判定。戻る/タブ→右で先頭セル、
    /// 行左端→Y最近傍のタブ/戻る。初期フォーカスは先頭セル。
    /// </summary>
    private void RebuildNav()
    {
        if (gridContent == null) return;

        var cellSel = new List<Selectable>();
        foreach (var c in cells)
        {
            if (c == null || !c.gameObject.activeInHierarchy) continue;
            var sel = c.GetComponent<Selectable>();
            if (sel != null && sel.interactable) cellSel.Add(sel);
        }
        cellSel.Sort((a, b) =>
        {
            float ay = a.transform.position.y, by = b.transform.position.y;
            if (!Mathf.Approximately(ay, by)) return by.CompareTo(ay);
            return a.transform.position.x.CompareTo(b.transform.position.x);
        });

        var rows = new List<List<Selectable>>();
        List<Selectable> cur = null;
        float curY = 0f;
        foreach (var s in cellSel)
        {
            float y = s.transform.position.y;
            if (cur == null || Mathf.Abs(y - curY) > 20f)
            {
                cur = new List<Selectable>();
                rows.Add(cur);
                curY = y;
            }
            cur.Add(s);
        }

        var leftCol = new List<Selectable>();
        if (normalButton != null && normalButton.isActiveAndEnabled && normalButton.interactable) leftCol.Add(normalButton);
        if (bossButton != null && bossButton.isActiveAndEnabled && bossButton.interactable) leftCol.Add(bossButton);
        if (backButton != null && backButton.isActiveAndEnabled) leftCol.Add(backButton);
        leftCol.Sort((a, b) => b.transform.position.y.CompareTo(a.transform.position.y));

        Selectable centerEntry = (rows.Count > 0 && rows[0].Count > 0) ? rows[0][0] : null;

        for (int i = 0; i < leftCol.Count; i++)
        {
            Selectable up = (i > 0) ? leftCol[i - 1] : null;
            Selectable down = (i < leftCol.Count - 1) ? leftCol[i + 1] : null;
            ControllerNav.SetExplicit(leftCol[i], up, down, null, centerEntry);
        }

        for (int r = 0; r < rows.Count; r++)
        {
            var row = rows[r];
            for (int i = 0; i < row.Count; i++)
            {
                var cell = row[i];
                float x = cell.transform.position.x;
                Selectable left = (i > 0) ? row[i - 1] : NearestByY(leftCol, cell.transform.position.y);
                Selectable right = (i < row.Count - 1) ? row[i + 1] : null;
                Selectable up = (r > 0) ? NearestByX(rows[r - 1], x) : null;
                Selectable down = (r < rows.Count - 1) ? NearestByX(rows[r + 1], x) : null;
                ControllerNav.SetExplicit(cell, up, down, left, right);
            }
        }

        SelectionHighlighter.PreferredFallback = centerEntry;
    }

    private static Selectable NearestByX(List<Selectable> row, float x)
    {
        Selectable best = null; float bestD = float.MaxValue;
        for (int i = 0; i < row.Count; i++)
        {
            if (row[i] == null) continue;
            float d = Mathf.Abs(row[i].transform.position.x - x);
            if (d < bestD) { bestD = d; best = row[i]; }
        }
        return best;
    }

    private static Selectable NearestByY(List<Selectable> col, float y)
    {
        Selectable best = null; float bestD = float.MaxValue;
        for (int i = 0; i < col.Count; i++)
        {
            if (col[i] == null) continue;
            float d = Mathf.Abs(col[i].transform.position.y - y);
            if (d < bestD) { bestD = d; best = col[i]; }
        }
        return best;
    }

    private void CenterOn(RectTransform target)
    {
        if (scrollRect == null || scrollRect.content == null || target == null) return;
        RectTransform c = scrollRect.content;
        RectTransform vp = scrollRect.viewport != null ? scrollRect.viewport : scrollRect.GetComponent<RectTransform>();
        float ch = c.rect.height, vh = vp.rect.height;
        if (ch <= vh) { scrollRect.verticalNormalizedPosition = 1f; return; }
        Vector3 lp = c.InverseTransformPoint(target.position);
        float targetFromTop = c.rect.yMax - lp.y;
        float desired = targetFromTop - vh * 0.5f;
        float maxScroll = ch - vh;
        desired = Mathf.Clamp(desired, 0f, maxScroll);
        scrollRect.verticalNormalizedPosition = Mathf.Clamp01(1f - desired / maxScroll);
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

    /// <summary>
    /// 1フレーム待ってレイアウト確定後、対象モンスターのセルが
    /// 画面内に収まるようスクロール位置を調整する。
    /// Content の実高さとセルの行位置から正規化スクロール位置を算出する。
    /// 端付近は 0/1 にクランプされ自然に端表示になる。
    /// </summary>
    private System.Collections.IEnumerator ScrollToTargetNextFrame(Monster target)
    {
        // グリッド生成直後はレイアウト未確定なので待ってから確定させる
        yield return null;
        Canvas.ForceUpdateCanvases();
        yield return null;

        if (scrollRect == null || scrollRect.content == null || scrollRect.viewport == null)
            yield break;

        List<Monster> list = showingBoss ? bossMonsters : normalMonsters;
        int index = list.IndexOf(target);
        if (index < 0) yield break;

        var grid = gridContent != null ? gridContent.GetComponent<GridLayoutGroup>() : null;
        if (grid == null) yield break;

        // 固定5列前提（FixedColumnCount）。念のため取得して使う。
        int columns = (grid.constraint == GridLayoutGroup.Constraint.FixedColumnCount)
            ? Mathf.Max(1, grid.constraintCount)
            : 5;

        int row = index / columns;  // 対象が何行目か（0始まり）

        // 対象行の上端Y（Content座標系。上方向に積み上がる）
        float rowTop = grid.padding.top + row * (grid.cellSize.y + grid.spacing.y);
        // 対象セルの中心を狙う
        float rowCenter = rowTop + grid.cellSize.y * 0.5f;

        float contentHeight = scrollRect.content.rect.height;
        float viewportHeight = scrollRect.viewport.rect.height;

        // スクロール可能量がなければ先頭でよい
        if (contentHeight <= viewportHeight)
        {
            scrollRect.verticalNormalizedPosition = 1f;
            yield break;
        }

        // 対象セル中心がビューポート中央に来るような上端からの距離
        float targetTop = rowCenter - viewportHeight * 0.5f;
        float maxScroll = contentHeight - viewportHeight;
        float normalizedFromTop = Mathf.Clamp01(targetTop / maxScroll);

        // verticalNormalizedPosition は 1=最上 / 0=最下 なので反転
        scrollRect.verticalNormalizedPosition = 1f - normalizedFromTop;
    }
}