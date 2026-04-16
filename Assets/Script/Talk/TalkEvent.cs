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
    // 報酬アイテム（追加）
    // =========================================================
    //
    // イベント終了時にプレイヤーに付与するアイテム。
    // null の場合は報酬なし。
    // TalkRunner.Finish() で ItemBoxManager.Instance.AddItem() を通じて付与する。
    // 枠が満杯の場合はログ警告のみ（自動破棄はしない）。
    //
    // 使用例:
    //   7階のルーペ付与イベント → rewardItem に M008_Loupe をセット
    //   5階クリア報酬 → rewardItem に C001_Yakusou をセット
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