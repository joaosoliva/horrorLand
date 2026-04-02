using UnityEngine;

public abstract class UnlockCondition : ScriptableObject
{
    public abstract bool IsMet(RunGameState state);
}
