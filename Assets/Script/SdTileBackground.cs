using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// SDキャラを対角配置で敷き詰め、行ごとに逆方向へ横スクロールさせる半透明背景。
///   1 2 3 4 5 6
///   2 3 4 5 6 1   ← この行は逆向きに流す
///   3 4 5 6 1 2
/// 各行を専用コンテナに入れ、行単位で左右逆にループさせる。
/// </summary>
public class SdTileBackground : MonoBehaviour
{
    [Header("素材（6体ぶん）")]
    public Sprite[] sprites;

    [Header("配置")]
    public RectTransform root;            // 全行をぶら下げる親（未指定なら自分自身）
    public Vector2 cellSize = new Vector2(180, 200);
    public int rows = 5;
    [Tooltip("画面に見せたい列数。実際にはこれ＋1周ぶん多く生成する。")]
    public int visibleCols = 8;

    [Header("スクロール")]
    public float scrollSpeed = 40f;       // px/秒
    [Tooltip("0行目の進行方向。trueで左へ。行ごとに反転する。")]
    public bool firstRowGoesLeft = true;

    [Header("見た目")]
    [Range(0f, 1f)] public float alpha = 0.5f;  // 半透明度

    int _period;
    float _loopWidth;
    int _genCols;                          // 実際に生成する列数

    class RowInfo
    {
        public RectTransform container;
        public bool goesLeft;
        public float startX;
    }
    readonly List<RowInfo> _rowInfos = new List<RowInfo>();

    void Start()
    {
        if (root == null) root = (RectTransform)transform;
        if (sprites == null || sprites.Length == 0) return;

        _period = sprites.Length;                 // = 6
        _loopWidth = cellSize.x * _period;        // 6体ぶんの幅
        // 画面ぶん + 1周ぶん + 予備1列。これで「最後に突然出現」を防ぐ
        _genCols = visibleCols + _period + 1;

        BuildRows();
    }

    void BuildRows()
    {
        for (int i = root.childCount - 1; i >= 0; i--)
            DestroyImmediate(root.GetChild(i).gameObject);
        _rowInfos.Clear();

        for (int row = 0; row < rows; row++)
        {
            // 行ごとのコンテナ
            var go = new GameObject($"row_{row}", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(root, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0.5f, 0.5f);

            bool goesLeft = (row % 2 == 0) ? firstRowGoesLeft : !firstRowGoesLeft;

            // 右へ流す行は、左に1周ぶん余分を置くため開始Xをずらす
            float startX = goesLeft ? 0f : -_loopWidth;
            rt.anchoredPosition = new Vector2(startX, -row * cellSize.y);

            for (int col = 0; col < _genCols; col++)
            {
                int index = (col + row) % _period;
                CreateCell(rt, col, sprites[index]);
            }

            _rowInfos.Add(new RowInfo { container = rt, goesLeft = goesLeft, startX = startX });
        }
    }

    void CreateCell(RectTransform parent, int col, Sprite sprite)
    {
        var go = new GameObject($"cell_{col}", typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = cellSize;
        rt.anchoredPosition = new Vector2(col * cellSize.x, 0);

        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        img.color = new Color(1f, 1f, 1f, alpha);   // 半透明
        img.preserveAspect = true;
    }

    void Update()
    {
        if (_period == 0) return;
        float delta = scrollSpeed * Time.deltaTime;

        foreach (var r in _rowInfos)
        {
            var p = r.container.anchoredPosition;

            if (r.goesLeft)
            {
                p.x -= delta;
                if (p.x <= r.startX - _loopWidth) p.x += _loopWidth;
            }
            else
            {
                p.x += delta;
                if (p.x >= r.startX + _loopWidth) p.x -= _loopWidth;
            }
            r.container.anchoredPosition = p;
        }
    }
}