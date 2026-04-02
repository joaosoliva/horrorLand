using UnityEngine;

[CreateAssetMenu(menuName = "HorrorLand/Endings/Conditions/Has Tag", fileName = "EndingHasTagCondition")]
public class EndingHasTagCondition : EndingCondition
{
    public string requiredTag;

    public override bool IsMet(RunGameState state)
    {
        return state != null && !string.IsNullOrEmpty(requiredTag) && state.HasCollectedTag(requiredTag);
    }
}

[CreateAssetMenu(menuName = "HorrorLand/Endings/Conditions/Was Chased", fileName = "EndingWasChasedCondition")]
public class EndingWasChasedCondition : EndingCondition
{
    public bool expectedValue = true;

    public override bool IsMet(RunGameState state)
    {
        return state != null && state.WasChased == expectedValue;
    }
}

[CreateAssetMenu(menuName = "HorrorLand/Endings/Conditions/Entered Secret Room", fileName = "EndingEnteredSecretRoomCondition")]
public class EndingEnteredSecretRoomCondition : EndingCondition
{
    public bool expectedValue = true;

    public override bool IsMet(RunGameState state)
    {
        return state != null && state.EnteredSecretRoom == expectedValue;
    }
}

[CreateAssetMenu(menuName = "HorrorLand/Endings/Conditions/Sanity Threshold", fileName = "EndingSanityThresholdCondition")]
public class EndingSanityThresholdCondition : EndingCondition
{
    public float minimumSanity = 0f;
    public float maximumSanity = 100f;

    public override bool IsMet(RunGameState state)
    {
        if (state == null) return false;
        return state.CurrentSanity >= minimumSanity && state.CurrentSanity <= maximumSanity;
    }
}
