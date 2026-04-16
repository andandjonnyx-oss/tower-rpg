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
}