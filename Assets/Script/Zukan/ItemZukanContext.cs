using System.Collections.Generic;

/// <summary>
/// アイテム図鑑のシーン間データ受け渡し用コンテキスト。
/// ZukanContext（モンスター図鑑）と同じパターン。
///
/// ZukanI → Istatus: 選択アイテムと、↑↓移動用の閲覧可能リストを渡す。
/// Istatus → ZukanI: 戻り時にジャンルタブとスクロール位置を復元するための情報を渡す。
/// </summary>
public static class ItemZukanContext
{
    // --- ZukanI → Istatus ---

    /// <summary>詳細画面で表示するアイテム。</summary>
    public static ItemData SelectedItem;

    /// <summary>
    /// ↑↓移動用の、発見済みアイテムの順序付きリスト。
    /// 選択中の大ジャンル内を小ジャンル順→アイテム順に並べ、発見済みだけに絞ったもの。
    /// </summary>
    public static List<ItemData> DiscoveredList;

    /// <summary>DiscoveredList 内の現在インデックス。</summary>
    public static int CurrentIndex;

    /// <summary>選択中の大ジャンルインデックス（戻り時のタブ復元に使う）。</summary>
    public static int CurrentMajorIndex;

    // --- Istatus → ZukanI（戻り復元） ---

    /// <summary>
    /// 詳細から一覧へ戻る最中かどうか。
    /// true の場合、ZukanI は ReturnMajorIndex のタブを開き、
    /// ReturnTargetItem を画面内に収めるようスクロールを復元する。
    /// 一度使用したら ZukanI 側でクリアする。
    /// </summary>
    public static bool ReturningFromDetail;

    /// <summary>戻り時に開く大ジャンルタブのインデックス。</summary>
    public static int ReturnMajorIndex;

    /// <summary>戻り時に画面内へ収めたいアイテム。</summary>
    public static ItemData ReturnTargetItem;
}