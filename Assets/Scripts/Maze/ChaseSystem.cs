using UnityEngine;

public class ChaseSystem : MonoBehaviour
{
	public enum ChasePattern
	{
		ShortBurst,
		ProlongedPressure,
		FakeChase
	}

	public enum ChaseStage
	{
		Idle,
		Reveal,
		Pursuit,
		SearchWindow
	}

	[Header("References")]
	public VillainAI villainAI;
	public HorrorDirector horrorDirector;

	[Header("Pattern Settings")]
	public Vector2 shortBurstDurationRange = new Vector2(4f, 8f);
	public Vector2 prolongedPressureDurationRange = new Vector2(10f, 18f);
	public Vector2 fakeChaseDurationRange = new Vector2(2.5f, 4.5f);
	public float shortBurstCooldown = 7f;
	public float prolongedPressureCooldown = 12f;
	public float fakeChaseCooldown = 9f;
	[Range(0f, 1f)] public float prolongedPressureTensionThreshold = 0.55f;
	[Range(0f, 1f)] public float fakeChaseChance = 0.18f;
	[Range(0f, 1f)] public float finaleChaseBias = 0.35f;

	[Header("Escalation")]
	public float chaseDetectionGracePeriod = 0.75f;
	public float graceReacquireTime = 2.75f;
	public float searchFallbackDuration = 2.5f;
	public bool alternatePatterns = true;

	[Header("Escape Window")]
	public float revealFreezeWindow = 0.45f;
	public float lineOfSightBreakTime = 1.5f;
	public float searchSpeedMultiplier = 0.72f;
	public float reacquireSpeedBonus = 0.18f;
	public float reacquireBurstDuration = 1.15f;
	public float successfulEscapeSafetyWindow = 3.5f;
	public float closeRangePersistenceDistance = 4.5f;
	public float pointBlankPersistenceDistance = 2.35f;
	public float closeRangeMinCommitTime = 2.1f;

	[Header("Jumpscare Budget")]
	public bool driveJumpscareBudget = true;
	public float jumpscareWarmup = 1.25f;
	public float jumpscareCooldownDuringChase = 6f;
	public float jumpscareCooldownOutsideChase = 12f;
	public float postChaseJumpscareLockout = 3f;

	[Header("Director Difficulty Influence")]
	[Range(0f, 2f)] public float tensionSpeedInfluence = 0.45f;
	public float buildPhaseSpeedBonus = 0.08f;
	public float threatPhaseSpeedBonus = 0.2f;
	public float peakPhaseSpeedBonus = 0.35f;
	public float finalePhaseSpeedBonus = 0.5f;
	public float earlyChaseAssistWindow = 75f;
	[Range(0f, 1f)] public float earlyChaseSpeedAssist = 0.14f;

	[Header("Debug")]
	public bool enableDebugLogs = false;

	public bool IsChaseActive { get { return chaseActive; } }
	public ChasePattern ActivePattern { get { return activePattern; } }
	public ChaseStage CurrentStage { get { return currentStage; } }
	public float ActivePatternTargetDuration { get { return activePatternTargetDuration; } }
	public float ChaseElapsedTime { get { return chaseActive ? Time.time - chaseStartTime : 0f; } }
	public float NextChaseAllowedTime { get { return nextChaseAllowedTime; } }
	public bool IsEscapeSafetyWindowActive { get { return Time.time < escapeSafetyWindowUntilTime; } }

	private bool chaseActive;
	private ChasePattern activePattern = ChasePattern.ShortBurst;
	private ChaseStage currentStage = ChaseStage.Idle;
	private float activePatternTargetDuration;
	private float chaseStartTime = -999f;
	private float nextChaseAllowedTime = -999f;
	private float nextJumpscareAllowedTime = -999f;
	private float lastChaseEndTime = -999f;
	private bool useShortBurstNext = true;
	private float lastLineOfSightTime = -999f;
	private float searchWindowStartedTime = -999f;
	private float reacquireBurstUntilTime = -999f;
	private float escapeSafetyWindowUntilTime = -999f;

	void Start()
	{
		if (villainAI == null)
		{
			villainAI = FindObjectOfType<VillainAI>();
		}

		if (horrorDirector == null)
		{
			horrorDirector = FindObjectOfType<HorrorDirector>();
		}
	}

	void OnEnable()
	{
		HorrorEvents.OnChaseStarted += HandleChaseStarted;
		HorrorEvents.OnChaseEnded += HandleChaseEnded;
	}

	void OnDisable()
	{
		HorrorEvents.OnChaseStarted -= HandleChaseStarted;
		HorrorEvents.OnChaseEnded -= HandleChaseEnded;
	}

