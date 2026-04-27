using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EncounterManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private VillainAI villain;
    [SerializeField] private Transform player;
    [SerializeField] private ChaseSystem chaseSystem;
    [SerializeField] private JumpscareSystem jumpscareSystem;
    [SerializeField] private List<EncounterBase> encounters = new List<EncounterBase>();

    [Header("Global Rules")]
    [SerializeField] private bool preChaseEnabled = true;
    [SerializeField] private float globalCooldown = 10f;
    [SerializeField] private int encountersBeforeChase = 2;
    [SerializeField] private float visibleReactionGrace = 0.2f;
    [SerializeField] private bool debugLogs = false;

    private float nextAllowedEncounterTime = -999f;
    private Coroutine runningEncounter;
    private EncounterBase activeEncounter;
    private string lastEncounterId;
    private int completedEncounterCount;
    private float playerVisibleSince = -1f;

    public VillainAI Villain => villain;
    public Transform Player => player;
    public JumpscareSystem JumpscareSystem => jumpscareSystem;

    void Awake()
    {
        for (int i = 0; i < encounters.Count; i++)
        {
            if (encounters[i] != null)
            {
                encounters[i].Bind(this);
            }
        }
    }

    public void SetPreChaseEnabled(bool enabled)
    {
        preChaseEnabled = enabled;
        if (!enabled)
        {
            ForceStopActiveEncounter();
        }
    }

    public bool TryHandlePreChaseTick(VillainAI.AIState state, float distanceToPlayer, bool villainSeesPlayer, bool playerSeesVillain)
    {
        if (!preChaseEnabled || villain == null || player == null || state == VillainAI.AIState.Chasing)
        {
            return false;
        }

        if (runningEncounter != null)
        {
            return true;
        }

        if (playerSeesVillain)
        {
            if (playerVisibleSince < 0f)
            {
                playerVisibleSince = Time.time;
            }

            if (Time.time - playerVisibleSince >= visibleReactionGrace)
            {
                if (Time.time >= nextAllowedEncounterTime && TryTriggerEncounter(new EncounterContext(state, distanceToPlayer, villainSeesPlayer, playerSeesVillain), true))
                {
                    return true;
                }

                HandleVisibleFailSafe();
                return true;
            }
        }
        else
        {
            playerVisibleSince = -1f;
        }

        if (Time.time < nextAllowedEncounterTime)
        {
            return false;
        }

        return TryTriggerEncounter(new EncounterContext(state, distanceToPlayer, villainSeesPlayer, playerSeesVillain), false);
    }

    public void ForceStopActiveEncounter()
    {
        if (runningEncounter != null)
        {
            StopCoroutine(runningEncounter);
            runningEncounter = null;
        }

        if (activeEncounter != null)
        {
            activeEncounter.ForceStop();
            activeEncounter = null;
        }

        if (villain != null && villain.IsExternallyControlled)
        {
            villain.PopExternalControl();
        }
    }

    private bool TryTriggerEncounter(EncounterContext context, bool urgent)
    {
        EncounterBase selected = SelectEncounter(context, urgent);
        if (selected == null)
        {
            return false;
        }

        runningEncounter = StartCoroutine(RunEncounterRoutine(selected, context));
        return true;
    }

    private EncounterBase SelectEncounter(EncounterContext context, bool urgent)
    {
        EncounterBase fallback = null;
        for (int i = 0; i < encounters.Count; i++)
        {
            EncounterBase candidate = encounters[i];
            if (candidate == null || !candidate.CanRun(context))
            {
                continue;
            }

            if (candidate.EncounterId != lastEncounterId)
            {
                return candidate;
            }

            fallback = candidate;
        }

        if (urgent)
        {
            return fallback;
        }

        return null;
    }

    private IEnumerator RunEncounterRoutine(EncounterBase encounter, EncounterContext context)
    {
        activeEncounter = encounter;
        if (debugLogs)
        {
            Debug.Log($"EncounterManager starting: {encounter.EncounterId}");
        }

        yield return encounter.Run(context);

        completedEncounterCount++;
        lastEncounterId = encounter.EncounterId;
        nextAllowedEncounterTime = Time.time + globalCooldown;

        if (debugLogs)
        {
            Debug.Log($"EncounterManager finished: {encounter.EncounterId}");
        }

        activeEncounter = null;
        runningEncounter = null;

        if (completedEncounterCount >= encountersBeforeChase)
        {
            completedEncounterCount = 0;
            StartChaseCommit();
        }
    }

    private void HandleVisibleFailSafe()
    {
        if (jumpscareSystem != null && !jumpscareSystem.IsJumpscareActive())
        {
            jumpscareSystem.ForceMinorScare(ScareType.PresenceCue);
        }

        if (villain != null)
        {
            villain.ForceDisappearFromPlayer("Visible fail-safe reaction");
        }

        nextAllowedEncounterTime = Time.time + Mathf.Max(1f, globalCooldown * 0.35f);
    }

    private void StartChaseCommit()
    {
        if (chaseSystem != null && chaseSystem.RequestDirectorChase("Pre-chase encounters reached escalation threshold"))
        {
            return;
        }

        if (villain != null)
        {
            villain.ForceChase();
        }
    }
}
