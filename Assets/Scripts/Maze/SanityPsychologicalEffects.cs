using UnityEngine;
using Kino;

public class SanityPsychologicalEffects : MonoBehaviour
{
	[Header("References")]
	public SanitySystem sanitySystem;
	public AudioLowPassFilter listenerLowPass;
	public AudioSource falseCueAudioSource;
	public AudioClip[] falseCueClips;
	public AnalogGlitch analogGlitch;
	public DigitalGlitch digitalGlitch;
	public Camera playerCamera;
	public AudioSource tinnitusLoop;

	[Header("Feature Toggles")]
	public bool enableAudioDistortion = true;
	public bool enableFalseAudioCues = true;
	public bool enableVisualGlitches = true;
	public bool enableCameraInstability = true;
	public bool enableRinging = true;

	[Header("Thresholds")]
	[Range(0f, 1f)] public float distortionStressThreshold = 0.2f;
	[Range(0f, 1f)] public float falseCueStressThreshold = 0.45f;
	[Range(0f, 1f)] public float visualGlitchStressThreshold = 0.55f;
	[Range(0f, 1f)] public float severeStressThreshold = 0.72f;
	[Range(0f, 1f)] public float criticalStressThreshold = 0.88f;

	[Header("Audio Distortion")]
	public float distortionSmoothing = 2.5f;
	public float minCutoff = 420f;
	public float maxCutoff = 22000f;
	public float minResonanceQ = 0.8f;
	public float maxResonanceQ = 1.45f;

	[Header("Ringing")]
	public float maxRingingVolume = 0.55f;
	public float ringingFadeSpeed = 2.2f;

	[Header("False Audio Cue")]
	public Vector2 falseCueIntervalRange = new Vector2(8f, 16f);
	public Vector2 falseCueVolumeRange = new Vector2(0.25f, 0.7f);
	public bool use3DSpatialFalseCues = false;
	public float falseCueRadius = 6f;
	public Transform player;

	[Header("Visual Glitch")]
	public Vector2 glitchIntervalRange = new Vector2(5f, 11f);
	public Vector2 glitchDurationRange = new Vector2(0.05f, 0.18f);
	public Vector2 analogJitterRange = new Vector2(0.05f, 0.35f);
	public Vector2 analogColorDriftRange = new Vector2(0.02f, 0.2f);
	public Vector2 digitalIntensityRange = new Vector2(0.06f, 0.35f);
	public float maxBaselineDigitalIntensity = 0.2f;

	[Header("Camera Instability")]
	public float maxFovReduction = 10f;
	public float fovPulseAmplitude = 2.4f;
	public float fovPulseFrequency = 3.2f;
	public float maxTiltAngle = 1.8f;
	public float maxPositionShake = 0.018f;

	private float nextFalseCueTime = -999f;
	private float nextGlitchTime = -999f;
	private float glitchUntilTime = -999f;
	private float glitchTargetIntensity;
	private float stress01;
	private float baseFieldOfView = 60f;
	private Vector3 baseLocalPosition;
	private Quaternion baseLocalRotation;

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

		if (playerCamera == null)
		{
			playerCamera = Camera.main;
		}
		if (playerCamera != null)
		{
			if (analogGlitch == null)
			{
				analogGlitch = playerCamera.GetComponent<AnalogGlitch>();
			}
			if (digitalGlitch == null)
			{
				digitalGlitch = playerCamera.GetComponent<DigitalGlitch>();
			}
		}

		if (playerCamera != null)
		{
			baseFieldOfView = playerCamera.fieldOfView;
			baseLocalPosition = playerCamera.transform.localPosition;
			baseLocalRotation = playerCamera.transform.localRotation;
		}

		ResetGlitchValues();

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

