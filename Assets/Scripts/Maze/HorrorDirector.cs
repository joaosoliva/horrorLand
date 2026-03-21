using UnityEngine;

public class HorrorDirector : MonoBehaviour
{
	public enum PacingBand
	{
		Calm,
		Uneasy,
		Panic,
		Recovery
	}

	[Header("References")]
	public VillainAI villainAI;
	public Transform player;
	public ScareScheduler scareScheduler;

	[Header("Tension Model")]
	[Range(0f, 1f)] public float currentTension = 0.1f;
	public float proximityRange = 20f;
	public float chaseIncreasePerSecond = 0.4f;
	public float passiveIncreasePerSecond = 0.08f;
	public float decayPerSecond = 0.12f;
	public float reliefDecayPerSecond = 0.2f;
	public float scareMemorySeconds = 10f;
	public float soundboardContributionWindow = 4f;
	public float humorReliefDrop = 0.12f;
	public float humorReliefDuration = 1.5f;
	public float humorReboundDelay = 0.75f;
	public float humorReboundDuration = 3f;
	public float humorReboundStrength = 0.2f;

	[Header("Pacing Controls")]
	public float maxQuietSeconds = 20f;
	public float finaleStartTime = 240f;
	public float reliefPhaseSeconds = 10f;
	public float buildThreshold = 0.28f;
	public float threatThreshold = 0.52f;
	public float peakThreshold = 0.78f;
	public Vector2 minute0Floor = new Vector2(0f, 0.12f);
	public Vector2 minute1Floor = new Vector2(60f, 0.22f);
	public Vector2 minute2Floor = new Vector2(120f, 0.35f);
	public Vector2 minute3Floor = new Vector2(180f, 0.48f);
	public Vector2 minute4Floor = new Vector2(240f, 0.65f);

	[Header("Reveal Budget")]
	public float ambientRevealMinInterval = 12f;
	public float ambientRevealMaxDuration = 1.4f;
	public float closeRevealDistance = 6f;
	public float closeRevealCooldown = 10f;
	public float threatRevealIntervalMultiplier = 0.75f;
	public float finaleRevealIntervalMultiplier = 0.55f;

	[Header("Debug")]
	public bool logPhaseChanges = false;

	public PacingBand CurrentBand => currentBand;
	public HorrorPhase CurrentPhase => currentPhase;
	public bool IsChaseActive => chaseActive;
	public float RuntimeSeconds => runtimeSeconds;
	public float CurrentTensionScore => currentTension * 100f;
	public float TimeSinceLastMeaningfulBeat => Time.time - lastMeaningfulBeatTime;
	public bool IsFinaleActive => finaleStarted;

	private PacingBand currentBand = PacingBand.Calm;
	private HorrorPhase currentPhase = HorrorPhase.Calm;
	private float lastScareTime = -999f;
	private float lastMeaningfulBeatTime = -999f;
	private float lastSoundboardTime = -999f;
	private float lastSoundboardLoudness = 0f;
	private float humorReliefUntilTime = -999f;
	private float humorReboundStartTime = -999f;
	private float humorReboundUntilTime = -999f;
	private float lastPeakTime = -999f;
	private bool chaseActive = false;
	private bool finaleStarted = false;
	private bool peakBroadcastActive = false;
	private float runtimeSeconds = 0f;
	private float lastBroadcastTension = -1f;
	private bool ambientRevealActive = false;
	private float ambientRevealStartTime = -999f;
	private float lastAmbientRevealTime = -999f;
	private float lastCloseRevealTime = -999f;

	void Start()
	{
		if (villainAI == null)
		{
			villainAI = FindObjectOfType<VillainAI>();
		}

		if (player == null)
		{
			GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
			if (playerObject != null)
			{
				player = playerObject.transform;
			}
		}

		if (scareScheduler == null)
		{
			scareScheduler = FindObjectOfType<ScareScheduler>();
		}

		lastMeaningfulBeatTime = Time.time;
		RefreshPacing(forceBroadcast: true);
	}

