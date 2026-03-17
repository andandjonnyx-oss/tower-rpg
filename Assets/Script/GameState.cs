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

    // çƒê∂çœÇ›ÅiëÂó Ç≈Ç‡çÇë¨Åj
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
        if (I != null)
        {
            Destroy(gameObject);
            return;
        }
        I = this;
        DontDestroyOnLoad(gameObject);
    }
}