using UnityEngine;

[CreateAssetMenu(menuName = "HorrorLand/Notes/Conditions/Visited Area", fileName = "VisitedAreaCondition")]
public class VisitedAreaCondition : UnlockCondition
{
    public string areaId;

    public override bool IsMet(RunGameState state)
    {
        return state != null && !string.IsNullOrEmpty(areaId) && state.HasVisitedArea(areaId);
    }
}

[CreateAssetMenu(menuName = "HorrorLand/Notes/Conditions/Time Survived", fileName = "TimeSurvivedCondition")]
public class TimeSurvivedCondition : UnlockCondition
{
    [Min(0f)] public float requiredSeconds = 60f;

    public override bool IsMet(RunGameState state)
    {
        return state != null && state.TimeSurvived >= requiredSeconds;
    }
}

[CreateAssetMenu(menuName = "HorrorLand/Notes/Conditions/Triggered Event", fileName = "TriggeredEventCondition")]
public class TriggeredEventCondition : UnlockCondition
{
    public string eventId;

    public override bool IsMet(RunGameState state)
    {
        return state != null && !string.IsNullOrEmpty(eventId) && state.HasTriggeredEvent(eventId);
    }
}

[CreateAssetMenu(menuName = "HorrorLand/Notes/Conditions/Chase Occurred", fileName = "ChaseOccurredCondition")]
public class ChaseOccurredCondition : UnlockCondition
{
    public override bool IsMet(RunGameState state)
    {
        return state != null && state.WasChased;
    }
}
