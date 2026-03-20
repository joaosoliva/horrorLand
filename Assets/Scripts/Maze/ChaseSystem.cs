using UnityEngine;

public class ChaseSystem : MonoBehaviour
{
	public enum ChasePattern
	{
		ShortBurst,
		ProlongedPressure
	}

	[Header("References")]
	public VillainAI villainAI;
	public HorrorDirector horrorDirector;

	[Header("Pattern Settings")]
	public Vector2 shortBurstDurationRange = new Vector2(4f, 7f);
	public Vector2 prolongedPressureDurationRange = new Vector2(9f, 15f);
	public float shortBurstCooldown = 8f;
	public float prolongedPressureCooldown = 14f;
	[Range(0f, 1f)] public float prolongedPressureTensionThreshold = 0.55f;

	[Header("Escalation")]
	public float chaseDetectionGracePeriod = 0.75f;
	public float searchFallbackDuration = 2.5f;
	public bool alternatePatterns = true;

	[Header("Jumpscare Budget")]
	public bool driveJumpscareBudget = true;
	public float jumpscareWarmup = 1.5f;
	public float jumpscareCooldownDuringChase = 7f;
	public float jumpscareCooldownOutsideChase = 14f;
	public float postChaseJumpscareLockout = 4f;

	[Header("Debug")]
	public bool enableDebugLogs = false;

	public bool IsChaseActive => chaseActive;
	public ChasePattern ActivePattern => activePattern;
	public float ActivePatternTargetDuration => activePatternTargetDuration;
	public float ChaseElapsedTime => chaseActive ? Time.time - chaseStartTime : 0f;
	public float NextChaseAllowedTime => nextChaseAllowedTime;

	private bool chaseActive;
	private ChasePattern activePattern = ChasePattern.ShortBurst;
	private float activePatternTargetDuration;
	private float chaseStartTime = -999f;
	private float nextChaseAllowedTime = -999f;
	private float nextJumpscareAllowedTime = -999f;
	private float lastChaseEndTime = -999f;
	private bool useShortBurstNext = true;

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
		bool hasHeldLongEnough = elapsed >= chaseDetectionGracePeriod;
		bool exceededPatternDuration = elapsed >= activePatternTargetDuration;

		if (activePattern == ChasePattern.ShortBurst)
		{
			if (hasHeldLongEnough && !hasLineOfSight)
			{
				EndChaseIntoSearch("Short burst chase lost sight of player");
				return;
			}

			if (exceededPatternDuration)
			{
				EndChaseIntoSearch("Short burst chase duration budget exhausted");
			}
			return;
		}

		bool playerRecentlyDetected = villainAI.TimeSinceLastPlayerDetection <= searchFallbackDuration;
		if (exceededPatternDuration && (!hasLineOfSight || !playerRecentlyDetected))
		{
			EndChaseIntoSearch("Prolonged pressure chase timed out after losing pressure");
		}
	}

	public bool RequestChase(string reason)
	{
		if (villainAI == null)
		{
			return false;
		}

		if (chaseActive)
		{
			if (enableDebugLogs)
			{
				Debug.Log($"ChaseSystem ignored chase request because a chase is already active. Reason: {reason}");
			}
			return false;
		}

		if (Time.time < nextChaseAllowedTime)
		{
			if (enableDebugLogs)
			{
				Debug.Log($"ChaseSystem blocked chase request until {nextChaseAllowedTime:F1}. Reason: {reason}");
			}
			return false;
		}

		ChasePattern pattern = ChoosePattern();
		activePattern = pattern;
		activePatternTargetDuration = GetDurationForPattern(pattern);
		villainAI.BeginDirectedChase(pattern.ToString(), reason);
		return true;
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

			if (activePattern == ChasePattern.ShortBurst)
			{
				return playerDistance >= 6f;
			}

			return playerDistance >= 4f;
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
		if (activePatternTargetDuration <= 0f)
		{
			activePattern = ChoosePattern();
			activePatternTargetDuration = GetDurationForPattern(activePattern);
		}

		if (enableDebugLogs)
		{
			Debug.Log($"ChaseSystem started {activePattern} chase with target duration {activePatternTargetDuration:F1}s");
		}
	}

	void HandleChaseEnded()
	{
		chaseActive = false;
		lastChaseEndTime = Time.time;
		nextChaseAllowedTime = Time.time + GetCooldownForPattern(activePattern);
		activePatternTargetDuration = 0f;
		if (alternatePatterns)
		{
			useShortBurstNext = !useShortBurstNext;
		}

		if (enableDebugLogs)
		{
			Debug.Log($"ChaseSystem ended {activePattern} chase. Cooldown until {nextChaseAllowedTime:F1}");
		}
	}

	ChasePattern ChoosePattern()
	{
		if (alternatePatterns)
		{
			return useShortBurstNext ? ChasePattern.ShortBurst : ChasePattern.ProlongedPressure;
		}

		float tension = horrorDirector != null ? horrorDirector.currentTension : 0f;
		return tension >= prolongedPressureTensionThreshold ? ChasePattern.ProlongedPressure : ChasePattern.ShortBurst;
	}

	float GetDurationForPattern(ChasePattern pattern)
	{
		Vector2 range = pattern == ChasePattern.ShortBurst ? shortBurstDurationRange : prolongedPressureDurationRange;
		return Random.Range(range.x, Mathf.Max(range.x, range.y));
	}

	float GetCooldownForPattern(ChasePattern pattern)
	{
		return pattern == ChasePattern.ShortBurst ? shortBurstCooldown : prolongedPressureCooldown;
	}
}
