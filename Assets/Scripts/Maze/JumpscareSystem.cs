using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class JumpscareSystem : MonoBehaviour
{
	[Header("References")]
	public VillainAI villainAI;
	public Transform player;
	public ChaseSystem chaseSystem;
	public HorrorDirector horrorDirector;

	[Header("Scheduling")]
	public bool useDirectorDrivenScheduling = true;
	public float minJumpscareInterval = 30f;
	public float maxJumpscareInterval = 60f;
	public float triggerDistance = 10f;
	public float maxDistanceForJumpscare = 20f;
	public Vector2 directorVisibilityRetryWindow = new Vector2(4f, 8f);
	public bool enableLineOfSightSnapTrigger = true;
	public float lineOfSightSnapCooldown = 7f;
	public bool enableCloseProximitySnapTrigger = true;
	public float closeProximitySnapDistance = 5.5f;

	[Header("Warning System")]
	public bool enableWarning = true;
	public bool preferDiegeticWarnings = true;
	public bool showWarningTextForMinorScares = false;
	public float warningDuration = 1.5f;
	public Canvas warningCanvas;
	public TextMeshProUGUI warningText;
	public string warningMessage = "HE IS NEAR!";
	public Color warningColor = Color.red;
	public AudioClip diegeticWarningClip;
	public float diegeticWarningVolume = 0.6f;

	[Header("Major Jumpscare Visual")]
	public Canvas jumpscareCanvas;
	public Image jumpscareImage;
	public Sprite jumpscareSprite;
	public float jumpscareDuration = 0.5f;
	public int flashCount = 3;
	public AudioClip jumpscareSound;
	public float majorScareVolume = 1f;
	public float anticipationDelay = 0.09f;
	public float anticipationFlashAlpha = 0.25f;
	public float majorStingPitch = 1.08f;
	public float guaranteedCaptureJumpscareDelay = 0.01f;
	public bool guaranteedCaptureBypassesWarning = true;

	[Header("Minor Scare Visual")]
	public Color minorFlashColor = new Color(0.75f, 0f, 0f, 0.55f);
	public float minorFlashDuration = 0.18f;
	public float minorSoundVolume = 0.35f;
	public string fakeoutMessage = "...RUN...";
	public string presenceMessage = "YOU ARE NOT ALONE";
	public string routePressureMessage = "DON'T GO THAT WAY";

	[Header("Screen Effects")]
	public Image screenFlash;
	public Color flashColor = Color.red;
	public float flashIntensity = 0.8f;
	public Camera effectsCamera;
	public float majorCameraShakeDuration = 0.2f;
	public float majorCameraShakeAmount = 0.18f;

	[Header("Debug")]
	public bool enableDebugLogs = true;

	private float nextJumpscareTime;
	private bool isJumpscareActive = false;
	private bool isWarningActive = false;
	private AudioSource audioSource;
	private Coroutine currentJumpscareCoroutine;
	private Coroutine currentWarningCoroutine;
	private ScareType pendingScareType = ScareType.MajorJumpscare;
	private bool hadLineOfSightLastFrame = false;
	private float lastLineOfSightSnapTime = -999f;
	private float lastProximitySnapTime = -999f;
	private bool forceImmediateMajorJumpscare = false;

	void Start()
	{
		if (chaseSystem == null)
		{
			chaseSystem = FindObjectOfType<ChaseSystem>();
		}
		if (horrorDirector == null)
		{
			horrorDirector = FindObjectOfType<HorrorDirector>();
		}
		if (villainAI == null)
		{
			villainAI = FindObjectOfType<VillainAI>();
		}
		if (player == null)
		{
			GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
			if (playerObj != null)
			{
				player = playerObj.transform;
			}
		}

		audioSource = GetComponent<AudioSource>();
		if (audioSource == null)
		{
			audioSource = gameObject.AddComponent<AudioSource>();
		}

		InitializeUI();
		ResetJumpscareTimer();
	}

	void InitializeUI()
	{
		if (warningCanvas != null)
		{
			warningCanvas.gameObject.SetActive(false);
		}
		if (warningText != null)
		{
			warningText.color = warningColor;
			warningText.text = warningMessage;
		}
		if (jumpscareCanvas != null)
		{
			jumpscareCanvas.gameObject.SetActive(false);
		}
		if (jumpscareImage != null)
		{
			jumpscareImage.gameObject.SetActive(false);
			if (jumpscareSprite != null)
			{
				jumpscareImage.sprite = jumpscareSprite;
			}
		}
		if (screenFlash != null)
		{
			screenFlash.gameObject.SetActive(false);
			screenFlash.color = Color.clear;
		}
	}

	void Update()
	{
		if (villainAI == null || player == null || isJumpscareActive || isWarningActive)
		{
			return;
		}

		float distanceToVillain = Vector3.Distance(player.position, villainAI.transform.position);
		bool villainVisible = villainAI.CanSeePlayer();
		if (useDirectorDrivenScheduling)
		{
			if (TryImmediateThreatSnap(distanceToVillain, villainVisible))
			{
				return;
			}

			HandleDirectorDrivenMajorScare(distanceToVillain, villainVisible);
			return;
		}

		if (Time.time >= nextJumpscareTime)
		{
			if (distanceToVillain <= maxDistanceForJumpscare && distanceToVillain >= triggerDistance)
			{
				ForceMajorScare(enableWarning);
			}
			else
			{
				ResetJumpscareTimer();
			}
		}
	}

	bool TryImmediateThreatSnap(float distanceToVillain, bool villainVisible)
	{
		bool justGainedLineOfSight = villainVisible && !hadLineOfSightLastFrame;
		hadLineOfSightLastFrame = villainVisible;

		if (enableLineOfSightSnapTrigger && justGainedLineOfSight && Time.time - lastLineOfSightSnapTime >= lineOfSightSnapCooldown)
		{
			lastLineOfSightSnapTime = Time.time;
			if (enableDebugLogs)
			{
				Debug.Log("Immediate jumpscare snap trigger fired from fresh villain line-of-sight.");
			}
			ForceMajorScare(enableWarning);
			return true;
		}

		if (enableCloseProximitySnapTrigger && distanceToVillain <= closeProximitySnapDistance && Time.time - lastProximitySnapTime >= Mathf.Max(2f, lineOfSightSnapCooldown * 0.6f))
		{
			lastProximitySnapTime = Time.time;
			if (enableDebugLogs)
			{
				Debug.Log("Immediate jumpscare snap trigger fired from close villain proximity.");
			}
			ForceMajorScare(enableWarning);
			return true;
		}

		return false;
	}

	void HandleDirectorDrivenMajorScare(float distanceToVillain, bool villainVisible)
	{
		if (Time.time < nextJumpscareTime)
		{
			return;
		}

		bool withinDistanceWindow = distanceToVillain <= maxDistanceForJumpscare && distanceToVillain >= triggerDistance;
		bool contextualOpportunity = villainVisible || withinDistanceWindow;
		if (!contextualOpportunity)
		{
			RetrySoon();
			return;
		}

		if (horrorDirector != null && horrorDirector.CurrentPhase == HorrorPhase.Calm)
		{
			RetrySoon();
			return;
		}

		if (chaseSystem != null && !chaseSystem.CanTriggerContextualJumpscare(distanceToVillain))
		{
			RetrySoon();
			return;
		}

		if (enableDebugLogs)
		{
			Debug.Log("Director-driven major jumpscare triggered from visibility/proximity opportunity.");
		}

		ForceMajorScare(enableWarning);
	}

	public void ForceMajorScare(bool withWarning)
	{
		pendingScareType = ScareType.MajorJumpscare;
		if (withWarning && enableWarning)
		{
			StartWarning(GetMessageForScareType(ScareType.MajorJumpscare), warningColor, pendingScareType);
		}
		else
		{
			TriggerJumpscare(ScareType.MajorJumpscare);
		}
	}

	public void ForceCaptureJumpscareImmediate()
	{
		if (currentWarningCoroutine != null)
		{
			StopCoroutine(currentWarningCoroutine);
			currentWarningCoroutine = null;
		}
		if (currentJumpscareCoroutine != null)
		{
			StopCoroutine(currentJumpscareCoroutine);
			currentJumpscareCoroutine = null;
		}

		isWarningActive = false;
		isJumpscareActive = false;
		forceImmediateMajorJumpscare = true;
		pendingScareType = ScareType.MajorJumpscare;

		if (guaranteedCaptureBypassesWarning || !enableWarning)
		{
			TriggerJumpscare(ScareType.MajorJumpscare);
		}
		else
		{
			StartWarning(GetMessageForScareType(ScareType.MajorJumpscare), warningColor, ScareType.MajorJumpscare);
		}
	}

	public void ForceMinorScare(ScareType scareType)
	{
		if (isJumpscareActive)
		{
			return;
		}

		StartCoroutine(MinorScareRoutine(scareType));
	}

	IEnumerator MinorScareRoutine(ScareType scareType)
	{
		isJumpscareActive = true;
		HorrorEvents.RaiseScareTriggered(scareType);

		if (screenFlash != null)
		{
			screenFlash.gameObject.SetActive(true);
			float elapsed = 0f;
			while (elapsed < minorFlashDuration)
			{
				float t = elapsed / Mathf.Max(0.01f, minorFlashDuration);
				Color color = minorFlashColor;
				color.a = Mathf.Lerp(minorFlashColor.a, 0f, t);
				screenFlash.color = color;
				elapsed += Time.deltaTime;
				yield return null;
			}
			screenFlash.color = Color.clear;
			screenFlash.gameObject.SetActive(false);
		}

		if (showWarningTextForMinorScares && warningCanvas != null && warningText != null)
		{
			warningCanvas.gameObject.SetActive(true);
			warningText.text = GetMessageForScareType(scareType);
			warningText.color = minorFlashColor;
			yield return new WaitForSeconds(0.2f);
			warningCanvas.gameObject.SetActive(false);
			warningText.text = warningMessage;
			warningText.color = warningColor;
		}

		if (jumpscareSound != null && audioSource != null)
		{
			audioSource.PlayOneShot(jumpscareSound, minorSoundVolume);
		}

		yield return new WaitForSeconds(0.05f);
		isJumpscareActive = false;
	}

	void StartWarning(string message, Color color, ScareType scareType)
	{
		if (isWarningActive || isJumpscareActive)
		{
			return;
		}

		pendingScareType = scareType;
		if (preferDiegeticWarnings)
		{
			currentWarningCoroutine = StartCoroutine(DiegeticWarningRoutine());
			return;
		}
		currentWarningCoroutine = StartCoroutine(WarningRoutine(message, color));
	}

	IEnumerator DiegeticWarningRoutine()
	{
		isWarningActive = true;
		PlayDiegeticWarning();
		yield return new WaitForSeconds(Mathf.Clamp(warningDuration * 0.55f, 0.2f, warningDuration));
		isWarningActive = false;
		TriggerJumpscare(pendingScareType);
	}

	IEnumerator WarningRoutine(string message, Color color)
	{
		isWarningActive = true;
		if (warningCanvas != null)
		{
			warningCanvas.gameObject.SetActive(true);
		}
		if (warningText != null)
		{
			warningText.text = message;
		}

		float elapsed = 0f;
		while (elapsed < warningDuration)
		{
			if (warningText != null)
			{
				Color pulseColor = color;
				pulseColor.a = Mathf.PingPong(elapsed * 5f, 1f);
				warningText.color = pulseColor;
			}
			elapsed += Time.deltaTime;
			yield return null;
		}

		if (warningCanvas != null)
		{
			warningCanvas.gameObject.SetActive(false);
		}
		if (warningText != null)
		{
			warningText.text = warningMessage;
			warningText.color = warningColor;
		}

		isWarningActive = false;
		TriggerJumpscare(pendingScareType);
	}

	void TriggerJumpscare(ScareType scareType)
	{
		if (isJumpscareActive)
		{
			return;
		}

		if (scareType == ScareType.MajorJumpscare && chaseSystem != null)
		{
			chaseSystem.ConsumeJumpscareBudget();
		}

		HorrorEvents.RaiseScareTriggered(scareType);
		if (scareType == ScareType.MajorJumpscare)
		{
			HorrorEvents.RaiseJumpscareTriggered();
		}
		currentJumpscareCoroutine = StartCoroutine(JumpscareRoutine(scareType));
		ResetJumpscareTimer();
	}

	IEnumerator JumpscareRoutine(ScareType scareType)
	{
		isJumpscareActive = true;
		bool immediateCaptureScare = forceImmediateMajorJumpscare && scareType == ScareType.MajorJumpscare;
		forceImmediateMajorJumpscare = false;

		if (jumpscareSound != null && audioSource != null)
		{
			audioSource.pitch = scareType == ScareType.MajorJumpscare ? majorStingPitch : 1f;
			audioSource.PlayOneShot(jumpscareSound, scareType == ScareType.MajorJumpscare ? majorScareVolume : minorSoundVolume);
			audioSource.pitch = 1f;
		}

		if (scareType == ScareType.MajorJumpscare && jumpscareImage != null && jumpscareSprite != null)
		{
			float effectiveAnticipationDelay = immediateCaptureScare ? guaranteedCaptureJumpscareDelay : anticipationDelay;
			if (screenFlash != null && effectiveAnticipationDelay > 0.001f)
			{
				screenFlash.gameObject.SetActive(true);
				Color anticipationColor = flashColor;
				anticipationColor.a = Mathf.Clamp01(anticipationFlashAlpha);
				screenFlash.color = anticipationColor;
				yield return new WaitForSeconds(effectiveAnticipationDelay);
			}

			if (jumpscareCanvas != null)
			{
				jumpscareCanvas.gameObject.SetActive(true);
			}

			for (int i = 0; i < flashCount; i++)
			{
				jumpscareImage.gameObject.SetActive(true);
				yield return new WaitForSeconds(jumpscareDuration / (flashCount * 2f));
				jumpscareImage.gameObject.SetActive(false);
				yield return new WaitForSeconds(jumpscareDuration / (flashCount * 2f));
			}

			if (jumpscareCanvas != null)
			{
				jumpscareCanvas.gameObject.SetActive(false);
			}

			yield return StartCoroutine(ApplyCameraShake(majorCameraShakeDuration, majorCameraShakeAmount));
		}

		if (screenFlash != null)
		{
			screenFlash.gameObject.SetActive(true);
			float flashTime = scareType == ScareType.MajorJumpscare ? jumpscareDuration * 0.5f : minorFlashDuration;
			Color baseColor = scareType == ScareType.MajorJumpscare ? flashColor : minorFlashColor;
			float startAlpha = scareType == ScareType.MajorJumpscare ? flashIntensity : minorFlashColor.a;
			float elapsed = 0f;
			while (elapsed < flashTime)
			{
				float t = elapsed / Mathf.Max(0.01f, flashTime);
				Color color = baseColor;
				color.a = Mathf.Lerp(startAlpha, 0f, t);
				screenFlash.color = color;
				elapsed += Time.deltaTime;
				yield return null;
			}
			screenFlash.color = Color.clear;
			screenFlash.gameObject.SetActive(false);
		}

		isJumpscareActive = false;
	}

	IEnumerator ApplyCameraShake(float duration, float amount)
	{
		Camera targetCamera = effectsCamera != null ? effectsCamera : Camera.main;
		if (targetCamera == null || duration <= 0f || amount <= 0f)
		{
			yield break;
		}

		Transform camTransform = targetCamera.transform;
		Vector3 originalLocalPosition = camTransform.localPosition;
		float elapsed = 0f;
		while (elapsed < duration)
		{
			float t = elapsed / duration;
			float damper = 1f - t;
			Vector2 random = Random.insideUnitCircle * amount * damper;
			camTransform.localPosition = originalLocalPosition + new Vector3(random.x, random.y, 0f);
			elapsed += Time.deltaTime;
			yield return null;
		}

		camTransform.localPosition = originalLocalPosition;
	}

	void ResetJumpscareTimer()
	{
		nextJumpscareTime = Time.time + Random.Range(minJumpscareInterval, Mathf.Max(minJumpscareInterval, maxJumpscareInterval));
	}

	void RetrySoon()
	{
		nextJumpscareTime = Time.time + Random.Range(
			Mathf.Min(directorVisibilityRetryWindow.x, directorVisibilityRetryWindow.y),
			Mathf.Max(directorVisibilityRetryWindow.x, directorVisibilityRetryWindow.y));
	}

	void PlayDiegeticWarning()
	{
		if (audioSource != null && diegeticWarningClip != null)
		{
			audioSource.PlayOneShot(diegeticWarningClip, Mathf.Clamp01(diegeticWarningVolume));
		}
		else if (audioSource != null && jumpscareSound != null)
		{
			audioSource.PlayOneShot(jumpscareSound, 0.3f);
		}
	}

	string GetMessageForScareType(ScareType scareType)
	{
		if (scareType == ScareType.Fakeout)
		{
			return fakeoutMessage;
		}
		if (scareType == ScareType.PresenceCue)
		{
			return presenceMessage;
		}
		if (scareType == ScareType.RoutePressure)
		{
			return routePressureMessage;
		}

		return warningMessage;
	}

	public bool IsJumpscareActive()
	{
		return isJumpscareActive || isWarningActive;
	}

	[ContextMenu("Test Major Jumpscare")]
	public void TestJumpscare()
	{
		ForceMajorScare(enableWarning);
	}

	[ContextMenu("Test Minor Scare")]
	public void TestMinorScare()
	{
		ForceMinorScare(ScareType.MinorPsychological);
	}
}
