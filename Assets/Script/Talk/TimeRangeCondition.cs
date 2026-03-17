using UnityEngine;
using System;

[CreateAssetMenu(menuName = "Talk/Conditions/TimeRange")]
public class TimeRangeCondition : EventCondition
{
    [Range(0, 23)] public int startHour = 21;
    [Range(1, 24)] public int endHour = 24; // 24を許可したいのでRangeは工夫

    public override bool Evaluate(GameState gs)
    {
        int hour = DateTime.Now.Hour;

        // 通常（start < end）：例 21-24
        if (startHour < endHour)
            return hour >= startHour && hour < endHour;

        // 日跨ぎ（start > end）：例 22-5
        return hour >= startHour || hour < endHour;
    }
}