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
    }


}