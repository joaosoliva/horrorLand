using System;
using System.Collections.Generic;
using UnityEngine;

public class CorruptionSystem : MonoBehaviour
{
	[Serializable]
	public class CorruptionEventEntry
	{
		public string id = "Event";
		[Range(0f, 1f)] public float minCorruptionNormalized = 0f;
		[Range(0f, 1f)] public float maxCorruptionNormalized = 1f;
		[Range(0f, 1f)] public float triggerChance = 0.15f;
		public float cooldownSeconds = 12f;
	}

	[Header("Corruption")]
	public float maxCorruption = 100f;
	public float startingCorruption = 0f;
	public float currentCorruption = 0f;
	public bool clampCorruption = true;

	[Header("Soundboard Gain")]
	public bool gainCorruptionFromSoundboard = true;
	public float corruptionGainMultiplier = 12f;
	public float minimumGainPerUse = 1f;

	[Header("Passive Drift")]
	public float passiveDecayPerSecond = 0.8f;

	[Header("Event Table (rolls on corruption gain)")]
	public List<CorruptionEventEntry> eventTable = new List<CorruptionEventEntry>();
	public bool debugLogs = false;

	private readonly Dictionary<string, float> eventCooldownById = new Dictionary<string, float>();

	public float CurrentCorruption => currentCorruption;
	public float NormalizedCorruption => maxCorruption <= 0f ? 0f : Mathf.Clamp01(currentCorruption / maxCorruption);

	void Start()
	{
		if (maxCorruption <= 0f)
		{
			maxCorruption = 100f;
		}

		currentCorruption = Mathf.Clamp(startingCorruption, 0f, maxCorruption);
		BroadcastCorruption();
	}

	void OnEnable()
	{
		HorrorEvents.OnSoundboardPlayed += HandleSoundboardPlayed;
	}

	void OnDisable()
	{
		HorrorEvents.OnSoundboardPlayed -= HandleSoundboardPlayed;
	}

	void Update()
	{
		if (passiveDecayPerSecond <= 0f || currentCorruption <= 0f)
		{
			return;
		}

		ApplyCorruptionDelta(-passiveDecayPerSecond * Time.deltaTime, "PassiveDecay", false);
	}

	void HandleSoundboardPlayed(string soundTag, float loudness)
	{
		if (!gainCorruptionFromSoundboard)
		{
			return;
		}

		float gain = Mathf.Max(minimumGainPerUse, Mathf.Clamp01(loudness) * corruptionGainMultiplier);
		AddCorruption(gain, "Soundboard:" + soundTag);
	}

	public void AddCorruption(float amount, string reason = "External")
	{
		ApplyCorruptionDelta(Mathf.Abs(amount), reason, true);
	}

	public void ReduceCorruption(float amount, string reason = "External")
	{
		ApplyCorruptionDelta(-Mathf.Abs(amount), reason, false);
	}

	void ApplyCorruptionDelta(float delta, string reason, bool rollEventTable)
	{
		if (Mathf.Abs(delta) <= 0f)
		{
			return;
		}

		float before = currentCorruption;
		currentCorruption += delta;
		if (clampCorruption)
		{
			currentCorruption = Mathf.Clamp(currentCorruption, 0f, maxCorruption);
		}

		if (Mathf.Abs(currentCorruption - before) < 0.001f)
		{
			return;
		}

		if (debugLogs)
		{
			Debug.Log("CorruptionSystem " + reason + " delta: " + delta.ToString("F2") + " -> " + currentCorruption.ToString("F1"));
		}

		BroadcastCorruption();

		if (rollEventTable && delta > 0f)
		{
			RollEventTable();
		}
	}

	void BroadcastCorruption()
	{
		HorrorEvents.RaiseCorruptionChanged(currentCorruption, NormalizedCorruption);
	}

	void RollEventTable()
	{
		if (eventTable == null || eventTable.Count == 0)
		{
			return;
		}

		float corruption = NormalizedCorruption;
		for (int i = 0; i < eventTable.Count; i++)
		{
			CorruptionEventEntry entry = eventTable[i];
			if (entry == null || string.IsNullOrWhiteSpace(entry.id))
			{
				continue;
			}

			if (corruption < entry.minCorruptionNormalized || corruption > entry.maxCorruptionNormalized)
			{
				continue;
			}

			if (eventCooldownById.TryGetValue(entry.id, out float cooldownUntil) && Time.time < cooldownUntil)
			{
				continue;
			}

			if (UnityEngine.Random.value <= Mathf.Clamp01(entry.triggerChance))
			{
				eventCooldownById[entry.id] = Time.time + Mathf.Max(0f, entry.cooldownSeconds);
				HorrorEvents.RaiseCorruptionEventTriggered(entry.id, corruption);
				if (debugLogs)
				{
					Debug.Log("Corruption event triggered: " + entry.id + " at " + corruption.ToString("0.00"));
				}
			}
		}
	}
}
