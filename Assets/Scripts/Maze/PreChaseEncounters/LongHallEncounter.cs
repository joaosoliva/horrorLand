using System.Collections;
using UnityEngine;

public class LongHallEncounter : EncounterBase
{
    [Header("Long-Hall Tuning")]
    [SerializeField] private int minHallCells = 6;
    [SerializeField] private float sprintSpeed = 18f;
    [SerializeField] private float jumpscareTriggerDistance = 2.8f;
    [SerializeField] private float maxSprintDuration = 1.8f;
    [SerializeField] private bool debugLogs;

    protected override bool CanTrigger(EncounterContext context)
    {
        if (Player == null || Villain == null || context.PlayerSeesVillain)
        {
            return false;
        }

        if (!context.MazeContext.IsValid)
        {
            Log("LongHall rejected: maze context invalid.");
            return false;
        }

        if (!context.MazeContext.IsLongHallAhead || context.MazeContext.StraightCellsAhead < minHallCells)
        {
            Log($"LongHall rejected: only {context.MazeContext.StraightCellsAhead} cells ahead, required {minHallCells}.");
            return false;
        }

        Log($"LongHall accepted: {context.MazeContext.StraightCellsAhead} valid forward cells.");
        return true;
    }

    protected override IEnumerator Execute(EncounterContext context)
    {
        if (Player == null || Villain == null)
        {
            yield break;
        }

        Vector3 spawnPoint = context.MazeContext.SuggestedHallSpawnPoint;
        Villain.PushExternalControl();
        Villain.TeleportToPosition(spawnPoint, true);

        float started = Time.time;
        while (Time.time - started < maxSprintDuration)
        {
            Vector3 toPlayer = Player.position - Villain.transform.position;
            toPlayer.y = 0f;
            float step = sprintSpeed * Time.deltaTime;
            if (toPlayer.magnitude <= jumpscareTriggerDistance)
            {
                TriggerAndVanish();
                Villain.PopExternalControl();
                yield break;
            }

            if (toPlayer.sqrMagnitude > 0.001f)
            {
                Villain.transform.position += toPlayer.normalized * step;
                Villain.transform.rotation = Quaternion.LookRotation(toPlayer.normalized);
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

        Villain.ForceDisappearFromPlayer("Long-hall encounter completed");
    }

    private void Log(string message)
    {
        if (debugLogs)
        {
            Debug.Log($"[LongHallEncounter] {message}");
        }
    }
}
