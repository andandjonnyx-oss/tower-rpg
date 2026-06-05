using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// モンスター図鑑のシーン間データ受け渡し用コンテキスト。
/// BattleContext と同じパターン。
/// ZukanM → Mstatus のシーン遷移時にモンスターを受け渡す。
/// </summary>
public static class ZukanContext
{
    /// <summary>図鑑詳細画面で表示するモンスター。</summary>
    public static Monster SelectedMonster;

    /// <summary>
    /// 閲覧可能（遭遇済み）モンスターの順序付きリスト。
    /// Mstatus で↑↓ボタンによるモンスター切替に使用。
    /// </summary>
    public static List<Monster> EncounteredList;

    /// <summary>
    /// EncounteredList 内の現在のインデックス。
    /// </summary>
    public static int CurrentIndex;

    /// <summary>
    /// 詳細画面(Mstatus)から一覧(ZukanM)へ戻る最中かどうか。
    /// true の場合、ZukanM は ReturnTargetMonster を画面内に収めるよう
    /// スクロール位置を復元する。トップ(Zukan)から来た場合は false で先頭表示。
    /// 一度使用したら ZukanM 側でクリアする。
    /// </summary>
    public static bool ReturningFromDetail;

    /// <summary>
    /// 詳細から戻る際、一覧で画面内に収めたいモンスター。
    /// 通常/ボスどちらのタブを開くかもこのモンスターの IsBoss から判定する。
    /// </summary>
    public static Monster ReturnTargetMonster;
}
