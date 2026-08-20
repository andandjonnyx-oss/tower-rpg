using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 図鑑のシーン間データ受け渡し用コンテキスト。
/// BattleContext と同じパターン。
/// ZukanM → Mstatus（モンスター図鑑）、ZukanT → Talk → ZukanT（会話図鑑）の
/// シーン遷移時にデータを受け渡す。
/// </summary>
public static class ZukanContext
{
    // =========================================================
    // モンスター図鑑（ZukanM / Mstatus）
    // =========================================================

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

    // =========================================================
    // 会話図鑑（ZukanT / Talk）
    // =========================================================
    //
    // 会話図鑑は ZukanT(一覧) → Talk(会話再生) → ZukanT(戻る) の流れ。
    // 会話を見た後に ZukanT へ戻った際、直前に開いた会話セルの位置へ
    // スクロールを復元する。図鑑トップ(Zukan)から入った時のみ先頭表示。
    //
    // モンスター図鑑の ReturningFromDetail / ReturnTargetMonster と同じパターンだが、
    // 会話図鑑はシーンをまたぐ（Talk を経由する）ため、参照ではなく
    // 文字列 id で復元対象を保持する。
    // =========================================================

    /// <summary>
    /// 会話再生(Talk)から会話図鑑(ZukanT)へ戻る最中かどうか。
    /// true の場合、ZukanT は TalkReturnTargetId のセルを画面内に収めるよう
    /// スクロール位置を復元する。トップ(Zukan)から来た場合は false で先頭表示。
    /// 一度使用したら ZukanT 側でクリアする。
    /// </summary>
    public static bool TalkReturningFromDetail;

    /// <summary>
    /// 会話再生から戻る際、一覧で画面内に収めたい会話イベントの id。
    /// </summary>
    public static string TalkReturnTargetId;
}