using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Talk/TalkEventDatabase")]
public class TalkEventDatabase : ScriptableObject
{
    public List<TalkEvent> events = new();

    // 初回アクセス時に作る索引の定義（floor/step -> list）
    private Dictionary<(int floor, int step), List<TalkEvent>> index;
    private Dictionary<string, TalkEvent> byId;

    // 辞書を作成する関数（基本1度のみ機能する）
    public void BuildIndexIfNeeded()
    {
        if (index != null && byId != null) return;

        index = new Dictionary<(int, int), List<TalkEvent>>();
        byId = new Dictionary<string, TalkEvent>();

        foreach (var e in events)
        {

            // 空白ならスキップし次のループへ
            if (e == null) continue;

            // id辞書 無ければ追加
            if (!string.IsNullOrEmpty(e.id) && !byId.ContainsKey(e.id))
                byId.Add(e.id, e);

            // 条件辞書　無ければ追加　あればそのリストの参照を所得
            var key = (e.floor, e.step);
            if (!index.TryGetValue(key, out var list))
            {
                list = new List<TalkEvent>();
                index.Add(key, list);
            }
            // 条件リストにイベントを追加
            list.Add(e);
        }
    }
    // 
    public IReadOnlyList<TalkEvent> FindByCondition(int floor, int step)
    {
        BuildIndexIfNeeded();
        //辞書にあればリストを返す、無ければ空の配列を返す（このように記載が無くても読み取り専用）
        return index.TryGetValue((floor, step), out var list) ? list : (IReadOnlyList<TalkEvent>)System.Array.Empty<TalkEvent>();
    }

    public TalkEvent FindById(string id)
    {
        BuildIndexIfNeeded();
        // IDが存在し、辞書にあれば一致したイベントを返す、無ければnullを返す
        return (!string.IsNullOrEmpty(id) && byId.TryGetValue(id, out var e)) ? e : null;
    }

    public void InvalidateIndex()
    {
        index = null;
        byId = null;
    }
}