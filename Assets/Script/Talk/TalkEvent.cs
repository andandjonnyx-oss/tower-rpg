using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Talk/TalkEvent")]
public class TalkEvent : ScriptableObject
{
    [Header("Identity")]
    public string id; // 一意（手入力推奨。例: "F01_S03_Intro"）

    [Tooltip("図鑑に表示するタイトル。未設定の場合は id がフォールバック表示される。")]
    public string zukanTitle;

    [Header("Trigger Condition")]
    public int floor;
    public int step;

    [Header("Background")]
    [Tooltip("このイベント全体のデフォルト背景画像。\n"
           + "null の場合はシーンのデフォルト背景がそのまま使われる。\n"
           + "各 TalkLine の backgroundOverride が設定されていればそちらが優先される。")]
    public Sprite backgroundImage;

    [Header("Content")]
    public List<TalkLine> lines = new();

    //その他の条件フラグ（任意に追加）
    [Header("Conditions (ALL must be true)")]
    public List<EventCondition> conditions = new(); // 追加

    // =========================================================
    // 確率分岐グループ（追加）
    // =========================================================
    //
    // 同じ floor/step に複数のイベントを登録し、確率で1つだけ発生させる仕組み。
    //
    // 使い方:
    //   1. 排他的なイベント群に同じ randomGroup 名を設定（例: "F77_lottery"）
    //   2. 各イベントに randomWeight を設定（合計100にする必要はない。比率で按分される）
    //   3. TowerEventTrigger が同じ randomGroup のイベントを集め、重み付き抽選で1つ選ぶ
    //   4. 当選イベントを再生し、exclusiveIds に列挙された他のイベントもまとめて MarkPlayed
    //
    // randomGroup が空の場合は従来通りの動作（確率判定なし）。
    // =========================================================

    [Header("Random Group")]
    [Tooltip("確率分岐グループ名。同じ名前のイベント群から重み付き抽選で1つだけ発生する。\n"
           + "空の場合は確率分岐なし（従来互換）。")]
    public string randomGroup;

    [Tooltip("抽選の重み。同グループ内の全 randomWeight の合計に対する比率で当選確率が決まる。\n"
           + "例: 7.77 / 70 / 22.23 → 合計100 → それぞれ 7.77% / 70% / 22.23%")]
    public float randomWeight = 0f;

    [Tooltip("このイベントが発生した場合にまとめて再生済みにするイベントIDのリスト。\n"
           + "確率分岐の他の分岐を消す用途。\n"
           + "ここに列挙されたIDは MarkPlayed されるが、図鑑には個別に登録される。")]
    public string[] exclusiveIds;

    // =========================================================
    // 報酬アイテム（追加）
    // =========================================================

    [Header("Reward")]
    [Tooltip("イベント終了時にプレイヤーに付与するアイテム（null=報酬なし）")]
    public ItemData rewardItem;

    // =========================================================
    // 所持アイテムによる分岐（第33回追加）
    // =========================================================
    //
    // インベントリに特定アイテムを持っているかどうかでイベント発動を分岐させる。
    // F85_S02 のような「猫アイテムを持っているか」で会話パターンを変える用途。
    //
    // 使い方:
    //   1. 同じ floor/step に2つのイベントを作成（例: F85_S02_neko_have / F85_S02_neko_none）
    //   2. 両方に同じ requiredItem（例: M045_neko）を設定
    //   3. _have 側は itemPossessionMode = HasItem
    //      _none 側は itemPossessionMode = NotHasItem
    //   4. 排他制御は randomGroup + exclusiveIds で行う（α案）。
    //      同じ randomGroup を設定し、互いを exclusiveIds で参照することで、
    //      どちらか一方を再生したら両方が MarkPlayed される。
    //
    // 判定対象はインベントリ（ItemBoxManager.Instance.GetItems()）のみ。
    // 倉庫の中身は判定対象外（倉庫預け = 未所持扱い）。
    // 判定は itemId 文字列比較で行うため、ScriptableObject の参照が壊れても安全。
    //
    // requiredItem が null の場合は所持判定なし（従来互換）。
    // =========================================================

    [Header("Item Possession Branch")]
    [Tooltip("インベントリ所持判定に使うアイテム。\n"
           + "null の場合は判定なし（従来互換）。\n"
           + "倉庫の中身は判定対象外（インベントリのみ）。\n"
           + "判定は itemId 文字列で行う。")]
    public ItemData requiredItem;

    [Tooltip("requiredItem の判定モード。\n"
           + "HasItem    = インベントリに所持していれば発動条件を満たす\n"
           + "NotHasItem = インベントリに所持していなければ発動条件を満たす（倉庫預け含む）")]
    public ItemPossessionMode itemPossessionMode = ItemPossessionMode.HasItem;

    [Serializable]
    public class TalkLine
    {
        public string speaker;     // 任意
        [TextArea(2, 6)]
        public string text;

        public Sprite portrait;    // 任意

        [Tooltip("この台詞で背景を変更する場合に設定。\n"
               + "null の場合は TalkEvent.backgroundImage が使われる。")]
        public Sprite backgroundOverride; // 台詞単位の背景オーバーライド

        [Tooltip("この台詞を表示する前に名前入力ポップアップを出す。\n"
       + "入力された名前は GameState.playerName に保存され、\n"
       + "以降の台詞内の {name} が置換される（この台詞自身も含む）。\n"
       + "図鑑リプレイ時はポップアップをスキップし、保存済みの名前を使う。")]
        public bool requestNameInput = false;
    }
}

// =========================================================
// 所持アイテム判定モード（第33回追加）
// =========================================================
public enum ItemPossessionMode
{
    /// <summary>requiredItem をインベントリに所持していれば true</summary>
    HasItem = 0,

    /// <summary>requiredItem をインベントリに所持していなければ true（倉庫預け含む）</summary>
    NotHasItem = 1,
}