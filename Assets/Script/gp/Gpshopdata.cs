using UnityEngine;

/// <summary>
/// GP交換ショップの商品1件を定義する ScriptableObject。
/// Inspector で ItemData への参照、GP価格、解放条件などを設定する。
/// </summary>
[CreateAssetMenu(menuName = "GpShop/GpShopData")]
public class GpShopData : ScriptableObject
{
    [Header("商品情報")]
    [Tooltip("交換で入手できるアイテム。ItemData への参照。")]
    public ItemData item;

    [Tooltip("交換に必要なGP。")]
    [Min(1)]
    public int gpCost = 1;

    [Header("解放条件")]
    [Tooltip("この商品が表示されるために必要な到達階。\n"
           + "GameState.reachedFloor がこの値以上の場合に表示される。\n"
           + "0 なら常に表示。")]
    [Min(0)]
    public int requiredFloor = 0;

    [Header("購入制限")]
    [Tooltip("この商品の最大購入回数。0 なら無制限。\n"
           + "購入回数は GpShopView 側で管理する（将来的にセーブ対象にする場合あり）。")]
    [Min(0)]
    public int maxPurchaseCount = 0;

    [Header("表示順")]
    [Tooltip("ショップ内での表示順。小さいほど先に表示される。")]
    public int sortOrder = 0;
}