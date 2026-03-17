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

    [Serializable]
    public class TalkLine
    {
        public string speaker;     // 任意
        [TextArea(2, 6)]
        public string text;

        public Sprite portrait;    // 任意
    }


}