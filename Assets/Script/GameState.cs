using System;
using System.Collections.Generic;
using UnityEngine;

public class GameState : MonoBehaviour
{
    public static GameState I { get; private set; }

    [Header("Progress")]
    public int floor = 1;
    public int step = 1;

    [Header("Talk")]
    public string pendingEventId;

    // 既存フィールドに追加
    [Header("Item Exchange")]
    // 交換フロー中に保持する拾ったアイテム。null = 交換中でない。
    [NonSerialized] public ItemData pendingItemData = null;

    [Header("Equipment")]
    // 装備中所持品の uid。空文字 = 未装備。
    // uid は所持品1個ごとに固有なので、同名武器が複数あっても正しく区別できる。
    public string equippedWeaponUid = "";

    private HashSet<string> played = new HashSet<string>();

    public bool IsPlayed(string eventId)
        => !string.IsNullOrEmpty(eventId) && played.Contains(eventId);

    public void MarkPlayed(string eventId)
    {
        if (!string.IsNullOrEmpty(eventId))
            played.Add(eventId);
    }

    private void Awake()
    {
        if (I != null) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);
    }
}