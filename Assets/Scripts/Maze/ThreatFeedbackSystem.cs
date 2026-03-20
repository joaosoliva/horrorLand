using UnityEngine;
using UnityEngine.UI;

public class ThreatFeedbackSystem : MonoBehaviour
{
	[Header("References")]
	public VillainAI villainAI;
	public HorrorDirector horrorDirector;
	public Transform player;

	[Header("Distance Bands")]
	public float nearDistance = 18f;
	public float dangerDistance = 10f;
	public float immediateDistance = 5f;

	[Header("Visual Feedback")]
	public Image dangerVignette;
	public CanvasGroup dangerCanvasGroup;
	public Color vignetteColor = new Color(0.4f, 0f, 0f, 0.8f);
	public float peakPulseBoost = 0.2f;
	public float scarePulseDuration = 1f;

	[Header("Audio Feedback")]
	public AudioSource heartbeatLoop;
	public AudioSource proximityDrone;
	public AudioSource breathingLoop;
	public float fadeSpeed = 1.5f;

	[Header("Debug")]
	public bool logBandChanges = false;

	private EnemyDistanceBand currentBand = EnemyDistanceBand.Far;
	private float scarePulseUntilTime = -999f;
	private float peakBoostUntilTime = -999f;

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
		if (player == null)
		{
			GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
			if (playerObject != null)
			{
				player = playerObject.transform;
			}
		}

		ApplyVisuals(0f);
	}

	void OnEnable()
	{
		HorrorEvents.OnScareTriggered += HandleScareTriggered;
		HorrorEvents.OnMajorPeakStarted += HandleMajorPeakStarted;
		HorrorEvents.OnMajorPeakEnded += HandleMajorPeakEnded;
	}

	void OnDisable()
	{
		HorrorEvents.OnScareTriggered -= HandleScareTriggered;
		HorrorEvents.OnMajorPeakStarted -= HandleMajorPeakStarted;
		HorrorEvents.OnMajorPeakEnded -= HandleMajorPeakEnded;
	}

	void Update()
	{
		if (villainAI == null || player == null)
		{
			return;
		}

		float distance = Vector3.Distance(villainAI.transform.position, player.position);
		EnemyDistanceBand nextBand = ResolveBand(distance);
		if (nextBand != currentBand)
		{
			currentBand = nextBand;
			HorrorEvents.RaiseThreatBandChanged(currentBand);
			if (logBandChanges)
			{
				Debug.Log("ThreatFeedback band changed to " + currentBand);
			}
		}

		float bandIntensity = GetBandIntensity(currentBand);
		float tensionIntensity = horrorDirector != null ? horrorDirector.currentTension : 0f;
		float pulseBoost = Time.time < scarePulseUntilTime ? 0.2f : 0f;
		float peakBoost = Time.time < peakBoostUntilTime ? peakPulseBoost : 0f;
		float totalIntensity = Mathf.Clamp01((bandIntensity * 0.7f) + (tensionIntensity * 0.5f) + pulseBoost + peakBoost);

		ApplyVisuals(totalIntensity);
		ApplyAudio(totalIntensity);
	}

	EnemyDistanceBand ResolveBand(float distance)
	{
		if (distance <= immediateDistance)
		{
			return EnemyDistanceBand.Immediate;
		}
		if (distance <= dangerDistance)
		{
			return EnemyDistanceBand.Danger;
		}
		if (distance <= nearDistance)
		{
			return EnemyDistanceBand.Near;
		}

		return EnemyDistanceBand.Far;
	}

	float GetBandIntensity(EnemyDistanceBand band)
	{
		if (band == EnemyDistanceBand.Immediate)
		{
			return 1f;
		}
		if (band == EnemyDistanceBand.Danger)
		{
			return 0.75f;
		}
		if (band == EnemyDistanceBand.Near)
		{
			return 0.4f;
		}

		return 0.08f;
	}

	void ApplyVisuals(float intensity)
	{
		if (dangerVignette != null)
		{
			Color color = vignetteColor;
			color.a = vignetteColor.a * intensity;
			dangerVignette.color = color;
		}

		if (dangerCanvasGroup != null)
		{
			dangerCanvasGroup.alpha = intensity;
		}
	}

	void ApplyAudio(float intensity)
	{
		UpdateLoop(heartbeatLoop, intensity, 0.65f, 1.25f);
		UpdateLoop(proximityDrone, intensity * 0.8f, 0.8f, 1.1f);
		UpdateLoop(breathingLoop, intensity * 0.9f, 0.85f, 1.2f);
	}

	void UpdateLoop(AudioSource source, float targetVolume, float minPitch, float maxPitch)
	{
		if (source == null)
		{
			return;
		}

		if (targetVolume > 0.01f && !source.isPlaying)
		{
			source.Play();
		}

		source.volume = Mathf.MoveTowards(source.volume, targetVolume, Time.deltaTime * fadeSpeed);
		source.pitch = Mathf.Lerp(minPitch, maxPitch, targetVolume);
		if (source.volume <= 0.01f && source.isPlaying)
		{
			source.Stop();
		}
	}

	void HandleScareTriggered(ScareType scareType)
	{
		if (scareType == ScareType.MinorPsychological || scareType == ScareType.Fakeout || scareType == ScareType.PresenceCue || scareType == ScareType.RoutePressure)
		{
			scarePulseUntilTime = Time.time + scarePulseDuration;
		}
	}

	void HandleMajorPeakStarted()
	{
		peakBoostUntilTime = Time.time + 2f;
	}

	void HandleMajorPeakEnded()
	{
		peakBoostUntilTime = Time.time + 0.5f;
	}
}