	void LateUpdate()
	{
		if (sanitySystem != null)
		{
			stress01 = sanitySystem.Stress01;
		}

		UpdateAudioDistortion();
		UpdateRinging();
		UpdateFalseCues();
		UpdateVisualGlitches();
		UpdateCameraInstability();
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

		float severeBoost = stress01 >= severeStressThreshold ? Mathf.InverseLerp(severeStressThreshold, 1f, stress01) : 0f;
		t = Mathf.Clamp01(t + (severeBoost * 0.25f));

		float targetCutoff = Mathf.Lerp(maxCutoff, minCutoff, t);
		listenerLowPass.cutoffFrequency = Mathf.MoveTowards(listenerLowPass.cutoffFrequency, targetCutoff, Time.deltaTime * distortionSmoothing * 16000f);
		listenerLowPass.lowpassResonanceQ = Mathf.Lerp(minResonanceQ, maxResonanceQ, t);
	}

	void UpdateRinging()
	{
		if (tinnitusLoop == null || !enableRinging)
		{
			return;
		}

		float target = 0f;
		if (stress01 >= severeStressThreshold)
		{
			float t = Mathf.InverseLerp(severeStressThreshold, 1f, stress01);
			target = maxRingingVolume * t;
		}

		if (target > 0.01f && !tinnitusLoop.isPlaying)
		{
			tinnitusLoop.Play();
		}

		tinnitusLoop.volume = Mathf.MoveTowards(tinnitusLoop.volume, target, Time.deltaTime * ringingFadeSpeed);
		if (tinnitusLoop.volume <= 0.01f && tinnitusLoop.isPlaying)
		{
			tinnitusLoop.Stop();
		}
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
		float stressBias = Mathf.InverseLerp(falseCueStressThreshold, 1f, stress01);
		float volume = Mathf.Lerp(falseCueVolumeRange.x, falseCueVolumeRange.y, stressBias);
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
		if (analogGlitch == null && digitalGlitch == null)
		{
			return;
		}

		if (!enableVisualGlitches || stress01 < visualGlitchStressThreshold)
		{
			FadeOutGlitches();
			return;
		}

		if (Time.time >= nextGlitchTime && Time.time >= glitchUntilTime)
		{
			float escalatedIntensity = stress01 >= criticalStressThreshold ? digitalIntensityRange.y : Random.Range(digitalIntensityRange.x, digitalIntensityRange.y);
			glitchTargetIntensity = escalatedIntensity;
			glitchUntilTime = Time.time + Random.Range(glitchDurationRange.x, glitchDurationRange.y);
			ScheduleNextGlitch();
		}

		float stressT = Mathf.InverseLerp(visualGlitchStressThreshold, 1f, stress01);
		float baselineDigital = maxBaselineDigitalIntensity * stressT;
		float burstDigital = Time.time < glitchUntilTime ? glitchTargetIntensity : 0f;
		float digitalTarget = Mathf.Max(baselineDigital, burstDigital);

		float analogTarget = Mathf.Lerp(analogJitterRange.x, analogJitterRange.y, stressT);
		float colorDriftTarget = Mathf.Lerp(analogColorDriftRange.x, analogColorDriftRange.y, stressT);
		float severeBoost = stress01 >= severeStressThreshold ? Mathf.InverseLerp(severeStressThreshold, 1f, stress01) : 0f;
		analogTarget = Mathf.Clamp01(analogTarget + (severeBoost * 0.25f));
		colorDriftTarget = Mathf.Clamp01(colorDriftTarget + (severeBoost * 0.2f));

		if (analogGlitch != null)
		{
			analogGlitch.scanLineJitter = Mathf.MoveTowards(analogGlitch.scanLineJitter, analogTarget, Time.deltaTime * 1.8f);
			analogGlitch.horizontalShake = Mathf.MoveTowards(analogGlitch.horizontalShake, analogTarget * 0.7f, Time.deltaTime * 2.2f);
			analogGlitch.verticalJump = Mathf.MoveTowards(analogGlitch.verticalJump, analogTarget * 0.5f, Time.deltaTime * 2f);
			analogGlitch.colorDrift = Mathf.MoveTowards(analogGlitch.colorDrift, colorDriftTarget, Time.deltaTime * 2.4f);
		}

		if (digitalGlitch != null)
		{
			digitalGlitch.intensity = Mathf.MoveTowards(digitalGlitch.intensity, digitalTarget, Time.deltaTime * 2.8f);
		}
	}

