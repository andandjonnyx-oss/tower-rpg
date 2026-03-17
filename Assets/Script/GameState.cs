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
    // 装備中武器スロットの GetInstanceID() を保持。
    // ScriptableObject 参照ではなくインスタンスIDで管理することで
    // 同名武器を複数持っていても1スロットだけ光る。
    // "itemId:index" 形式。空文字 = 未装備。シーン再ロードでも消えない。
    public string equippedSlotKey = "";

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