	void OnEnable()
	{
		HorrorEvents.OnChaseStarted += HandleChaseStarted;
		HorrorEvents.OnChaseEnded += HandleChaseEnded;
		HorrorEvents.OnSoundboardPlayed += HandleSoundboardPlayed;
		HorrorEvents.OnJumpscareTriggered += HandleJumpscareTriggered;
		HorrorEvents.OnScareTriggered += HandleScareTriggered;
	}

	void OnDisable()
	{
		HorrorEvents.OnChaseStarted -= HandleChaseStarted;
		HorrorEvents.OnChaseEnded -= HandleChaseEnded;
		HorrorEvents.OnSoundboardPlayed -= HandleSoundboardPlayed;
		HorrorEvents.OnJumpscareTriggered -= HandleJumpscareTriggered;
		HorrorEvents.OnScareTriggered -= HandleScareTriggered;
	}

	void Update()
	{
		runtimeSeconds += Time.deltaTime;

		if (!finaleStarted && runtimeSeconds >= finaleStartTime)
		{
			finaleStarted = true;
			lastMeaningfulBeatTime = Time.time;
			HorrorEvents.RaiseFinaleStarted();
		}

		float proximityTension = GetProximityTension();
		float scareWeight = GetRecentScareWeight();
		float soundboardWeight = GetRecentSoundboardWeight();
		float humorReliefWeight = GetHumorReliefWeight();
		float humorReboundWeight = GetHumorReboundWeight();
		float tensionFloor = GetMinuteFloor(runtimeSeconds);
		float phaseBonus = currentPhase == HorrorPhase.Threat ? 0.04f : 0f;
		float finaleBonus = finaleStarted ? 0.12f : 0f;

		float targetTension = Mathf.Clamp01(
			tensionFloor +
			(proximityTension * 0.38f) +
			(scareWeight * 0.24f) +
			(soundboardWeight * 0.12f) +
			(humorReboundWeight * humorReboundStrength) +
			(chaseActive ? 0.28f : 0f) +
			phaseBonus +
			finaleBonus -
			(humorReliefWeight * humorReliefDrop));

		float increaseRate = passiveIncreasePerSecond * (0.65f + proximityTension + scareWeight + soundboardWeight + humorReboundWeight);
		if (chaseActive)
		{
			increaseRate += chaseIncreasePerSecond;
		}
		if (finaleStarted)
		{
			increaseRate += 0.08f;
		}

		float decayRate = currentPhase == HorrorPhase.Relief ? reliefDecayPerSecond : decayPerSecond;
		float tensionVelocity = currentTension < targetTension ? increaseRate : -decayRate;
		currentTension = Mathf.Clamp01(currentTension + (tensionVelocity * Time.deltaTime));
		if (!chaseActive && currentTension > targetTension)
		{
			currentTension = Mathf.MoveTowards(currentTension, targetTension, decayRate * Time.deltaTime);
		}

		RefreshPacing(forceBroadcast: false);

		if (scareScheduler != null && Time.time - lastMeaningfulBeatTime >= maxQuietSeconds)
		{
			scareScheduler.RequestCatchUpBeat();
			lastMeaningfulBeatTime = Time.time;
		}
	}

	public void RegisterMeaningfulBeat(ScareType scareType)
	{
		HandleScareTriggered(scareType);
	}

	public bool CanAllowAmbientReveal(float distanceToPlayer)
	{
		if (chaseActive)
		{
			return false;
		}

		if (distanceToPlayer <= closeRevealDistance && Time.time - lastCloseRevealTime < closeRevealCooldown)
		{
			return false;
		}

		float intervalMultiplier = 1f;
		if (currentPhase == HorrorPhase.Threat || currentPhase == HorrorPhase.Peak)
		{
			intervalMultiplier = threatRevealIntervalMultiplier;
		}
		if (finaleStarted)
		{
			intervalMultiplier *= finaleRevealIntervalMultiplier;
		}

		float requiredInterval = ambientRevealMinInterval * Mathf.Max(0.15f, intervalMultiplier);
		return Time.time - lastAmbientRevealTime >= requiredInterval;
	}

