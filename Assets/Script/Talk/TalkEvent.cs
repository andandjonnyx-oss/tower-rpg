using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Talk/TalkEvent")]
public class TalkEvent : ScriptableObject
{
    [Header("Identity")]
    public string id; // 一意（手入力推奨。例: "F01_S03_Intro"）

    [Header("Trigger Condition")]
    public int floor;
    public int step;

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
    }


}