using System.Collections.Generic;
using UnityEngine.UI;

/// <summary>
/// コントローラー/キーボードの明示ナビゲーション配線を組む共有ユーティリティ。
/// 各シーンの巡回ループ配線はこのヘルパーに集約する（同じループ配線を
/// あちこちに手書きしないため）。
///
/// ※ Battle / Title は導入初期に同等ロジックを各自インラインで持っている。
///   それらは動作確認済みのため無理に置き換えず、以降の新規配線はこの
///   ヘルパーを使う。
/// </summary>
public static class ControllerNav
{
    /// <summary>
    /// 渡した順序で十字キーの縦ループ（上下）を明示配線する。
    /// 先頭↑で末尾へ、末尾↓で先頭へ回る。左右は割り当てない。
    /// null や1件以下のリストは何もしない（安全）。
    /// </summary>
    public static void WireVerticalLoop(IList<Selectable> loop)
    {
        if (loop == null || loop.Count == 0) return;

        if (loop.Count == 1)
        {
            // 1件だけなら自分自身に向ける（暴発防止で全方向自分）
            var only = loop[0];
            if (only == null) return;
            only.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = only,
                selectOnDown = only,
            };
            return;
        }

        int n = loop.Count;
        for (int i = 0; i < n; i++)
        {
            var s = loop[i];
            if (s == null) continue;
            s.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = loop[(i - 1 + n) % n],
                selectOnDown = loop[(i + 1) % n],
                // 左右は割り当てない（縦ループのみ）
            };
        }
    }

    /// <summary>
    /// 渡した順序で横ループ（左右）を明示配線する。左端←で末尾、右端→で先頭へ回る。
    /// 上下は割り当てない。null や1件以下は安全に無視。
    /// </summary>
    public static void WireHorizontalLoop(IList<Selectable> loop)
    {
        if (loop == null || loop.Count == 0) return;
        if (loop.Count == 1)
        {
            var only = loop[0];
            if (only == null) return;
            only.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnLeft = only,
                selectOnRight = only,
            };
            return;
        }
        int n = loop.Count;
        for (int i = 0; i < n; i++)
        {
            var s = loop[i];
            if (s == null) continue;
            s.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnLeft = loop[(i - 1 + n) % n],
                selectOnRight = loop[(i + 1) % n],
            };
        }
    }

    /// <summary>指定 Selectable を十字キーのナビゲーション対象から外す。</summary>
    public static void SetNavigationNone(Selectable s)
    {
        if (s == null) return;
        var nav = s.navigation;
        nav.mode = Navigation.Mode.None;
        s.navigation = nav;
    }

    /// <summary>
    /// 上下左右を明示指定して配線する（各方向 null = その方向へ移動しない）。
    /// グリッド状レイアウトの配線に使う。
    /// </summary>
    public static void SetExplicit(Selectable s, Selectable up, Selectable down,
                                   Selectable left, Selectable right)
    {
        if (s == null) return;
        s.navigation = new Navigation
        {
            mode = Navigation.Mode.Explicit,
            selectOnUp = up,
            selectOnDown = down,
            selectOnLeft = left,
            selectOnRight = right,
        };
    }
}