	void FadeOutGlitches()
	{
		if (analogGlitch != null)
		{
			analogGlitch.scanLineJitter = Mathf.MoveTowards(analogGlitch.scanLineJitter, 0f, Time.deltaTime * 2f);
			analogGlitch.horizontalShake = Mathf.MoveTowards(analogGlitch.horizontalShake, 0f, Time.deltaTime * 2.4f);
			analogGlitch.verticalJump = Mathf.MoveTowards(analogGlitch.verticalJump, 0f, Time.deltaTime * 2.2f);
			analogGlitch.colorDrift = Mathf.MoveTowards(analogGlitch.colorDrift, 0f, Time.deltaTime * 2.5f);
		}

		if (digitalGlitch != null)
		{
			digitalGlitch.intensity = Mathf.MoveTowards(digitalGlitch.intensity, 0f, Time.deltaTime * 3f);
		}
	}

	void ResetGlitchValues()
	{
		if (analogGlitch != null)
		{
			analogGlitch.scanLineJitter = 0f;
			analogGlitch.horizontalShake = 0f;
			analogGlitch.verticalJump = 0f;
			analogGlitch.colorDrift = 0f;
		}

		if (digitalGlitch != null)
		{
			digitalGlitch.intensity = 0f;
		}
	}

	void UpdateCameraInstability()
	{
		if (playerCamera == null || !enableCameraInstability)
		{
			return;
		}

		float instability = Mathf.InverseLerp(visualGlitchStressThreshold, 1f, stress01);
		float criticalBoost = stress01 >= criticalStressThreshold ? Mathf.InverseLerp(criticalStressThreshold, 1f, stress01) : 0f;
		instability = Mathf.Clamp01(instability + (criticalBoost * 0.35f));

		float pulse = Mathf.Sin(Time.time * fovPulseFrequency) * fovPulseAmplitude * instability;
		float fovTarget = baseFieldOfView - (maxFovReduction * instability) + pulse;
		playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, fovTarget, Time.deltaTime * 7f);

		float tilt = Mathf.Sin(Time.time * (fovPulseFrequency * 0.7f)) * maxTiltAngle * instability;
		Quaternion targetRotation = baseLocalRotation * Quaternion.Euler(0f, 0f, tilt);
		playerCamera.transform.localRotation = Quaternion.Slerp(playerCamera.transform.localRotation, targetRotation, Time.deltaTime * 10f);

		Vector3 shakeOffset = Random.insideUnitSphere * (maxPositionShake * instability);
		shakeOffset.z = 0f;
		Vector3 targetPosition = baseLocalPosition + shakeOffset;
		playerCamera.transform.localPosition = Vector3.Lerp(playerCamera.transform.localPosition, targetPosition, Time.deltaTime * 11f);
	}

	void ScheduleNextFalseCue()
	{
		float stressT = Mathf.InverseLerp(falseCueStressThreshold, 1f, stress01);
		float minInterval = Mathf.Lerp(falseCueIntervalRange.x, falseCueIntervalRange.x * 0.45f, stressT);
		float maxInterval = Mathf.Lerp(falseCueIntervalRange.y, falseCueIntervalRange.y * 0.45f, stressT);
		nextFalseCueTime = Time.time + Random.Range(minInterval, maxInterval);
	}

	void ScheduleNextGlitch()
	{
		float stressT = Mathf.InverseLerp(visualGlitchStressThreshold, 1f, stress01);
		float minInterval = Mathf.Lerp(glitchIntervalRange.x, glitchIntervalRange.x * 0.35f, stressT);
		float maxInterval = Mathf.Lerp(glitchIntervalRange.y, glitchIntervalRange.y * 0.35f, stressT);
		nextGlitchTime = Time.time + Random.Range(minInterval, maxInterval);
	}
}
