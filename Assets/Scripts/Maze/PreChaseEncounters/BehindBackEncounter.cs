using System.Collections;
using UnityEngine;

public class BehindBackEncounter : EncounterBase
{
    [Header("Behind-Back Tuning")]
    [SerializeField] private float spawnDistance = 5.5f;
    [SerializeField] private float sideOffset = 1.5f;
    [SerializeField] private float maxWaitForLook = 2.5f;

    protected override bool CanTrigger(EncounterContext context)
    {
        if (Player == null || Villain == null || context.PlayerSeesVillain)
        {
            return false;
        }

        if (!context.MazeContext.IsValid || context.MazeContext.IsInitialRoom || context.MazeContext.IsSafeZone)
        {
            return false;
        }

        Vector3 spawnPoint = ComputeSpawnPoint();
        return !Villain.HasLineOfSightBetween(Player.position, spawnPoint);
    }

    protected override IEnumerator Execute(EncounterContext context)
    {
        if (Player == null || Villain == null)
        {
            yield break;
        }

        Villain.PushExternalControl();
        Vector3 spawnPoint = ComputeSpawnPoint();
        Villain.TeleportToPosition(spawnPoint, true);

        float started = Time.time;
        while (Time.time - started < maxWaitForLook)
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

    private Vector3 ComputeSpawnPoint()
    {
        Vector3 backwards = -Player.forward;
        backwards.y = 0f;
        backwards.Normalize();

        Vector3 lateral = Vector3.Cross(Vector3.up, backwards) * (Random.value < 0.5f ? -1f : 1f);
        Vector3 candidate = Player.position + backwards * spawnDistance + lateral.normalized * sideOffset;
        candidate.y = 0f;
        return candidate;
    }

    private void TriggerAndVanish()
    {
        if (Manager != null && Manager.JumpscareSystem != null && !Manager.JumpscareSystem.IsJumpscareActive())
        {
            Manager.JumpscareSystem.ForceMajorScare(false);
        }

        Villain.ForceDisappearFromPlayer("Behind-back encounter completed");
    }
}
