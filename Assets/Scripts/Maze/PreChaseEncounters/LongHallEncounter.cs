using System.Collections;
using UnityEngine;

public class LongHallEncounter : EncounterBase
{
    [Header("Long-Hall Tuning")]
    [SerializeField] private float minHallLength = 11f;
    [SerializeField] private float spawnAheadPadding = 2f;
    [SerializeField] private float sprintSpeed = 18f;
    [SerializeField] private float jumpscareTriggerDistance = 2.8f;
    [SerializeField] private float maxSprintDuration = 1.8f;
    [SerializeField] private LayerMask obstacleMask = ~0;

    protected override bool CanTrigger(EncounterContext context)
    {
        if (Player == null || Villain == null || context.PlayerSeesVillain)
        {
            return false;
        }

        return TryFindHallSpawnPoint(out _);
    }

    protected override IEnumerator Execute(EncounterContext context)
    {
        if (Player == null || Villain == null || !TryFindHallSpawnPoint(out Vector3 spawnPoint))
        {
            yield break;
        }

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

    private bool TryFindHallSpawnPoint(out Vector3 spawnPoint)
    {
        Vector3 start = Player.position + Vector3.up;
        Vector3 forward = Player.forward;
        forward.y = 0f;
        forward.Normalize();

        if (Physics.Raycast(start, forward, out RaycastHit hit, minHallLength + spawnAheadPadding, obstacleMask, QueryTriggerInteraction.Ignore))
        {
            float clearDistance = hit.distance;
            if (clearDistance >= minHallLength)
            {
                spawnPoint = Player.position + forward * Mathf.Max(3f, clearDistance - spawnAheadPadding);
                spawnPoint.y = 0f;
                return true;
            }
        }

        spawnPoint = Vector3.zero;
        return false;
    }

    private void TriggerAndVanish()
    {
        if (Manager != null && Manager.JumpscareSystem != null && !Manager.JumpscareSystem.IsJumpscareActive())
        {
            Manager.JumpscareSystem.ForceMajorScare(false);
        }

        Villain.ForceDisappearFromPlayer("Long-hall encounter completed");
    }
}
