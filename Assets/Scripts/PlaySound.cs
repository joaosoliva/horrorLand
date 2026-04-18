using System;
using UnityEngine;

public class PlaySound : MonoBehaviour
{
	[Serializable]
	public class SoundboardSlot
	{
		public string tag = "Generic";
		public KeyCode key = KeyCode.None;
		public AudioClip clip;
		[Range(0f, 1f)] public float loudness = 0.6f;
		[Range(0f, 1f)] public float humorReliefValue = 0.3f;
		[Range(0f, 1f)] public float threatAttraction = 0.6f;
		[Range(0f, 1f)] public float misfireChance = 0f;
		public float cooldownSeconds = 5f;
	}

	[Header("Legacy Fallback")]
	public AudioClip[] soundClips;

	[Header("Soundboard")]
	public SoundboardSlot[] soundboardSlots = new SoundboardSlot[4];
	public bool raiseHorrorSoundboardEvent = true;
	public bool allowLegacyRandomTrigger = false;
	public KeyCode legacyRandomTriggerKey = KeyCode.Mouse1;

	[Header("Anti-Spam Cooldown")]
	public bool useGlobalCooldown = true;
	public float globalCooldownSeconds = 0.35f;
	public AudioClip cooldownNotReadySfx;
	[Range(0f, 1f)] public float cooldownNotReadyVolume = 0.45f;
	public float cooldownNotReadySfxInterval = 0.15f;

	private static readonly KeyCode[] DefaultKeys =
	{
		KeyCode.Alpha1,
		KeyCode.Alpha2,
		KeyCode.Alpha3,
		KeyCode.Alpha4
	};

	private AudioSource audioSource;
	private AudioSource feedbackAudioSource;
	private float[] slotCooldowns = Array.Empty<float>();
	private float globalCooldownUntil = -999f;
	private float nextCooldownBlockedSfxAt = -999f;

	void Start()
	{
		audioSource = GetComponent<AudioSource>();
		if (audioSource == null)
		{
			audioSource = gameObject.AddComponent<AudioSource>();
		}

		feedbackAudioSource = gameObject.AddComponent<AudioSource>();
		feedbackAudioSource.playOnAwake = false;
		feedbackAudioSource.loop = false;
		feedbackAudioSource.spatialBlend = 0f;

		BuildDefaultSoundboardIfNeeded();
		slotCooldowns = new float[soundboardSlots.Length];
	}

	void Update()
	{
		HandleMappedSlots();
		HandleLegacyRandomTrigger();
	}

	void HandleMappedSlots()
	{
		if (soundboardSlots == null || soundboardSlots.Length == 0)
		{
			return;
		}

		for (int i = 0; i < soundboardSlots.Length; i++)
		{
			SoundboardSlot slot = soundboardSlots[i];
			if (slot == null || slot.clip == null || slot.key == KeyCode.None)
			{
				continue;
			}

			if (!Input.GetKeyDown(slot.key))
			{
				continue;
			}

			bool slotCoolingDown = Time.time < slotCooldowns[i];
			bool globalCoolingDown = useGlobalCooldown && Time.time < globalCooldownUntil;
			if (slotCoolingDown || globalCoolingDown)
			{
				PlayCooldownBlockedFeedback();
				continue;
			}

			PlaySlot(i, slot);
		}
	}

	void HandleLegacyRandomTrigger()
	{
		if (!allowLegacyRandomTrigger || soundClips == null || soundClips.Length == 0)
		{
			return;
		}

		if (!Input.GetKeyDown(legacyRandomTriggerKey))
		{
			return;
		}

		int randomIndex = UnityEngine.Random.Range(0, soundClips.Length);
		PlayClip(soundClips[randomIndex], "LegacyRandom", 0.5f, 0.2f, 0.45f);
	}

