using UnityEngine;

public class SanitySystem : MonoBehaviour
{
	[Header("Sanity")]
	public float maxSanity = 100f;
	public float startingSanity = 100f;
	public float currentSanity = 100f;
	public bool clampSanity = true;

	[Header("Passive Changes")]
	public float passiveRecoveryPerSecond = 2.4f;
	public float safeBandRecoveryMultiplier = 1.25f;
	public float nearBandRecoveryMultiplier = 0.55f;
	public float dangerBandDrainPerSecond = 4.5f;
	public float immediateBandDrainPerSecond = 8f;
	public float chaseDrainPerSecond = 9f;

	[Header("Event Impacts")]
	public float chaseStartSanityLoss = 10f;
	public float chaseEndSanityLoss = 4f;
	public float majorJumpscareSanityLoss = 16f;
	public float minorScareSanityLoss = 5f;
	public float fakeoutSanityLoss = 3f;
	public float presenceSanityLoss = 4f;
	public float routePressureSanityLoss = 5f;
	public float reliefBeatSanityGain = 6f;
	public float nearMissSanityLoss = 7f;

	[Header("Soundboard Relief")]
	public bool gainSanityFromSoundboard = true;
	public float soundboardBaseSanityGain = 0.8f;
	public float soundboardAdditionalSanityGain = 0.9f;

	[Header("Thresholds")]
	[Range(0f, 1f)] public float lowSanityThresholdNormalized = 0.3f;
	[Range(0f, 1f)] public float criticalSanityThresholdNormalized = 0.15f;
	public bool enableDebugLogs = false;

	public float CurrentSanity => currentSanity;
	public float NormalizedSanity => maxSanity <= 0f ? 0f : Mathf.Clamp01(currentSanity / maxSanity);
	public float Stress01 => 1f - NormalizedSanity;

	private bool chaseActive;
	private EnemyDistanceBand currentBand = EnemyDistanceBand.Far;
	private EnemyDistanceBand previousBand = EnemyDistanceBand.Far;
	private bool wasLowSanity;
	private bool wasCriticalSanity;

	void Start()
	{
		if (maxSanity <= 0f)
		{
			maxSanity = 100f;
		}

		currentSanity = Mathf.Clamp(startingSanity, 0f, maxSanity);
		BroadcastSanity();
	}

	void OnEnable()
	{
		HorrorEvents.OnChaseStarted += HandleChaseStarted;
		HorrorEvents.OnChaseEnded += HandleChaseEnded;
		HorrorEvents.OnJumpscareTriggered += HandleMajorJumpscare;
		HorrorEvents.OnScareTriggered += HandleScareTriggered;
		HorrorEvents.OnThreatBandChanged += HandleThreatBandChanged;
		HorrorEvents.OnSoundboardPlayed += HandleSoundboardPlayed;
	}

	void OnDisable()
	{
		HorrorEvents.OnChaseStarted -= HandleChaseStarted;
		HorrorEvents.OnChaseEnded -= HandleChaseEnded;
		HorrorEvents.OnJumpscareTriggered -= HandleMajorJumpscare;
		HorrorEvents.OnScareTriggered -= HandleScareTriggered;
		HorrorEvents.OnThreatBandChanged -= HandleThreatBandChanged;
		HorrorEvents.OnSoundboardPlayed -= HandleSoundboardPlayed;
	}

	void Update()
	{
		float delta = 0f;
		if (chaseActive)
		{
			delta -= chaseDrainPerSecond * Time.deltaTime;
		}

		if (currentBand == EnemyDistanceBand.Immediate)
		{
			delta -= immediateBandDrainPerSecond * Time.deltaTime;
		}
		else if (currentBand == EnemyDistanceBand.Danger)
		{
			delta -= dangerBandDrainPerSecond * Time.deltaTime;
		}
		else
		{
			float recoveryMultiplier = currentBand == EnemyDistanceBand.Far ? safeBandRecoveryMultiplier : nearBandRecoveryMultiplier;
			delta += passiveRecoveryPerSecond * recoveryMultiplier * Time.deltaTime;
		}

		if (Mathf.Abs(delta) > 0f)
		{
			ApplySanityDelta(delta, "PassiveTick");
		}
	}

