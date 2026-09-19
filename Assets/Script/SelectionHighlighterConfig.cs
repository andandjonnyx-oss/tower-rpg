using UnityEngine;

/// <summary>
/// SelectionHighlighter（自動生成の常駐オブジェクト）がシーンを介さずにアセットを
/// 参照するための設定。Resources/SelectionHighlighterConfig.asset として置く。
/// 画像そのものは Assets/Art 側に据え置き、ここから参照するだけ（Resources に複製しない）。
/// </summary>
public class SelectionHighlighterConfig : ScriptableObject
{
    [Tooltip("フォーカス中ボタンの左に置くナビ用ちびキャラ（実行時に左右反転して表示）")]
    public Sprite cursorSprite;
}
