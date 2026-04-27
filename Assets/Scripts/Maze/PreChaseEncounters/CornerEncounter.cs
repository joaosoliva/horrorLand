using System.Collections;
using UnityEngine;

public class CornerEncounter : EncounterBase
{
    [Header("Corner Encounter Tuning")]
    [SerializeField] private float cornerProbeDistance = 2.2f;
    [SerializeField] private float cornerSideOffset = 2.5f;
    [SerializeField] private float maxWaitForTurn = 1.5f;
    [SerializeField] private LayerMask obstacleMask = ~0;

    protected override bool CanTrigger(EncounterContext context)
    {
        if (Player == null || Villain == null || context.PlayerSeesVillain)
        {
            return false;
        }

        return TryFindCornerPosition(out _);
    }

    protected override IEnumerator Execute(EncounterContext context)
    {
        if (Player == null || Villain == null || !TryFindCornerPosition(out Vector3 cornerSpot))
        {
            yield break;
        }

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

    private bool TryFindCornerPosition(out Vector3 cornerPosition)
    {
        Vector3 origin = Player.position + Vector3.up;
        Vector3 forward = Player.forward;
        forward.y = 0f;
        forward.Normalize();

        bool frontBlocked = Physics.Raycast(origin, forward, cornerProbeDistance, obstacleMask, QueryTriggerInteraction.Ignore);
        if (!frontBlocked)
        {
            cornerPosition = Vector3.zero;
            return false;
        }

        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        Vector3 left = -right;

        Vector3[] sideDirs = Random.value < 0.5f ? new[] { left, right } : new[] { right, left };
        for (int i = 0; i < sideDirs.Length; i++)
        {
            Vector3 probe = Player.position + sideDirs[i] * cornerSideOffset;
            probe.y = 0f;
            bool hiddenFromPlayer = !Villain.HasLineOfSightBetween(Player.position, probe);
            if (hiddenFromPlayer)
            {
                cornerPosition = probe;
                return true;
            }
        }

        cornerPosition = Vector3.zero;
        return false;
    }

    private void TriggerAndVanish()
    {
        if (Manager != null && Manager.JumpscareSystem != null && !Manager.JumpscareSystem.IsJumpscareActive())
        {
            Manager.JumpscareSystem.ForceMajorScare(false);
        }

        Villain.ForceDisappearFromPlayer("Corner encounter completed");
    }
}