	void HandleChaseStarted()
	{
		chaseActive = true;
		ApplySanityDelta(-chaseStartSanityLoss, "ChaseStarted");
	}

	void HandleChaseEnded()
	{
		chaseActive = false;
		ApplySanityDelta(-chaseEndSanityLoss, "ChaseEnded");
	}

	void HandleMajorJumpscare()
	{
		ApplySanityDelta(-majorJumpscareSanityLoss, "MajorJumpscare");
	}

	void HandleScareTriggered(ScareType scareType)
	{
		if (scareType == ScareType.MinorPsychological)
		{
			ApplySanityDelta(-minorScareSanityLoss, "MinorScare");
		}
		else if (scareType == ScareType.Fakeout)
		{
			ApplySanityDelta(-fakeoutSanityLoss, "Fakeout");
		}
		else if (scareType == ScareType.PresenceCue)
		{
			ApplySanityDelta(-presenceSanityLoss, "PresenceCue");
		}
		else if (scareType == ScareType.RoutePressure)
		{
			ApplySanityDelta(-routePressureSanityLoss, "RoutePressure");
		}
		else if (scareType == ScareType.ReliefBeat)
		{
			ApplySanityDelta(reliefBeatSanityGain, "ReliefBeat");
		}
	}


	void HandleSoundboardPlayed(string soundTag, float loudness)
	{
		if (!gainSanityFromSoundboard)
		{
			return;
		}

		float clampedLoudness = Mathf.Clamp01(loudness);
		float gain = soundboardBaseSanityGain + ((1f - clampedLoudness) * soundboardAdditionalSanityGain);
		ApplySanityDelta(gain, "SoundboardRelief:" + soundTag);
	}

	void HandleThreatBandChanged(EnemyDistanceBand band)
	{
		previousBand = currentBand;
		currentBand = band;

		bool wasThreatened = previousBand == EnemyDistanceBand.Immediate || previousBand == EnemyDistanceBand.Danger;
		bool escapedThreat = band == EnemyDistanceBand.Near || band == EnemyDistanceBand.Far;
		if (!chaseActive && wasThreatened && escapedThreat)
		{
			ApplySanityDelta(-nearMissSanityLoss, "NearMiss");
		}
	}

	public void AddExternalSanity(float amount, string reason = "External")
	{
		ApplySanityDelta(amount, reason);
	}

	void ApplySanityDelta(float amount, string reason)
	{
		if (Mathf.Abs(amount) <= 0f)
		{
			return;
		}

		float before = currentSanity;
		currentSanity += amount;
		if (clampSanity)
		{
			currentSanity = Mathf.Clamp(currentSanity, 0f, maxSanity);
		}

		if (Mathf.Abs(currentSanity - before) >= 0.001f)
		{
			if (enableDebugLogs)
			{
				Debug.Log("SanitySystem " + reason + " delta: " + amount.ToString("F2") + " -> " + currentSanity.ToString("F1"));
			}
			BroadcastSanity();
		}
	}

	void BroadcastSanity()
	{
		float normalized = NormalizedSanity;
		HorrorEvents.RaiseSanityChanged(currentSanity, normalized, Stress01);

		bool isLow = normalized <= lowSanityThresholdNormalized;
		bool isCritical = normalized <= criticalSanityThresholdNormalized;
		if (isLow && !wasLowSanity)
		{
			HorrorEvents.RaiseSanityLow();
		}
		if (isCritical && !wasCriticalSanity)
		{
			HorrorEvents.RaiseSanityCritical();
		}

		wasLowSanity = isLow;
		wasCriticalSanity = isCritical;
	}
}
