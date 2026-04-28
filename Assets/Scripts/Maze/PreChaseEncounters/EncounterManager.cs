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
    [SerializeField] private GameManager gameManager;
    [SerializeField] private List<EncounterBase> encounters = new List<EncounterBase>();

    [Header("Global Rules")]
    [SerializeField] private bool preChaseEnabled = true;
    [SerializeField] private bool requireGameActive = true;
    [SerializeField] private float globalCooldown = 10f;
    [SerializeField] private int encountersBeforeChase = 2;
    [SerializeField] private float initialEncounterGracePeriod = 12f;
    [SerializeField] private float visibleReactionGrace = 0.2f;
    [SerializeField] private bool enableEncounterDebugLogs = false;

    [Header("Back Encounter Staging")]
    [SerializeField] private float backEncounterTurnAngleThreshold = 140f;
    [SerializeField] private float backEncounterMaxPendingTime = 10f;
    [SerializeField] private float backEncounterMinDelay = 0.75f;
    [SerializeField] private float backEncounterMaxPendingTravelMeters = 3f;
    [SerializeField] private float backEncounterPostRevealCatchGrace = 0.9f;
    [SerializeField] private bool cancelBackEncounterOnSegmentChange = true;
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
    private Vector2Int pendingBackTravelDirection;
    private int pendingBackSegmentHash;
    private Vector3 pendingBackStartPosition;
    private float pendingBackStartedAt;
    private float lastBackEncounterTriggeredAt = -999f;
    private Vector3 lastPlayerPosition;
    private bool hasLastPlayerPosition;
    private Vector2Int lastTravelDirection;
    private string lastCandidateSummary = "None";

    public VillainAI Villain => villain;
    public Transform Player => player;
    public JumpscareSystem JumpscareSystem => jumpscareSystem;
    public MazeContextSnapshot CurrentContext => lastContext;
    public string LastRejectedReason => lastRejectedReason;
    public bool IsBackEncounterPending => pendingBackEncounter != null;
    public bool IsBackEncounterCatchGraceActive(float currentTime) => currentTime - lastBackEncounterTriggeredAt < backEncounterPostRevealCatchGrace;

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

        if (requireGameActive && gameManager != null && gameManager.IsGameEnded)
        {
            SetRejected("game already ended");
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

        UpdateTravelDirection();

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

        if (!villain.enabled)
        {
            reason = "villain disabled";
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
        List<string> rejectedCandidateIds = new List<string>();
        for (int i = 0; i < encounters.Count; i++)
        {
            EncounterBase candidate = encounters[i];
            if (candidate == null)
            {
                continue;
            }

            if (!candidate.CanRun(context))
            {
                rejectedCandidateIds.Add(candidate.EncounterId);
                continue;
            }

            validCandidates.Add(candidate);
        }

        if (validCandidates.Count == 0)
        {
            lastCandidateSummary = rejectedCandidateIds.Count > 0 ? $"None (rejected: {string.Join(",", rejectedCandidateIds)})" : "None";
            return null;
        }

        List<EncounterBase> nonRepeatedCandidates = new List<EncounterBase>();
        for (int i = 0; i < validCandidates.Count; i++)
        {
            if (validCandidates[i].EncounterId != lastEncounterId)
            {
                nonRepeatedCandidates.Add(validCandidates[i]);
            }
        }

        if (nonRepeatedCandidates.Count > 0)
        {
            lastCandidateSummary = BuildCandidateSummary(validCandidates, nonRepeatedCandidates);
            return nonRepeatedCandidates[Random.Range(0, nonRepeatedCandidates.Count)];
        }

        lastCandidateSummary = BuildCandidateSummary(validCandidates, nonRepeatedCandidates);
        return urgent ? validCandidates[Random.Range(0, validCandidates.Count)] : null;
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

        if (cancelBackEncounterOnSegmentChange && pendingBackSegmentHash != 0 && lastContext.StraightSegmentHash != pendingBackSegmentHash)
        {
            CancelPendingBackEncounter("player changed corridor segment");
            return false;
        }

        if (lastContext.IsIntersectionNearby || lastContext.IsCornerAhead)
        {
            CancelPendingBackEncounter("player moved into corner/intersection context");
            return false;
        }

        float pendingTravel = Vector3.Distance(new Vector3(player.position.x, 0f, player.position.z), new Vector3(pendingBackStartPosition.x, 0f, pendingBackStartPosition.z));
        if (pendingTravel > backEncounterMaxPendingTravelMeters)
        {
            CancelPendingBackEncounter("player moved too far before looking back");
            return false;
        }

        Vector2Int travelDir = pendingBackTravelDirection;
        if (travelDir == Vector2Int.zero)
        {
            travelDir = lastTravelDirection != Vector2Int.zero ? lastTravelDirection : lastContext.ForwardDirection;
        }

        Vector3 travelForward = new Vector3(travelDir.x, 0f, travelDir.y);
        if (travelForward.sqrMagnitude < 0.001f)
        {
            return true;
        }
        travelForward.Normalize();

        Vector3 currentForward = player.forward;
        currentForward.y = 0f;
        currentForward.Normalize();
        float angleDelta = Vector3.Angle(travelForward, currentForward);
        if (angleDelta < backEncounterTurnAngleThreshold)
        {
            return true;
        }

        lastBackEncounterTriggeredAt = Time.time;
        LogDebug($"BackEncounter triggered: player looked {angleDelta:F1} degrees away from travel direction.");
        EncounterContext context = new EncounterContext(state, distanceToPlayer, villainSeesPlayer, playerSeesVillain, lastContext);
        runningEncounter = StartCoroutine(RunEncounterRoutine(pendingBackEncounter, context));
        pendingBackEncounter = null;
        return true;
    }

    private void BeginPendingBackEncounter(BehindBackEncounter encounter)
    {
        pendingBackEncounter = encounter;
        pendingBackStartedAt = Time.time;
        pendingBackTravelDirection = lastTravelDirection != Vector2Int.zero ? lastTravelDirection : lastContext.ForwardDirection;
        pendingBackSegmentHash = lastContext.StraightSegmentHash;
        pendingBackStartPosition = player.position;
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
        pendingBackTravelDirection = Vector2Int.zero;
        pendingBackSegmentHash = 0;
    }

    private void UpdateTravelDirection()
    {
        if (player == null)
        {
            return;
        }

        Vector3 current = player.position;
        if (!hasLastPlayerPosition)
        {
            hasLastPlayerPosition = true;
            lastPlayerPosition = current;
            return;
        }

        Vector3 delta = current - lastPlayerPosition;
        delta.y = 0f;
        if (delta.sqrMagnitude < 0.0036f)
        {
            return;
        }

        if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.z))
        {
            lastTravelDirection = delta.x >= 0f ? Vector2Int.right : Vector2Int.left;
        }
        else
        {
            lastTravelDirection = delta.z >= 0f ? Vector2Int.up : Vector2Int.down;
        }

        lastPlayerPosition = current;
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
        string gameActive = gameManager == null ? "Unknown" : (!gameManager.IsGameEnded).ToString();
        float graceRemaining = Mathf.Max(0f, initialEncounterGracePeriod - (Time.time - runtimeStartedAt));
        string contextCell = lastContext.IsValid ? lastContext.CurrentCell.ToString() : "Invalid";
        string cornerDir = lastContext.IsCornerAhead ? lastContext.CornerTurnDirection.ToString() : "None";
        string overlay = $"EncounterMgr\\nCell: {contextCell}\\nInitialRoom: {lastContext.IsInitialRoom}\\nSafeZone: {lastContext.IsSafeZone}\\nGameActive: {gameActive}\\nGraceRemaining: {graceRemaining:F1}s\\nHallAheadCells: {lastContext.StraightCellsAhead}\\nHallSegmentCells: {lastContext.StraightSegmentLength}\\nCornerAhead: {lastContext.IsCornerAhead}\\nCornerDir: {cornerDir}\\nPendingBack: {pendingBack}\\nActive: {active}\\nCandidates: {lastCandidateSummary}\\nLastRejected: {lastRejectedReason}";
        GUI.Label(new Rect(24f, 24f, 680f, 260f), overlay);
    }

    void OnDrawGizmosSelected()
    {
        if (!drawDebugOverlay || !lastContext.IsValid)
        {
            return;
        }

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(lastContext.SuggestedHallSpawnPoint + Vector3.up * 0.2f, 0.4f);

        if (lastContext.IsCornerAhead)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(lastContext.SuggestedCornerRevealPoint + Vector3.up * 0.2f, 0.4f);
        }
    }

    private static string BuildCandidateSummary(List<EncounterBase> validCandidates, List<EncounterBase> nonRepeatedCandidates)
    {
        if (validCandidates == null || validCandidates.Count == 0)
        {
            return "None";
        }

        string all = string.Empty;
        for (int i = 0; i < validCandidates.Count; i++)
        {
            all += validCandidates[i].EncounterId;
            if (i < validCandidates.Count - 1)
            {
                all += ",";
            }
        }

        if (nonRepeatedCandidates == null || nonRepeatedCandidates.Count == 0)
        {
            return all + " (repeat-only)";
        }

        return all;
    }

    [ContextMenu("Phase 7/Checklist: Log Encounter Runtime Status")]
    private void LogEncounterRuntimeStatus()
    {
        string header = "[EncounterChecklist]";
        if (player == null || villain == null || mazeContextQuery == null)
        {
            Debug.LogWarning($"{header} Missing core references. player={(player != null)}, villain={(villain != null)}, mazeContextQuery={(mazeContextQuery != null)}");
            return;
        }

        float runtime = Time.time - runtimeStartedAt;
        bool contextReady = mazeContextQuery.TryBuildContext(player, out MazeContextSnapshot context);
        string reason = contextReady ? string.Empty : "Context unavailable";
        bool canTrigger = contextReady && mazeContextQuery.CanTriggerEncounter(player, runtime, initialEncounterGracePeriod, out reason);
        string pending = pendingBackEncounter != null ? "Yes" : "No";
        string active = activeEncounter != null ? activeEncounter.EncounterId : "None";
        string state = villain.CurrentState.ToString();
        string visibility = villain.IsVillainVisibleToPlayer().ToString();
        string safeZone = contextReady ? context.IsSafeZone.ToString() : "Unknown";
        string initialRoom = contextReady ? context.IsInitialRoom.ToString() : "Unknown";
        string hallCells = contextReady ? context.StraightCellsAhead.ToString() : "Unknown";
        string hallSegment = contextReady ? context.StraightSegmentLength.ToString() : "Unknown";
        string corner = contextReady ? context.IsCornerAhead.ToString() : "Unknown";
        string cornerDir = contextReady && context.IsCornerAhead ? context.CornerTurnDirection.ToString() : "None";

        Debug.Log($"{header} Runtime={runtime:F1}s | AIState={state} | VisibleToPlayer={visibility} | InitialRoom={initialRoom} | SafeZone={safeZone} | HallCells={hallCells} | HallSegment={hallSegment} | CornerAhead={corner} ({cornerDir}) | PendingBack={pending} | ActiveEncounter={active} | CanTriggerNow={canTrigger} | Reason={(canTrigger ? "Eligible" : reason)}");
    }
}