	void Update()
	{
		if (villainAI == null || !chaseActive)
		{
			return;
		}

		float elapsed = Time.time - chaseStartTime;
		bool hasLineOfSight = villainAI.CanSeePlayer();
		float distanceToPlayer = villainAI.player != null ? Vector3.Distance(villainAI.transform.position, villainAI.player.position) : float.MaxValue;
		bool heldLongEnough = elapsed >= chaseDetectionGracePeriod;
		bool exceededPatternDuration = elapsed >= activePatternTargetDuration;
		bool playerRecentlyDetected = villainAI.TimeSinceLastPlayerDetection <= graceReacquireTime;
		bool revealComplete = elapsed >= revealFreezeWindow;

		if (hasLineOfSight)
		{
			if (currentStage == ChaseStage.SearchWindow)
			{
				reacquireBurstUntilTime = Time.time + reacquireBurstDuration;
			}
			currentStage = revealComplete ? ChaseStage.Pursuit : ChaseStage.Reveal;
			lastLineOfSightTime = Time.time;
			searchWindowStartedTime = -999f;
		}
		else if (revealComplete && Time.time - lastLineOfSightTime >= lineOfSightBreakTime)
		{
			if (currentStage != ChaseStage.SearchWindow)
			{
				currentStage = ChaseStage.SearchWindow;
				searchWindowStartedTime = Time.time;
			}
		}

		if (!revealComplete)
		{
			return;
		}

		bool shouldPersistCloseRange = distanceToPlayer <= closeRangePersistenceDistance;
		bool inPointBlankRange = distanceToPlayer <= pointBlankPersistenceDistance;
		bool closeRangeCommitLocked = shouldPersistCloseRange && elapsed < closeRangeMinCommitTime;

		if (activePattern == ChasePattern.FakeChase)
		{
			if (!shouldPersistCloseRange && heldLongEnough && (!hasLineOfSight || exceededPatternDuration))
			{
				EndChaseIntoSearch("Fake chase peeled away into uncertainty");
			}
			return;
		}

		if (activePattern == ChasePattern.ShortBurst)
		{
			if (!shouldPersistCloseRange && heldLongEnough && currentStage == ChaseStage.SearchWindow && Time.time - searchWindowStartedTime >= searchFallbackDuration)
			{
				EndChaseIntoSearch("Short burst chase lost sight of player");
				return;
			}

			if (exceededPatternDuration && !closeRangeCommitLocked && !inPointBlankRange)
			{
				EndChaseIntoSearch("Short burst chase duration budget exhausted");
			}
			return;
		}

		if (!shouldPersistCloseRange && currentStage == ChaseStage.SearchWindow && Time.time - searchWindowStartedTime >= searchFallbackDuration && !hasLineOfSight)
		{
			EndChaseIntoSearch("Prolonged pressure chase lost player long enough to break pursuit");
			return;
		}

		if (!closeRangeCommitLocked && !inPointBlankRange && exceededPatternDuration && (!hasLineOfSight || !playerRecentlyDetected || villainAI.TimeSinceLastPlayerDetection > searchFallbackDuration))
		{
			EndChaseIntoSearch("Prolonged pressure chase timed out after losing pressure");
		}
	}

	public bool RequestChase(string reason)
	{
		return TryStartChase(reason, false);
	}

	public bool RequestDirectorChase(string reason)
	{
		return TryStartChase(reason, true);
	}

	public bool CanTriggerContextualJumpscare(float playerDistance)
	{
		if (!driveJumpscareBudget)
		{
			return true;
		}

		if (Time.time < nextJumpscareAllowedTime)
		{
			return false;
		}

		if (chaseActive)
		{
			if (Time.time - chaseStartTime < jumpscareWarmup)
			{
				return false;
			}

			if (activePattern == ChasePattern.FakeChase)
			{
				return false;
			}

			if (activePattern == ChasePattern.ShortBurst)
			{
				return playerDistance >= 5f;
			}

			return playerDistance >= 3.5f;
		}

		return Time.time - lastChaseEndTime >= postChaseJumpscareLockout;
	}

	public void ConsumeJumpscareBudget()
	{
		if (!driveJumpscareBudget)
		{
			return;
		}

		nextJumpscareAllowedTime = Time.time + (chaseActive ? jumpscareCooldownDuringChase : jumpscareCooldownOutsideChase);
	}

	bool TryStartChase(string reason, bool directorRequested)
	{
		if (villainAI == null)
		{
			return false;
		}

		if (chaseActive)
		{
			return false;
		}

		if (Time.time < escapeSafetyWindowUntilTime)
		{
			return false;
		}

		if (Time.time < nextChaseAllowedTime && !directorRequested)
		{
			if (enableDebugLogs)
			{
				Debug.Log("ChaseSystem blocked chase request until " + nextChaseAllowedTime.ToString("F1") + ". Reason: " + reason);
			}
			return false;
		}

		activePattern = ChoosePattern(directorRequested);
		activePatternTargetDuration = GetDurationForPattern(activePattern);
		villainAI.BeginDirectedChase(activePattern.ToString(), reason);
		HorrorEvents.RaiseScareTriggered(ScareType.ChaseTrigger);
		return true;
	}

