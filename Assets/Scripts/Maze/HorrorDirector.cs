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

	[Header("Tension Model")]
	[Range(0f, 1f)] public float currentTension = 0.1f;
	public float proximityRange = 20f;
	public float chaseIncreasePerSecond = 0.35f;
	public float passiveIncreasePerSecond = 0.05f;
	public float decayPerSecond = 0.08f;
	public float recoveryDecayPerSecond = 0.18f;
	public float scareMemorySeconds = 8f;
	public float soundboardContributionWindow = 4f;

	[Header("Pacing Bands")]
	[Range(0f, 1f)] public float uneasyThreshold = 0.3f;
	[Range(0f, 1f)] public float panicThreshold = 0.7f;
	[Range(0f, 1f)] public float recoveryThreshold = 0.2f;

	[Header("Debug")]
	public bool logBandChanges = false;

	public PacingBand CurrentBand => currentBand;
	public bool IsChaseActive => chaseActive;

	private PacingBand currentBand = PacingBand.Calm;
	private float lastScareTime = -999f;
	private float lastSoundboardTime = -999f;
	private float lastSoundboardLoudness = 0f;
	private bool chaseActive = false;
	private float lastBroadcastTension = -1f;

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

		RefreshBand(forceBroadcast: true);
	}

	void OnEnable()
	{
		HorrorEvents.OnChaseStarted += HandleChaseStarted;
		HorrorEvents.OnChaseEnded += HandleChaseEnded;
		HorrorEvents.OnSoundboardPlayed += HandleSoundboardPlayed;
	}

	void OnDisable()
	{
		HorrorEvents.OnChaseStarted -= HandleChaseStarted;
		HorrorEvents.OnChaseEnded -= HandleChaseEnded;
		HorrorEvents.OnSoundboardPlayed -= HandleSoundboardPlayed;
	}

	void Update()
	{
		float deltaTime = Time.deltaTime;
		float proximityTension = GetProximityTension();
		float scareWeight = GetRecentScareWeight();
		float soundboardWeight = GetRecentSoundboardWeight();

		float increaseRate = passiveIncreasePerSecond * (0.35f + proximityTension + scareWeight + soundboardWeight);
		if (chaseActive)
		{
			increaseRate += chaseIncreasePerSecond;
		}

		float decayRate = currentBand == PacingBand.Recovery ? recoveryDecayPerSecond : decayPerSecond;
		float targetTension = Mathf.Clamp01(proximityTension * 0.55f + scareWeight * 0.25f + soundboardWeight * 0.2f + (chaseActive ? 0.35f : 0f));
		float tensionVelocity = currentTension < targetTension ? increaseRate : -decayRate;

		currentTension = Mathf.Clamp01(currentTension + tensionVelocity * deltaTime);
		if (!chaseActive && currentTension > targetTension)
		{
			currentTension = Mathf.MoveTowards(currentTension, targetTension, decayRate * deltaTime);
		}

		RefreshBand(forceBroadcast: false);
	}

	void HandleChaseStarted()
	{
		chaseActive = true;
		lastScareTime = Time.time;
	}

	void HandleChaseEnded()
	{
		chaseActive = false;
		lastScareTime = Time.time;
	}

	void HandleSoundboardPlayed(string soundTag, float loudness)
	{
		lastSoundboardTime = Time.time;
		lastSoundboardLoudness = loudness;
		lastScareTime = Time.time;
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
		float elapsed = Time.time - lastSoundboardTime;
		if (elapsed >= soundboardContributionWindow)
		{
			return 0f;
		}

		float freshness = 1f - Mathf.Clamp01(elapsed / soundboardContributionWindow);
		return freshness * lastSoundboardLoudness;
	}

	void RefreshBand(bool forceBroadcast)
	{
		PacingBand previousBand = currentBand;

		if (!chaseActive && previousBand == PacingBand.Panic && currentTension <= panicThreshold)
		{
			currentBand = PacingBand.Recovery;
		}
		else if (!chaseActive && previousBand == PacingBand.Recovery && currentTension <= recoveryThreshold)
		{
			currentBand = PacingBand.Calm;
		}
		else if (currentTension >= panicThreshold || chaseActive)
		{
			currentBand = PacingBand.Panic;
		}
		else if (currentTension >= uneasyThreshold)
		{
			currentBand = PacingBand.Uneasy;
		}
		else
		{
			currentBand = PacingBand.Calm;
		}

		bool tensionChangedEnough = forceBroadcast || Mathf.Abs(currentTension - lastBroadcastTension) >= 0.01f;
		bool bandChanged = previousBand != currentBand;
		if (tensionChangedEnough || bandChanged)
		{
			lastBroadcastTension = currentTension;
			HorrorEvents.RaiseTensionChanged(currentTension);
			if (bandChanged && logBandChanges)
			{
				Debug.Log($"HorrorDirector band changed: {previousBand} -> {currentBand} at tension {currentTension:F2}");
			}
		}
	}
}