	public void RegisterAmbientReveal(float distanceToPlayer)
	{
		if (!ambientRevealActive)
		{
			ambientRevealActive = true;
			ambientRevealStartTime = Time.time;
			lastAmbientRevealTime = Time.time;
			lastMeaningfulBeatTime = Time.time;
		}

		if (distanceToPlayer <= closeRevealDistance)
		{
			lastCloseRevealTime = Time.time;
		}
	}

	public void EndAmbientReveal()
	{
		ambientRevealActive = false;
		ambientRevealStartTime = -999f;
	}

	public bool ShouldEndAmbientReveal(float distanceToPlayer)
	{
		if (!ambientRevealActive)
		{
			return false;
		}

		if (distanceToPlayer <= closeRevealDistance)
		{
			return true;
		}

		float maxDuration = ambientRevealMaxDuration;
		if (currentPhase == HorrorPhase.Threat || currentPhase == HorrorPhase.Peak)
		{
			maxDuration *= 0.85f;
		}
		if (finaleStarted)
		{
			maxDuration *= 0.75f;
		}

		return Time.time - ambientRevealStartTime >= maxDuration;
	}

	void HandleChaseStarted()
	{
		chaseActive = true;
		EndAmbientReveal();
		lastPeakTime = Time.time;
		lastScareTime = Time.time;
		lastMeaningfulBeatTime = Time.time;
	}

	void HandleChaseEnded()
	{
		chaseActive = false;
		lastPeakTime = Time.time;
		lastMeaningfulBeatTime = Time.time;
	}

	void HandleSoundboardPlayed(string soundTag, float loudness)
	{
		lastSoundboardTime = Time.time;
		lastSoundboardLoudness = loudness;
		humorReliefUntilTime = Time.time + humorReliefDuration;
		humorReboundStartTime = humorReliefUntilTime + humorReboundDelay;
		humorReboundUntilTime = humorReboundStartTime + humorReboundDuration;
		currentTension = Mathf.Clamp01(currentTension - (humorReliefDrop * Mathf.Clamp01(loudness)));
		lastMeaningfulBeatTime = Time.time;
	}

	void HandleJumpscareTriggered()
	{
		lastScareTime = Time.time;
		lastPeakTime = Time.time;
		lastMeaningfulBeatTime = Time.time;
	}

	void HandleScareTriggered(ScareType scareType)
	{
		lastMeaningfulBeatTime = Time.time;
		if (scareType != ScareType.ReliefBeat)
		{
			lastScareTime = Time.time;
		}

		if (scareType == ScareType.MajorJumpscare || scareType == ScareType.ChaseTrigger)
		{
			lastPeakTime = Time.time;
		}
		else if (scareType == ScareType.ReliefBeat)
		{
			currentTension = Mathf.Clamp01(currentTension - 0.08f);
		}
	}

	float GetProximityTension()
	{
		if (villainAI == null || player == null)
		{
			return 0f;
		}

		float distance = Vector3.Distance(villainAI.transform.position, player.position);
		return 1f - Mathf.Clamp01(distance / Mathf.Max(0.01f, proximityRange));
	}

	float GetRecentScareWeight()
	{
		float elapsed = Time.time - lastScareTime;
		if (elapsed >= scareMemorySeconds)
		{
			return 0f;
		}

		return 1f - Mathf.Clamp01(elapsed / scareMemorySeconds);
	}

