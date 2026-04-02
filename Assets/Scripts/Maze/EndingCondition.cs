using UnityEngine;

public abstract class EndingCondition : ScriptableObject
{
    public abstract bool IsMet(RunGameState state);
}
