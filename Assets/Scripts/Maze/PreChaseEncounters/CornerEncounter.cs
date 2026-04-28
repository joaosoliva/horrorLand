using System.Collections;
using UnityEngine;

public class CornerEncounter : EncounterBase
{
    [Header("Corner Encounter Tuning")]
    [SerializeField] private float maxWaitForTurn = 1.5f;
    [SerializeField] private bool debugLogs;

    protected override bool CanTrigger(EncounterContext context)
    {
        if (Player == null || Villain == null || context.PlayerSeesVillain)
        {
            return false;
        }

        if (!context.MazeContext.IsValid)
        {
            Log("Corner rejected: maze context invalid.");
            return false;
        }

        if (!context.MazeContext.IsCornerAhead)
        {
            Log("Corner rejected: no connected perpendicular corridor.");
            return false;
        }

        string dir = context.MazeContext.CornerTurnDirection == Vector2Int.right ? "right" : "left";
        int cellsAhead = Mathf.Abs(context.MazeContext.CornerCell.x - context.MazeContext.CurrentCell.x) + Mathf.Abs(context.MazeContext.CornerCell.y - context.MazeContext.CurrentCell.y);
        Log($"Corner accepted: {dir} turn detected {Mathf.Max(1, cellsAhead)} cells ahead.");
        return true;
    }

    protected override IEnumerator Execute(EncounterContext context)
    {
        if (Player == null || Villain == null)
        {
            yield break;
        }

        Vector3 cornerSpot = context.MazeContext.SuggestedCornerRevealPoint;
        Villain.PushExternalControl();
        Villain.TeleportToPosition(cornerSpot, true);

        float started = Time.time;
        while (Time.time - started < maxWaitForTurn)
        {
            if (Villain.IsVillainVisibleToPlayer())
            {
                TriggerAndVanish();
                Villain.PopExternalControl();
                yield break;
            }

            yield return null;
        }

        TriggerAndVanish();
        Villain.PopExternalControl();
    }

    public override void ForceStop()
    {
        if (Villain != null && Villain.IsExternallyControlled)
        {
            Villain.PopExternalControl();
        }
    }

    private void TriggerAndVanish()
    {
        if (Manager != null && Manager.JumpscareSystem != null && !Manager.JumpscareSystem.IsJumpscareActive())
        {
            Manager.JumpscareSystem.ForceMajorScare(false);
        }

        Villain.ForceDisappearFromPlayer("Corner encounter completed");
    }

    private void Log(string message)
    {
        if (debugLogs)
        {
            Debug.Log($"[CornerEncounter] {message}");
        }
    }
}