	float GetRecentSoundboardWeight()
	{
		if (Time.time < humorReliefUntilTime)
		{
			return 0f;
		}

		float elapsed = Time.time - lastSoundboardTime;
		if (elapsed >= soundboardContributionWindow)
		{
			return 0f;
		}

		float freshness = 1f - Mathf.Clamp01(elapsed / soundboardContributionWindow);
		return freshness * lastSoundboardLoudness;
	}

	float GetHumorReliefWeight()
	{
		if (Time.time >= humorReliefUntilTime)
		{
			return 0f;
		}

		float remaining = humorReliefUntilTime - Time.time;
		return Mathf.Clamp01(remaining / Mathf.Max(0.01f, humorReliefDuration)) * lastSoundboardLoudness;
	}

	float GetHumorReboundWeight()
	{
		if (Time.time < humorReboundStartTime || Time.time >= humorReboundUntilTime)
		{
			return 0f;
		}

		float normalized = Mathf.InverseLerp(humorReboundStartTime, humorReboundUntilTime, Time.time);
		return (1f - normalized) * lastSoundboardLoudness;
	}

	float GetMinuteFloor(float elapsedRuntime)
	{
		if (elapsedRuntime >= minute4Floor.x)
		{
			return minute4Floor.y;
		}
		if (elapsedRuntime >= minute3Floor.x)
		{
			return minute3Floor.y;
		}
		if (elapsedRuntime >= minute2Floor.x)
		{
			return minute2Floor.y;
		}
		if (elapsedRuntime >= minute1Floor.x)
		{
			return minute1Floor.y;
		}

		return minute0Floor.y;
	}

	void RefreshPacing(bool forceBroadcast)
	{
		HorrorPhase previousPhase = currentPhase;
		PacingBand previousBand = currentBand;

		if (finaleStarted)
		{
			currentPhase = HorrorPhase.Finale;
		}
		else if (chaseActive || currentTension >= peakThreshold)
		{
			currentPhase = HorrorPhase.Peak;
		}
		else if (Time.time - lastPeakTime <= reliefPhaseSeconds)
		{
			currentPhase = HorrorPhase.Relief;
		}
		else if (currentTension >= threatThreshold)
		{
			currentPhase = HorrorPhase.Threat;
		}
		else if (currentTension >= buildThreshold)
		{
			currentPhase = HorrorPhase.Build;
		}
		else
		{
			currentPhase = HorrorPhase.Calm;
		}

		if (currentPhase == HorrorPhase.Peak || currentPhase == HorrorPhase.Finale)
		{
			currentBand = PacingBand.Panic;
		}
		else if (currentPhase == HorrorPhase.Threat || currentPhase == HorrorPhase.Build)
		{
			currentBand = PacingBand.Uneasy;
		}
		else if (currentPhase == HorrorPhase.Relief)
		{
			currentBand = PacingBand.Recovery;
		}
		else
		{
			currentBand = PacingBand.Calm;
		}

		bool tensionChangedEnough = forceBroadcast || Mathf.Abs(currentTension - lastBroadcastTension) >= 0.01f;
		bool phaseChanged = previousPhase != currentPhase;
		if (tensionChangedEnough || phaseChanged)
		{
			lastBroadcastTension = currentTension;
			HorrorEvents.RaiseTensionChanged(currentTension);
			if (phaseChanged)
			{
				HorrorEvents.RaisePhaseChanged(currentPhase);
				if (logPhaseChanges)
				{
					Debug.Log("HorrorDirector phase changed: " + previousPhase + " -> " + currentPhase + " at tension " + currentTension.ToString("F2"));
				}
			}
		}

		bool peakActiveNow = currentPhase == HorrorPhase.Peak || currentPhase == HorrorPhase.Finale;
		if (peakActiveNow && !peakBroadcastActive)
		{
			peakBroadcastActive = true;
			HorrorEvents.RaiseMajorPeakStarted();
		}
		else if (!peakActiveNow && peakBroadcastActive)
		{
			peakBroadcastActive = false;
			HorrorEvents.RaiseMajorPeakEnded();
		}
	}
}
