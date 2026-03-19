using UnityEngine;

[RequireComponent(typeof(VillainAI))]
public class VillainAudio : MonoBehaviour
{
	[Header("Audio Sources")]
	public AudioSource ambientHum;
	public AudioSource distortionStatic;
	public AudioSource heartbeat;
	public AudioSource tensionBurst;

	[Header("Distance Settings")]
	public Transform player;
	public float maxDistance = 25f;

	[Header("Volume Curve")]
	public AnimationCurve intensityCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

	[Header("Layer Thresholds")]
	public float distortionStart = 15f;
	public float heartbeatStart = 10f;
	public float burstTrigger = 5f;
	private bool hasBurstPlayed = false;

	private VillainAI villain;

	void Start()
	{
		villain = GetComponent<VillainAI>();
		if (player == null && villain != null)
			player = villain.player;

		StopAll();
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

	void StopAll()
	{
		ambientHum?.Stop();
		distortionStatic?.Stop();
		heartbeat?.Stop();
		tensionBurst?.Stop();
	}

	void Update()
	{
		if (player == null) return;

		float distance = Vector3.Distance(transform.position, player.position);
		float t = Mathf.Clamp01(1f - (distance / maxDistance)); // 0 = far, 1 = close
		float intensity = intensityCurve.Evaluate(t);

		HandleLayer(ambientHum, intensity * 0.5f, 0.8f, 1.1f);

		if (distance < distortionStart)
			HandleLayer(distortionStatic, Mathf.Lerp(0f, 0.8f, intensity), 0.95f, 1.2f);
		else
			FadeOut(distortionStatic);

		if (distance < heartbeatStart)
			HandleLayer(heartbeat, Mathf.Lerp(0f, 1f, intensity), 0.8f, 1.3f);
		else
			FadeOut(heartbeat);

		// Trigger burst once per approach
		if (distance < burstTrigger && !hasBurstPlayed)
		{
			if (tensionBurst != null)
			{
				tensionBurst.PlayOneShot(tensionBurst.clip, 1f);
				hasBurstPlayed = true;
			}
		}
		else if (distance >= burstTrigger + 3f)
		{
			hasBurstPlayed = false; // reset when player escapes
		}
	}


	void HandleChaseStarted()
	{
		if (tensionBurst != null && tensionBurst.clip != null)
		{
			tensionBurst.PlayOneShot(tensionBurst.clip, 1f);
		}
		hasBurstPlayed = true;
	}

	void HandleChaseEnded()
	{
		hasBurstPlayed = false;
	}
	void HandleLayer(AudioSource src, float targetVolume, float minPitch, float maxPitch)
	{
		if (src == null) return;

		if (!src.isPlaying)
			src.Play();

		src.volume = Mathf.MoveTowards(src.volume, targetVolume, Time.deltaTime);
		src.pitch = Mathf.Lerp(minPitch, maxPitch, src.volume);
	}

	void FadeOut(AudioSource src)
	{
		if (src == null) return;
		src.volume = Mathf.MoveTowards(src.volume, 0f, Time.deltaTime * 2f);
		if (src.volume <= 0.01f && src.isPlaying)
			src.Stop();
	}
}
