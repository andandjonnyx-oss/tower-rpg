using System;
using System.Collections.Generic;

[Serializable]
public class InventoryItem
{
    // 所持品1個ごとの固有ID。取得時に GUID で発行する。
    public string uid;
    // マスターデータへの参照。
    public ItemData data;

    public InventoryItem(ItemData data)
    {
        //GUID ランダムな文字列　例："7c9ec9ed-c93a-4d4c-83f3-4a93cc8c767d"
        this.uid = Guid.NewGuid().ToString();
        this.data = data;
    }

    // key = skillId, value = 残りクールタイムターン数
    public Dictionary<string, int> skillCooldowns = new();
}