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

    [Header("Equipment")]
    // ScriptableObject 参照で保持。SortItems 後も参照は変わらないのでズレない。
    // 同名武器の識別は IndexOf で行う（下記 ItemSlotView 参照）。
    public ItemData equippedWeapon = null;

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