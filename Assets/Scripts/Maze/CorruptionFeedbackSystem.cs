using Kino;
using UnityEngine;

public class CorruptionFeedbackSystem : MonoBehaviour
{
	[Header("References")]
	public CorruptionSystem corruptionSystem;
	public Camera playerCamera;
	public DigitalGlitch digitalGlitch;

	[Header("Digital Glitch Mapping")]
	[Range(0f, 1f)] public float baseDigitalIntensity = 0.02f;
	[Range(0f, 1f)] public float maxDigitalIntensity = 0.45f;
	public AnimationCurve intensityByCorruption = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
	public float blendSpeed = 2.8f;

	[Header("Corruption Event Bursts")]
	public string intenseGlitchEventId = "IntenseVisualGlitch";
	[Range(0f, 1f)] public float intenseBurstIntensity = 0.9f;
	public float intenseBurstDuration = 0.35f;

	private float corruption01;
	private float burstUntilTime = -999f;

	void Start()
	{
		if (corruptionSystem == null)
		{
			corruptionSystem = FindObjectOfType<CorruptionSystem>();
		}

		if (playerCamera == null)
		{
			playerCamera = Camera.main;
		}

		if (digitalGlitch == null && playerCamera != null)
		{
			digitalGlitch = playerCamera.GetComponent<DigitalGlitch>();
		}

		if (corruptionSystem != null)
		{
			corruption01 = corruptionSystem.NormalizedCorruption;
		}

		if (digitalGlitch != null)
		{
			digitalGlitch.intensity = 0f;
		}
	}

	void OnEnable()
	{
		HorrorEvents.OnCorruptionChanged += HandleCorruptionChanged;
		HorrorEvents.OnCorruptionEventTriggered += HandleCorruptionEventTriggered;
	}

	void OnDisable()
	{
		HorrorEvents.OnCorruptionChanged -= HandleCorruptionChanged;
		HorrorEvents.OnCorruptionEventTriggered -= HandleCorruptionEventTriggered;
	}

	void Update()
	{
		if (digitalGlitch == null)
		{
			return;
		}

		float mapped = Mathf.Lerp(baseDigitalIntensity, maxDigitalIntensity, intensityByCorruption.Evaluate(corruption01));
		float burst = Time.time < burstUntilTime ? intenseBurstIntensity : 0f;
		float target = Mathf.Clamp01(Mathf.Max(mapped, burst));
		digitalGlitch.intensity = Mathf.MoveTowards(digitalGlitch.intensity, target, Time.deltaTime * blendSpeed);
	}

	void HandleCorruptionChanged(float currentCorruption, float normalizedCorruption)
	{
		corruption01 = Mathf.Clamp01(normalizedCorruption);
	}

	public void TriggerTutorialCorruptionBurst(float normalizedCorruption = 0.75f)
	{
		corruption01 = Mathf.Max(corruption01, Mathf.Clamp01(normalizedCorruption));
		burstUntilTime = Time.time + intenseBurstDuration;
	}

	void HandleCorruptionEventTriggered(string eventId, float normalizedCorruption)
	{
		if (eventId != intenseGlitchEventId)
		{
			return;
		}

		TriggerTutorialCorruptionBurst(normalizedCorruption);
	}
}
