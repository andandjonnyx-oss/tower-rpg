using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Items/ItemDatabase")]
public class ItemDatabase : ScriptableObject
{
    public List<ItemData> items = new();

    // 索引： (floor, step) -> 出現候補アイテム一覧
    private Dictionary<(int floor, int step), List<ItemData>> index;

    [Header("Index Settings")]
    [Min(1)] public int maxFloor = 100;
    [Min(1)] public int maxStepPerFloor = 20;

    public void BuildIndexIfNeeded()
    {
        if (index != null) return;

        index = new Dictionary<(int, int), List<ItemData>>(maxFloor * maxStepPerFloor);

        // 全地点に空リストを用意
        for (int f = 1; f <= maxFloor; f++)
        {
            for (int s = 1; s <= maxStepPerFloor; s++)
                index[(f, s)] = new List<ItemData>();
        }

        // 各アイテムの出現範囲を辞書に展開
        foreach (var item in items)
        {
            if (item == null) continue;

            NormalizeRange(item, out int minF, out int minS, out int maxF, out int maxS);

            // 範囲外の値を入力した際に範囲内に収める
            minF = Mathf.Clamp(minF, 1, maxFloor);
            maxF = Mathf.Clamp(maxF, 1, maxFloor);

            for (int f = minF; f <= maxF; f++)
            {
                int sFrom = (f == minF) ? minS : 1;
                int sTo = (f == maxF) ? maxS : maxStepPerFloor;

                sFrom = Mathf.Clamp(sFrom, 1, maxStepPerFloor);
                sTo = Mathf.Clamp(sTo, 1, maxStepPerFloor);

                for (int s = sFrom; s <= sTo; s++)
                    index[(f, s)].Add(item);
            }
        }
    }

    public IReadOnlyList<ItemData> FindCandidates(int floor, int step)
    {
        BuildIndexIfNeeded();

        floor = Mathf.Clamp(floor, 1, maxFloor);
        step = Mathf.Clamp(step, 1, maxStepPerFloor);

        return index.TryGetValue((floor, step), out var list) ? list : Array.Empty<ItemData>();
    }

    // 出現するアイテムが複数あった場合はランダムに1つ返す
    public ItemData GetRandomCandidate(int floor, int step)
    {
        var list = FindCandidates(floor, step);
        if (list.Count == 0) return null;
        return list[UnityEngine.Random.Range(0, list.Count)];
    }

    // 設定ミスで最小と最大がおかしくなっていた際にそれを修正する
    private void NormalizeRange(ItemData item, out int minF, out int minS, out int maxF, out int maxS)
    {
        minF = item.Minfloor;
        minS = item.Minstep;
        maxF = item.Maxfloor;
        maxS = item.Maxstep;

        if (ComparePos(minF, minS, maxF, maxS) > 0)
        {
            (minF, maxF) = (maxF, minF);
            (minS, maxS) = (maxS, minS);
        }
    }

    // (floor, step) の辞書順比較
    private int ComparePos(int f1, int s1, int f2, int s2)
    {
        if (f1 != f2) return f1.CompareTo(f2);
        return s1.CompareTo(s2);
    }

    // データ変更時に索引を作り直したい時に呼ぶ用
    public void InvalidateIndex()
    {
        index = null;
    }
}