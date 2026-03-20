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
		public float cooldownSeconds = 5f;
	}

	[Header("Legacy Fallback")]
	public AudioClip[] soundClips;

	[Header("Soundboard")]
	public SoundboardSlot[] soundboardSlots = new SoundboardSlot[4];
	public bool raiseHorrorSoundboardEvent = true;
	public bool allowLegacyRandomTrigger = false;
	public KeyCode legacyRandomTriggerKey = KeyCode.Mouse1;

	private static readonly KeyCode[] DefaultKeys =
	{
		KeyCode.Alpha1,
		KeyCode.Alpha2,
		KeyCode.Alpha3,
		KeyCode.Alpha4
	};

	private AudioSource audioSource;
	private float[] slotCooldowns = Array.Empty<float>();

	void Start()
	{
		audioSource = GetComponent<AudioSource>();
		if (audioSource == null)
		{
			audioSource = gameObject.AddComponent<AudioSource>();
		}

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

			if (Time.time < slotCooldowns[i])
			{
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
		PlayClip(soundClips[randomIndex], "LegacyRandom", 0.5f);
	}

	void PlaySlot(int slotIndex, SoundboardSlot slot)
	{
		slotCooldowns[slotIndex] = Time.time + Mathf.Max(0f, slot.cooldownSeconds);
		PlayClip(slot.clip, string.IsNullOrWhiteSpace(slot.tag) ? $"Slot{slotIndex + 1}" : slot.tag, slot.loudness);
	}

	void PlayClip(AudioClip clip, string soundboardTag, float loudness)
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
			HorrorEvents.RaiseSoundboardPlayed(soundboardTag, loudness);
		}
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
				tag = $"Meme{i + 1}",
				key = DefaultKeys[i],
				clip = soundClips[i],
				loudness = Mathf.Clamp01(0.45f + (0.1f * i)),
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
