using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Battle/MonsterDatabase")]
public class MonsterDatabase : ScriptableObject
{
    public List<Monster> monsters = new();

    // 索引： (floor, step) -> 出現候補モンスター一覧
    // 特定するイベントと違い、ランダムに選ぶのであればID検索等は不要
    private Dictionary<(int floor, int step), List<Monster>> index;

    [Header("Index Settings")]
    [Min(1)] public int maxFloor = 100;
    [Min(1)] public int maxStepPerFloor = 20;

    public void BuildIndexIfNeeded()
    {
        if (index != null) return;

        index = new Dictionary<(int, int), List<Monster>>(maxFloor * maxStepPerFloor);

        // 全地点に空リストを用意（後で確実に引けるように）
        // 敵が登録されていないフロアでもエラーにならない
        for (int f = 1; f <= maxFloor; f++)
        {
            for (int s = 1; s <= maxStepPerFloor; s++)
                index[(f, s)] = new List<Monster>();
        }

        // 各モンスターの出現範囲を辞書に展開
        foreach (var m in monsters)
        {
            if (m == null) continue;

            NormalizeRange(m, out int minF, out int minS, out int maxF, out int maxS);

            // 範囲外の値を入力した際に範囲内に収める
            minF = Mathf.Clamp(minF, 1, maxFloor);
            maxF = Mathf.Clamp(maxF, 1, maxFloor);

            for (int f = minF; f <= maxF; f++)
            {

                // 出現範囲 (minFloor,minStep) ～ (maxFloor,maxStep) を展開する
                // 例：2F7STEP～4F5STEP
                // 2F : STEP7～20
                // 3F : STEP1～20
                // 4F : STEP1～5
                int sFrom = (f == minF) ? minS : 1;
                int sTo = (f == maxF) ? maxS : maxStepPerFloor;

                sFrom = Mathf.Clamp(sFrom, 1, maxStepPerFloor);
                sTo = Mathf.Clamp(sTo, 1, maxStepPerFloor);

                //その階の正しいSTEP範囲を辞書登録
                for (int s = sFrom; s <= sTo; s++)
                    index[(f, s)].Add(m);
            }
        }
    }

    public IReadOnlyList<Monster> FindCandidates(int floor, int step)
    {
        BuildIndexIfNeeded();

        floor = Mathf.Clamp(floor, 1, maxFloor);
        step = Mathf.Clamp(step, 1, maxStepPerFloor);

        return index.TryGetValue((floor, step), out var list) ? list : Array.Empty<Monster>();
    }

    /// <summary>
    /// 出現候補から Weight による重み付き抽選で1体を返す。
    ///
    /// 抽選方式:
    ///   全候補の Weight を合算し、0 ～ totalWeight の範囲で乱数を振る。
    ///   Weight が大きいほど選ばれやすい。
    ///
    /// 例: A(Weight=1), B(Weight=1), C(Weight=0.1) の場合
    ///   totalWeight = 2.1
    ///   A の確率 = 1/2.1 ≒ 47.6%
    ///   B の確率 = 1/2.1 ≒ 47.6%
    ///   C の確率 = 0.1/2.1 ≒ 4.8%
    ///
    /// Weight が 0 以下のモンスターは抽選対象外（スキップ）。
    /// 候補が0件、または全候補の Weight が 0 以下の場合は null を返す。
    /// </summary>
    public Monster GetRandomCandidate(int floor, int step)
    {
        var list = FindCandidates(floor, step);
        if (list.Count == 0) return null;

        // Weight の合計を計算
        float totalWeight = 0f;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] != null && list[i].Weight > 0f)
                totalWeight += list[i].Weight;
        }

        // 全候補の Weight が 0 以下の場合
        if (totalWeight <= 0f) return null;

        // 重み付き抽選
        float roll = UnityEngine.Random.Range(0f, totalWeight);
        float cumulative = 0f;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] == null || list[i].Weight <= 0f) continue;
            cumulative += list[i].Weight;
            if (roll < cumulative)
                return list[i];
        }

        // 浮動小数点の丸め誤差対策（通常ここには到達しない）
        return list[list.Count - 1];
    }

    //　設定ミスで最小と最大がおかしくなっていた際にそれを修正する
    private void NormalizeRange(Monster m, out int minF, out int minS, out int maxF, out int maxS)
    {
        minF = m.Minfloor;
        minS = m.Minstep;
        maxF = m.Maxfloor;
        maxS = m.Maxstep;

        // min > max だった場合に入れ替える（位置比較）
        if (ComparePos(minF, minS, maxF, maxS) > 0)
        {
            (minF, maxF) = (maxF, minF);
            (minS, maxS) = (maxS, minS);
        }
    }

    // (floor, step) の辞書順比較
    private int ComparePos(int f1, int s1, int f2, int s2)
    {
        //CompareTo 左＞右で1,=で0、左＜右で-1
        if (f1 != f2) return f1.CompareTo(f2);
        return s1.CompareTo(s2);
    }

    // データ変更時に索引を作り直したい時に呼ぶ用
    public void InvalidateIndex()
    {
        index = null;
    }
}