using UnityEngine;
using UnityEngine.UI;

public class SanityPsychologicalEffects : MonoBehaviour
{
	[Header("References")]
	public SanitySystem sanitySystem;
	public AudioLowPassFilter listenerLowPass;
	public AudioSource falseCueAudioSource;
	public AudioClip[] falseCueClips;
	public CanvasGroup glitchCanvasGroup;
	public Image glitchImage;

	[Header("Feature Toggles")]
	public bool enableAudioDistortion = true;
	public bool enableFalseAudioCues = true;
	public bool enableVisualGlitches = true;

	[Header("Thresholds")]
	[Range(0f, 1f)] public float distortionStressThreshold = 0.3f;
	[Range(0f, 1f)] public float falseCueStressThreshold = 0.5f;
	[Range(0f, 1f)] public float visualGlitchStressThreshold = 0.65f;

	[Header("Audio Distortion")]
	public float distortionSmoothing = 2f;
	public float minCutoff = 680f;
	public float maxCutoff = 22000f;
	public float minResonanceQ = 0.8f;
	public float maxResonanceQ = 1.35f;

	[Header("False Audio Cue")]
	public Vector2 falseCueIntervalRange = new Vector2(8f, 16f);
	public Vector2 falseCueVolumeRange = new Vector2(0.25f, 0.7f);
	public bool use3DSpatialFalseCues = false;
	public float falseCueRadius = 6f;
	public Transform player;

	[Header("Visual Glitch")]
	public Vector2 glitchIntervalRange = new Vector2(5f, 11f);
	public Vector2 glitchDurationRange = new Vector2(0.05f, 0.18f);
	public Vector2 glitchAlphaRange = new Vector2(0.1f, 0.3f);
	public Color glitchColor = new Color(1f, 1f, 1f, 0.2f);

	private float nextFalseCueTime = -999f;
	private float nextGlitchTime = -999f;
	private float glitchUntilTime = -999f;
	private float glitchTargetAlpha;
	private float stress01;

	void Start()
	{
		if (sanitySystem == null)
		{
			sanitySystem = FindObjectOfType<SanitySystem>();
		}

		if (player == null)
		{
			GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
			if (playerObject != null)
			{
				player = playerObject.transform;
			}
		}

		if (listenerLowPass == null)
		{
			listenerLowPass = FindObjectOfType<AudioLowPassFilter>();
		}

		if (glitchImage != null)
		{
			glitchImage.color = glitchColor;
		}

		if (glitchCanvasGroup != null)
		{
			glitchCanvasGroup.alpha = 0f;
		}

		ScheduleNextFalseCue();
		ScheduleNextGlitch();
	}

	void OnEnable()
	{
		HorrorEvents.OnSanityChanged += HandleSanityChanged;
	}

	void OnDisable()
	{
		HorrorEvents.OnSanityChanged -= HandleSanityChanged;
	}

	void Update()
	{
		if (sanitySystem != null)
		{
			stress01 = sanitySystem.Stress01;
		}

		UpdateAudioDistortion();
		UpdateFalseCues();
		UpdateVisualGlitches();
	}

	void HandleSanityChanged(float currentSanity, float normalizedSanity, float stress)
	{
		stress01 = stress;
	}

	void UpdateAudioDistortion()
	{
		if (listenerLowPass == null || !enableAudioDistortion)
		{
			return;
		}

		float t = 0f;
		if (stress01 >= distortionStressThreshold)
		{
			t = Mathf.InverseLerp(distortionStressThreshold, 1f, stress01);
		}

		float targetCutoff = Mathf.Lerp(maxCutoff, minCutoff, t);
		listenerLowPass.cutoffFrequency = Mathf.MoveTowards(listenerLowPass.cutoffFrequency, targetCutoff, Time.deltaTime * distortionSmoothing * 12000f);
		listenerLowPass.lowpassResonanceQ = Mathf.Lerp(minResonanceQ, maxResonanceQ, t);
	}

	void UpdateFalseCues()
	{
		if (!enableFalseAudioCues || falseCueAudioSource == null || falseCueClips == null || falseCueClips.Length == 0)
		{
			return;
		}

		if (stress01 < falseCueStressThreshold)
		{
			return;
		}

		if (Time.time < nextFalseCueTime)
		{
			return;
		}

		AudioClip cue = falseCueClips[Random.Range(0, falseCueClips.Length)];
		float volume = Random.Range(falseCueVolumeRange.x, falseCueVolumeRange.y);
		if (use3DSpatialFalseCues && player != null)
		{
			Vector2 offset = Random.insideUnitCircle * falseCueRadius;
			Vector3 worldPosition = player.position + new Vector3(offset.x, 0f, offset.y);
			AudioSource.PlayClipAtPoint(cue, worldPosition, volume);
		}
		else
		{
			falseCueAudioSource.PlayOneShot(cue, volume);
		}

		ScheduleNextFalseCue();
	}

	void UpdateVisualGlitches()
	{
		if (glitchCanvasGroup == null)
		{
			return;
		}

		if (!enableVisualGlitches || stress01 < visualGlitchStressThreshold)
		{
			glitchCanvasGroup.alpha = Mathf.MoveTowards(glitchCanvasGroup.alpha, 0f, Time.deltaTime * 6f);
			return;
		}

		if (Time.time >= nextGlitchTime && Time.time >= glitchUntilTime)
		{
			glitchTargetAlpha = Random.Range(glitchAlphaRange.x, glitchAlphaRange.y);
			glitchUntilTime = Time.time + Random.Range(glitchDurationRange.x, glitchDurationRange.y);
			ScheduleNextGlitch();
		}

		float target = Time.time < glitchUntilTime ? glitchTargetAlpha : 0f;
		glitchCanvasGroup.alpha = Mathf.MoveTowards(glitchCanvasGroup.alpha, target, Time.deltaTime * 14f);
	}

	void ScheduleNextFalseCue()
	{
		nextFalseCueTime = Time.time + Random.Range(falseCueIntervalRange.x, falseCueIntervalRange.y);
	}

	void ScheduleNextGlitch()
	{
		nextGlitchTime = Time.time + Random.Range(glitchIntervalRange.x, glitchIntervalRange.y);
	}
}