	void EndChaseIntoSearch(string reason)
	{
		if (villainAI == null || !villainAI.IsChasing)
		{
			return;
		}

		villainAI.ForceSearchAtLastKnownPosition(reason);
	}

	void HandleChaseStarted()
	{
		chaseActive = true;
		chaseStartTime = Time.time;
		currentStage = ChaseStage.Reveal;
		lastLineOfSightTime = Time.time;
		searchWindowStartedTime = -999f;
		reacquireBurstUntilTime = -999f;
		if (activePatternTargetDuration <= 0f)
		{
			activePattern = ChoosePattern(false);
			activePatternTargetDuration = GetDurationForPattern(activePattern);
		}

		if (enableDebugLogs)
		{
			Debug.Log("ChaseSystem started " + activePattern + " chase with target duration " + activePatternTargetDuration.ToString("F1") + "s");
		}
	}

	void HandleChaseEnded()
	{
		chaseActive = false;
		lastChaseEndTime = Time.time;
		currentStage = ChaseStage.Idle;
		float cooldown = GetCooldownForPattern(activePattern);
		if (horrorDirector != null && horrorDirector.IsFinaleActive)
		{
			cooldown *= 0.65f;
		}
		nextChaseAllowedTime = Time.time + cooldown;
		escapeSafetyWindowUntilTime = Time.time + successfulEscapeSafetyWindow;
		activePatternTargetDuration = 0f;
		if (alternatePatterns)
		{
			useShortBurstNext = !useShortBurstNext;
		}
	}

	ChasePattern ChoosePattern(bool directorRequested)
	{
		float tension = horrorDirector != null ? horrorDirector.currentTension : 0f;
		HorrorPhase phase = horrorDirector != null ? horrorDirector.CurrentPhase : HorrorPhase.Build;
		bool finale = horrorDirector != null && horrorDirector.IsFinaleActive;

		if (!finale && Random.value < fakeChaseChance && phase != HorrorPhase.Relief)
		{
			return ChasePattern.FakeChase;
		}

		if (finale || phase == HorrorPhase.Finale)
		{
			if (Random.value < Mathf.Clamp01(0.55f + finaleChaseBias))
			{
				return ChasePattern.ProlongedPressure;
			}
		}

		if (directorRequested && (phase == HorrorPhase.Threat || phase == HorrorPhase.Peak || tension >= prolongedPressureTensionThreshold))
		{
			return ChasePattern.ProlongedPressure;
		}

		if (alternatePatterns)
		{
			return useShortBurstNext ? ChasePattern.ShortBurst : ChasePattern.ProlongedPressure;
		}

		return tension >= prolongedPressureTensionThreshold ? ChasePattern.ProlongedPressure : ChasePattern.ShortBurst;
	}

	public float GetChaseSpeedMultiplier()
	{
		if (!chaseActive)
		{
			return 1f;
		}

		if (currentStage == ChaseStage.Reveal)
		{
			return 0.15f;
		}

		if (currentStage == ChaseStage.SearchWindow)
		{
			return searchSpeedMultiplier;
		}

		float multiplier = 1f;
		if (Time.time < reacquireBurstUntilTime)
		{
			multiplier += reacquireSpeedBonus;
		}

		if (horrorDirector != null)
		{
			float tensionBonus = Mathf.Clamp01(horrorDirector.currentTension) * tensionSpeedInfluence;
			multiplier += tensionBonus;

			switch (horrorDirector.CurrentPhase)
			{
				case HorrorPhase.Build:
					multiplier += buildPhaseSpeedBonus;
					break;
				case HorrorPhase.Threat:
					multiplier += threatPhaseSpeedBonus;
					break;
				case HorrorPhase.Peak:
					multiplier += peakPhaseSpeedBonus;
					break;
				case HorrorPhase.Finale:
					multiplier += finalePhaseSpeedBonus;
					break;
			}
		}

		if (horrorDirector != null)
		{
			float runtime = horrorDirector.RuntimeSeconds;
			float earlyAssistT = 1f - Mathf.Clamp01(runtime / Mathf.Max(1f, earlyChaseAssistWindow));
			multiplier -= earlyAssistT * earlyChaseSpeedAssist;
		}

		return Mathf.Max(0.55f, multiplier);
	}

	float GetDurationForPattern(ChasePattern pattern)
	{
		Vector2 range = shortBurstDurationRange;
		if (pattern == ChasePattern.ProlongedPressure)
		{
			range = prolongedPressureDurationRange;
		}
		else if (pattern == ChasePattern.FakeChase)
		{
			range = fakeChaseDurationRange;
		}

		return Random.Range(range.x, Mathf.Max(range.x, range.y));
	}

	float GetCooldownForPattern(ChasePattern pattern)
	{
		if (pattern == ChasePattern.ProlongedPressure)
		{
			return prolongedPressureCooldown;
		}
		if (pattern == ChasePattern.FakeChase)
		{
			return fakeChaseCooldown;
		}

		return shortBurstCooldown;
	}
}
