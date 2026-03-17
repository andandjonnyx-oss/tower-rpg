using UnityEngine;

public abstract class EventCondition : ScriptableObject
{
    // ğŒ‚ª¬—§‚·‚é‚È‚ç true
    public abstract bool Evaluate(GameState gs);
}