	void PlaySlot(int slotIndex, SoundboardSlot slot)
	{
		slotCooldowns[slotIndex] = Time.time + Mathf.Max(0f, slot.cooldownSeconds);
		if (useGlobalCooldown)
		{
			globalCooldownUntil = Time.time + Mathf.Max(0f, globalCooldownSeconds);
		}

		AudioClip clipToPlay = slot.clip;
		string tagToPlay = string.IsNullOrWhiteSpace(slot.tag) ? "Slot" + (slotIndex + 1) : slot.tag;
		float effectiveLoudness = Mathf.Clamp01(Mathf.Lerp(slot.loudness, slot.threatAttraction, 0.5f));
		float reliefValue = Mathf.Clamp01(slot.humorReliefValue);
		float attraction = Mathf.Clamp01(slot.threatAttraction);

		if (slot.misfireChance > 0f && soundboardSlots.Length > 1 && UnityEngine.Random.value < slot.misfireChance)
		{
			int randomIndex = UnityEngine.Random.Range(0, soundboardSlots.Length);
			if (soundboardSlots[randomIndex] != null && soundboardSlots[randomIndex].clip != null)
			{
				clipToPlay = soundboardSlots[randomIndex].clip;
				tagToPlay += "_MISFIRE";
				effectiveLoudness = Mathf.Clamp01(Mathf.Lerp(soundboardSlots[randomIndex].loudness, soundboardSlots[randomIndex].threatAttraction, 0.5f));
			}
		}

		PlayClip(clipToPlay, tagToPlay, effectiveLoudness, reliefValue, attraction);
	}

	void PlayClip(AudioClip clip, string soundboardTag, float loudness, float humorReliefValue, float threatAttraction)
	{
		if (clip == null)
		{
			return;
		}

		audioSource.Stop();
		audioSource.clip = clip;
		audioSource.Play();

		if (raiseHorrorSoundboardEvent)
		{
			float eventLoudness = Mathf.Clamp01((loudness * 0.6f) + (threatAttraction * 0.4f) - (humorReliefValue * 0.15f));
			HorrorEvents.RaiseSoundboardPlayed(soundboardTag, eventLoudness);
		}
	}


	void PlayCooldownBlockedFeedback()
	{
		if (cooldownNotReadySfx == null || feedbackAudioSource == null)
		{
			return;
		}

		if (Time.time < nextCooldownBlockedSfxAt)
		{
			return;
		}

		nextCooldownBlockedSfxAt = Time.time + Mathf.Max(0.01f, cooldownNotReadySfxInterval);
		feedbackAudioSource.PlayOneShot(cooldownNotReadySfx, Mathf.Clamp01(cooldownNotReadyVolume));
	}

	void BuildDefaultSoundboardIfNeeded()
	{
		if (HasConfiguredSoundboard())
		{
			NormalizeUnassignedKeys();
			return;
		}

		int slotCount = Mathf.Min(4, soundClips != null ? soundClips.Length : 0);
		if (slotCount <= 0)
		{
			return;
		}

		soundboardSlots = new SoundboardSlot[slotCount];
		for (int i = 0; i < slotCount; i++)
		{
			soundboardSlots[i] = new SoundboardSlot
			{
				tag = "Meme" + (i + 1),
				key = DefaultKeys[i],
				clip = soundClips[i],
				loudness = Mathf.Clamp01(0.45f + (0.1f * i)),
				humorReliefValue = Mathf.Clamp01(0.25f + (0.05f * i)),
				threatAttraction = Mathf.Clamp01(0.5f + (0.08f * i)),
				cooldownSeconds = 4f + i
			};
		}
	}

	bool HasConfiguredSoundboard()
	{
		if (soundboardSlots == null || soundboardSlots.Length == 0)
		{
			return false;
		}

		for (int i = 0; i < soundboardSlots.Length; i++)
		{
			SoundboardSlot slot = soundboardSlots[i];
			if (slot != null && slot.clip != null)
			{
				return true;
			}
		}

		return false;
	}

	void NormalizeUnassignedKeys()
	{
		for (int i = 0; i < soundboardSlots.Length && i < DefaultKeys.Length; i++)
		{
			if (soundboardSlots[i] == null)
			{
				soundboardSlots[i] = new SoundboardSlot();
			}

			if (soundboardSlots[i].key == KeyCode.None)
			{
				soundboardSlots[i].key = DefaultKeys[i];
			}
		}
	}
}
