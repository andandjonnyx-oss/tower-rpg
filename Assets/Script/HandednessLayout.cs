using UnityEngine;

/// <summary>
/// Tower シーン専用の利き手レイアウト切替。
/// 左利き設定（GameSettings.IsLeftHanded）のとき、登録した各UIに
/// 個別指定のオフセット（leftHandedOffset）を加算して位置をずらす。
///
/// 「画面中心基準の自動反転」ではなく要素ごとに手動オフセットを与える方式。
/// 理由：魔法ボタン群は階表示より横に長く、単純反転すると HP/MP と重なるため、
///       実シーンを見ながら要素単位で微調整したい。
///
/// 使い方：
///   1. Tower シーンの Canvas 直下に空 GameObject を作り本コンポーネントを付与。
///   2. targets に「左利き時に動かす UI の RectTransform」と
///      その時のオフセット(leftHandedOffset)を1件ずつ登録。
///   3. 右利き（既定）では何も動かさない＝デザイン時の配置のまま。
///   4. 左利きに設定して Play し、Scene を見ながら各 leftHandedOffset を調整。
///
/// 安全設計：
///   - 対象が null でも例外を出さずスキップ（アサイン漏れがあっても進行可能）。
///   - 右利き時は anchoredPosition を元値のまま再代入するだけ（実質 no-op）。
///   - 座標加算のみで可逆（ResetToOriginal で完全復元）。
///   - anchoredPosition 以外（active/scale/rotation/リスナー等）は一切変更しない。
/// </summary>
[DefaultExecutionOrder(-50)]
public class HandednessLayout : MonoBehaviour
{
    [System.Serializable]
    public class Entry
    {
        [Tooltip("左利き時に動かす対象の RectTransform")]
        public RectTransform target;

        [Tooltip("左利き時に加算するオフセット（右利き位置からの相対量、px）。\n"
               + "左へ動かすなら X 負、右へ動かすなら X 正。")]
        public Vector2 leftHandedOffset;

        [Tooltip("任意メモ（例：進むボタン／魔法ボタン群）。挙動には影響しない。")]
        public string note;

        [System.NonSerialized] public Vector2 originalAnchoredPos;
        [System.NonSerialized] public bool captured;
    }

    [Tooltip("左利き時に位置をずらす UI の一覧。右利き（既定）では何も動かさない。")]
    [SerializeField] private Entry[] targets;

    private bool applied = false;

    private void Awake()
    {
        Apply();
    }

    /// <summary>現在の利き手設定に応じて適用する。</summary>
    public void Apply()
    {
        if (applied) return; // 二重適用防止
        if (targets == null) return;

        bool left = GameSettings.IsLeftHanded;

        foreach (var e in targets)
        {
            if (e == null || e.target == null) continue;

            if (!e.captured)
            {
                e.originalAnchoredPos = e.target.anchoredPosition;
                e.captured = true;
            }

            e.target.anchoredPosition = left
                ? e.originalAnchoredPos + e.leftHandedOffset
                : e.originalAnchoredPos;
        }

        applied = true;
    }

    /// <summary>元の（右利き）位置へ戻す。</summary>
    public void ResetToOriginal()
    {
        if (targets == null) return;
        foreach (var e in targets)
        {
            if (e == null || e.target == null || !e.captured) continue;
            e.target.anchoredPosition = e.originalAnchoredPos;
        }
        applied = false;
    }
}