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
    [SerializeField] private MazeContextQuery mazeContextQuery;
    [SerializeField] private List<EncounterBase> encounters = new List<EncounterBase>();

    [Header("Global Rules")]
    [SerializeField] private bool preChaseEnabled = true;
    [SerializeField] private float globalCooldown = 10f;
    [SerializeField] private int encountersBeforeChase = 2;
    [SerializeField] private float initialEncounterGracePeriod = 12f;
    [SerializeField] private float visibleReactionGrace = 0.2f;
    [SerializeField] private bool enableEncounterDebugLogs = false;

    [Header("Back Encounter Staging")]
    [SerializeField] private float backEncounterTurnAngleThreshold = 140f;
    [SerializeField] private float backEncounterMaxPendingTime = 10f;
    [SerializeField] private float backEncounterMinDelay = 0.75f;
    [SerializeField] private bool drawDebugOverlay;

    private float nextAllowedEncounterTime = -999f;
    private float runtimeStartedAt;
    private Coroutine runningEncounter;
    private EncounterBase activeEncounter;
    private string lastEncounterId;
    private int completedEncounterCount;
    private float playerVisibleSince = -1f;
    private MazeContextSnapshot lastContext = MazeContextSnapshot.Invalid;
    private string lastRejectedReason = "None";

    private BehindBackEncounter pendingBackEncounter;
    private Vector3 pendingBackInitialForward;
    private float pendingBackStartedAt;

    public VillainAI Villain => villain;
    public Transform Player => player;
    public JumpscareSystem JumpscareSystem => jumpscareSystem;
    public MazeContextSnapshot CurrentContext => lastContext;
    public string LastRejectedReason => lastRejectedReason;

    void Awake()
    {
        runtimeStartedAt = Time.time;
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
            CancelPendingBackEncounter("pre-chase disabled");
            ForceStopActiveEncounter();
        }
    }

    public bool TryHandlePreChaseTick(VillainAI.AIState state, float distanceToPlayer, bool villainSeesPlayer, bool playerSeesVillain)
    {
        if (!preChaseEnabled || villain == null || player == null || state == VillainAI.AIState.Chasing)
        {
            return false;
        }

        bool hasContext = mazeContextQuery != null && mazeContextQuery.TryBuildContext(player, out lastContext);
        if (!hasContext)
        {
            lastContext = MazeContextSnapshot.Invalid;
            SetRejected("maze context unavailable");
            return false;
        }

        if (!IsGloballyEligible(out string blockReason))
        {
            SetRejected(blockReason);
            CancelPendingBackEncounter(blockReason);
            return false;
        }

        if (runningEncounter != null)
        {
            return true;
        }

        if (pendingBackEncounter != null)
        {
            return TryResolvePendingBack(state, distanceToPlayer, villainSeesPlayer, playerSeesVillain);
        }

        if (playerSeesVillain)
        {
            if (playerVisibleSince < 0f)
            {
                playerVisibleSince = Time.time;
            }

            if (Time.time - playerVisibleSince >= visibleReactionGrace)
            {
                if (Time.time >= nextAllowedEncounterTime && TryTriggerEncounter(new EncounterContext(state, distanceToPlayer, villainSeesPlayer, playerSeesVillain, lastContext), true))
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
            SetRejected($"global cooldown {(nextAllowedEncounterTime - Time.time):F1}s");
            return false;
        }

        return TryTriggerEncounter(new EncounterContext(state, distanceToPlayer, villainSeesPlayer, playerSeesVillain, lastContext), false);
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

    private bool IsGloballyEligible(out string reason)
    {
        reason = string.Empty;
        float runtime = Time.time - runtimeStartedAt;
        if (mazeContextQuery != null && !mazeContextQuery.CanTriggerEncounter(player, runtime, initialEncounterGracePeriod, out reason))
        {
            return false;
        }

        if (lastContext.IsSafeZone)
        {
            reason = "player in safe zone";
            return false;
        }

        if (lastContext.IsInitialRoom)
        {
            reason = "player in initial room";
            return false;
        }

        return true;
    }

    private bool TryTriggerEncounter(EncounterContext context, bool urgent)
    {
        EncounterBase selected = SelectEncounter(context, urgent);
        if (selected == null)
        {
            LogDebug("Encounter selection yielded no valid candidates.");
            SetRejected("no valid encounter candidates");
            return false;
        }

        if (selected is BehindBackEncounter backEncounter)
        {
            BeginPendingBackEncounter(backEncounter);
            return true;
        }

        runningEncounter = StartCoroutine(RunEncounterRoutine(selected, context));
        lastRejectedReason = "None";
        return true;
    }

    private EncounterBase SelectEncounter(EncounterContext context, bool urgent)
    {
        List<EncounterBase> validCandidates = new List<EncounterBase>();
        for (int i = 0; i < encounters.Count; i++)
        {
            EncounterBase candidate = encounters[i];
            if (candidate == null || !candidate.CanRun(context))
            {
                continue;
            }

            validCandidates.Add(candidate);
        }

        if (validCandidates.Count == 0)
        {
            return null;
        }

        for (int i = 0; i < validCandidates.Count; i++)
        {
            if (validCandidates[i].EncounterId != lastEncounterId)
            {
                return validCandidates[i];
            }
        }

        return urgent ? validCandidates[0] : null;
    }

    private IEnumerator RunEncounterRoutine(EncounterBase encounter, EncounterContext context)
    {
        activeEncounter = encounter;
        LogDebug($"EncounterManager starting: {encounter.EncounterId}");

        yield return encounter.Run(context);

        completedEncounterCount++;
        lastEncounterId = encounter.EncounterId;
        nextAllowedEncounterTime = Time.time + globalCooldown;

        LogDebug($"EncounterManager finished: {encounter.EncounterId}");

        activeEncounter = null;
        runningEncounter = null;

        if (completedEncounterCount >= encountersBeforeChase)
        {
            completedEncounterCount = 0;
            StartChaseCommit();
        }
    }

    private bool TryResolvePendingBack(VillainAI.AIState state, float distanceToPlayer, bool villainSeesPlayer, bool playerSeesVillain)
    {
        if (pendingBackEncounter == null)
        {
            return false;
        }

        float elapsed = Time.time - pendingBackStartedAt;
        if (elapsed > backEncounterMaxPendingTime)
        {
            CancelPendingBackEncounter("player did not turn in time");
            return false;
        }

        if (elapsed < backEncounterMinDelay)
        {
            return true;
        }

        Vector3 currentForward = player.forward;
        currentForward.y = 0f;
        currentForward.Normalize();
        float angleDelta = Vector3.Angle(pendingBackInitialForward, currentForward);
        if (angleDelta < backEncounterTurnAngleThreshold)
        {
            return true;
        }

        LogDebug($"BackEncounter triggered: player turned {angleDelta:F1} degrees.");
        EncounterContext context = new EncounterContext(state, distanceToPlayer, villainSeesPlayer, playerSeesVillain, lastContext);
        runningEncounter = StartCoroutine(RunEncounterRoutine(pendingBackEncounter, context));
        pendingBackEncounter = null;
        return true;
    }

    private void BeginPendingBackEncounter(BehindBackEncounter encounter)
    {
        pendingBackEncounter = encounter;
        pendingBackStartedAt = Time.time;
        pendingBackInitialForward = player.forward;
        pendingBackInitialForward.y = 0f;
        pendingBackInitialForward.Normalize();
        LogDebug("BackEncounter pending: waiting for player turn.");
        lastRejectedReason = "BackEncounter pending";
    }

    private void CancelPendingBackEncounter(string reason)
    {
        if (pendingBackEncounter == null)
        {
            return;
        }

        LogDebug($"BackEncounter cancelled: {reason}.");
        pendingBackEncounter = null;
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

    private void LogDebug(string message)
    {
        if (enableEncounterDebugLogs)
        {
            Debug.Log($"[EncounterManager] {message}");
        }
    }

    private void SetRejected(string reason)
    {
        lastRejectedReason = reason;
        LogDebug($"Encounter blocked: {reason}.");
    }

    void OnGUI()
    {
        if (!drawDebugOverlay)
        {
            return;
        }

        string active = activeEncounter != null ? activeEncounter.EncounterId : "None";
        string pendingBack = pendingBackEncounter != null ? "Yes" : "No";
        float graceRemaining = Mathf.Max(0f, initialEncounterGracePeriod - (Time.time - runtimeStartedAt));
        string contextCell = lastContext.IsValid ? lastContext.CurrentCell.ToString() : "Invalid";
        string overlay = $"EncounterMgr\\nCell: {contextCell}\\nInitialRoom: {lastContext.IsInitialRoom}\\nSafeZone: {lastContext.IsSafeZone}\\nGraceRemaining: {graceRemaining:F1}s\\nHallAheadCells: {lastContext.StraightCellsAhead}\\nCornerAhead: {lastContext.IsCornerAhead}\\nPendingBack: {pendingBack}\\nActive: {active}\\nLastRejected: {lastRejectedReason}";
        GUI.Label(new Rect(24f, 24f, 520f, 220f), overlay);
    }
}
