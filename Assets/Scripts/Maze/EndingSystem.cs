using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class EndingSystem : MonoBehaviour
{
    [Header("Ending Library")]
    [Tooltip("Add exactly 3 endings for this game's current design.")]
    public List<EndingData> endings = new List<EndingData>();

    public EndingData ResolveEnding(RunGameState state)
    {
        if (state == null || endings == null || endings.Count == 0)
        {
            return null;
        }

        List<EndingData> ordered = endings
            .Where(e => e != null)
            .OrderByDescending(e => e.priority)
            .ToList();

        for (int i = 0; i < ordered.Count; i++)
        {
            if (IsEndingValid(ordered[i], state))
            {
                return ordered[i];
            }
        }

        return null;
    }

    private bool IsEndingValid(EndingData ending, RunGameState state)
    {
        if (ending == null)
        {
            return false;
        }

        for (int i = 0; i < ending.conditions.Count; i++)
        {
            EndingCondition condition = ending.conditions[i];
            if (condition != null && !condition.IsMet(state))
            {
                return false;
            }
        }

        return true;
    }
}
