using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// GP交換ショップの商品リストを保持する ScriptableObject。
/// Inspector で GpShopData のリストを設定する。
/// </summary>
[CreateAssetMenu(menuName = "GpShop/GpShopDatabase")]
public class GpShopDatabase : ScriptableObject
{
    [Tooltip("ショップに並ぶ商品リスト。sortOrder 順に表示される。")]
    public List<GpShopData> shopItems = new();

    /// <summary>
    /// 現在の到達階で表示可能な商品リストを返す。
    /// sortOrder 昇順でソート済み。
    /// </summary>
    public List<GpShopData> GetAvailableItems(int reachedFloor)
    {
        var result = new List<GpShopData>();

        foreach (var shopItem in shopItems)
        {
            if (shopItem == null || shopItem.item == null) continue;
            if (shopItem.requiredFloor > reachedFloor) continue;
            result.Add(shopItem);
        }

        result.Sort((a, b) => a.sortOrder.CompareTo(b.sortOrder));
        return result;
    }
}