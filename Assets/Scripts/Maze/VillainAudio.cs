using UnityEngine;

[RequireComponent(typeof(VillainAI))]
public class VillainAudio : MonoBehaviour
{
	[Header("Audio Sources")]
	public AudioSource ambientHum;
	public AudioSource distortionStatic;
	public AudioSource heartbeat;
	public AudioSource tensionBurst;
	public AudioSource reliefBed;
	public AudioSource cueOneShotSource;

	[Header("Intent Cues")]
	public AudioClip[] stalkingCues;
	public AudioClip[] approachCues;
	public AudioClip[] searchCues;
	public AudioClip[] fakeoutCues;
	public AudioClip[] releaseCues;
	public float cueCooldown = 3.5f;
	public float cueBaseVolume = 0.5f;

	[Header("References")]
	public Transform player;
	public HorrorDirector horrorDirector;

	[Header("Distance Settings")]
	public float maxDistance = 25f;

	[Header("Volume Curve")]
	public AnimationCurve intensityCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

	[Header("Layer Thresholds")]
	public float distortionStart = 15f;
	public float heartbeatStart = 10f;
	public float burstTrigger = 5f;

	private bool hasBurstPlayed;
	private VillainAI villain;
	private HorrorPhase currentPhase = HorrorPhase.Calm;
	private float currentTension;
	private float lastCueTime = -999f;

	void Start()
	{
		villain = GetComponent<VillainAI>();
		if (player == null && villain != null)
		{
			player = villain.player;
		}
		if (horrorDirector == null)
		{
			horrorDirector = FindObjectOfType<HorrorDirector>();
		}
		if (cueOneShotSource == null)
		{
			cueOneShotSource = tensionBurst;
		}

		StopAll();
	}

	void OnEnable()
	{
		HorrorEvents.OnChaseStarted += HandleChaseStarted;
		HorrorEvents.OnChaseEnded += HandleChaseEnded;
		HorrorEvents.OnPhaseChanged += HandlePhaseChanged;
		HorrorEvents.OnTensionChanged += HandleTensionChanged;
		HorrorEvents.OnScareTriggered += HandleScareTriggered;
	}

	void OnDisable()
	{
		HorrorEvents.OnChaseStarted -= HandleChaseStarted;
		HorrorEvents.OnChaseEnded -= HandleChaseEnded;
		HorrorEvents.OnPhaseChanged -= HandlePhaseChanged;
		HorrorEvents.OnTensionChanged -= HandleTensionChanged;
		HorrorEvents.OnScareTriggered -= HandleScareTriggered;
	}

	void StopAll()
	{
		StopSource(ambientHum);
		StopSource(distortionStatic);
		StopSource(heartbeat);
		StopSource(reliefBed);
	}

	void Update()
	{
		if (player == null)
		{
			return;
		}

		float distance = Vector3.Distance(transform.position, player.position);
		float proximity = Mathf.Clamp01(1f - (distance / Mathf.Max(0.01f, maxDistance)));
		float intensity = intensityCurve.Evaluate(Mathf.Clamp01((proximity * 0.7f) + (currentTension * 0.6f)));

		HandleLayer(ambientHum, Mathf.Lerp(0.12f, 0.45f, currentTension), 0.9f, 1.05f);

		if (distance < distortionStart || currentPhase == HorrorPhase.Threat || currentPhase == HorrorPhase.Peak || currentPhase == HorrorPhase.Finale)
		{
			HandleLayer(distortionStatic, Mathf.Lerp(0f, 0.7f, intensity), 0.92f, 1.12f);
		}
		else
		{
			FadeOut(distortionStatic);
		}

		if (distance < heartbeatStart || currentPhase == HorrorPhase.Peak || currentPhase == HorrorPhase.Finale)
		{
			HandleLayer(heartbeat, Mathf.Lerp(0f, 0.95f, intensity), 0.8f, 1.25f);
		}
		else
		{
			FadeOut(heartbeat);
		}

		if (currentPhase == HorrorPhase.Relief && !villain.IsChasing)
		{
			HandleLayer(reliefBed, 0.25f, 0.95f, 1.05f);
		}
		else
		{
			FadeOut(reliefBed);
		}

		if (distance < burstTrigger && !hasBurstPlayed)
		{
			PlayBurst(1f);
			hasBurstPlayed = true;
		}
		else if (distance >= burstTrigger + 3f)
		{
			hasBurstPlayed = false;
		}
	}

	void HandleChaseStarted()
	{
		PlayBurst(1f);
		PlayCue(approachCues, 0.7f);
		hasBurstPlayed = true;
	}

	void HandleChaseEnded()
	{
		PlayCue(searchCues, 0.6f);
		PlayCue(releaseCues, 0.55f);
		hasBurstPlayed = false;
	}

	void HandlePhaseChanged(HorrorPhase phase)
	{
		currentPhase = phase;
		if (phase == HorrorPhase.Build)
		{
			PlayCue(stalkingCues, 0.45f);
		}
		else if (phase == HorrorPhase.Threat || phase == HorrorPhase.Peak)
		{
			PlayCue(approachCues, 0.6f);
		}
		else if (phase == HorrorPhase.Relief)
		{
			PlayCue(releaseCues, 0.45f);
		}
	}

	void HandleTensionChanged(float tension)
	{
		currentTension = tension;
	}

	void HandleScareTriggered(ScareType scareType)
	{
		if (scareType == ScareType.MajorJumpscare || scareType == ScareType.Fakeout || scareType == ScareType.ChaseTrigger)
		{
			PlayBurst(0.85f);
		}
		if (scareType == ScareType.Fakeout || scareType == ScareType.PresenceCue)
		{
			PlayCue(fakeoutCues, 0.5f);
		}
		if (scareType == ScareType.RoutePressure)
		{
			PlayCue(approachCues, 0.65f);
		}
	}

	void PlayBurst(float volume)
	{
		if (tensionBurst != null && tensionBurst.clip != null)
		{
			tensionBurst.PlayOneShot(tensionBurst.clip, volume);
		}
	}

	void HandleLayer(AudioSource src, float targetVolume, float minPitch, float maxPitch)
	{
		if (src == null)
		{
			return;
		}

		if (!src.isPlaying)
		{
			src.Play();
		}

		src.volume = Mathf.MoveTowards(src.volume, targetVolume, Time.deltaTime);
		src.pitch = Mathf.Lerp(minPitch, maxPitch, Mathf.Clamp01(targetVolume));
	}

	void FadeOut(AudioSource src)
	{
		if (src == null)
		{
			return;
		}

		src.volume = Mathf.MoveTowards(src.volume, 0f, Time.deltaTime * 2f);
		if (src.volume <= 0.01f && src.isPlaying)
		{
			src.Stop();
		}
	}

	void StopSource(AudioSource src)
	{
		if (src != null)
		{
			src.Stop();
		}
	}

	void PlayCue(AudioClip[] clips, float volumeScale)
	{
		if (clips == null || clips.Length == 0 || cueOneShotSource == null)
		{
			return;
		}

		if (Time.time - lastCueTime < cueCooldown)
		{
			return;
		}

		AudioClip clip = clips[Random.Range(0, clips.Length)];
		if (clip == null)
		{
			return;
		}

		cueOneShotSource.PlayOneShot(clip, Mathf.Clamp01(cueBaseVolume * volumeScale));
		lastCueTime = Time.time;
	}
}
