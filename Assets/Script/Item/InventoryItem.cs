using System;

[Serializable]
public class InventoryItem
{
    // 所持品1個ごとの固有ID。取得時に GUID で発行する。
    public string uid;
    // マスターデータへの参照。
    public ItemData data;

    public InventoryItem(ItemData data)
    {
        this.uid = Guid.NewGuid().ToString();
        this.data = data;
    }
}