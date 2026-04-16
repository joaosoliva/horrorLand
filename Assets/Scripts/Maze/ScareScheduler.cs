using System.Collections.Generic;
using UnityEngine;

public class ScareScheduler : MonoBehaviour
{
	[Header("References")]
	public HorrorDirector horrorDirector;
	public EncounterDirector encounterDirector;
	public JumpscareSystem jumpscareSystem;
	public ChaseSystem chaseSystem;
	public VillainAI villainAI;

	[Header("Beat Timing")]
	public Vector2 calmBeatInterval = new Vector2(12f, 18f);
	public Vector2 buildBeatInterval = new Vector2(12f, 16f);
	public Vector2 threatBeatInterval = new Vector2(10f, 14f);
	public Vector2 reliefBeatInterval = new Vector2(8f, 12f);
	public Vector2 finaleBeatInterval = new Vector2(6f, 10f);
	public float majorScareCooldown = 35f;
	public float minimumScareFrequency = 13f;
	public float failedBeatRetryDelay = 1.75f;
	public float presenceCueGuarantee = 25f;
	public int repeatHistorySize = 2;
	[Range(0f, 1f)] public float nonPayoffBuildChance = 0.35f;
	public float audioFakeoutCooldown = 12f;
	public float silhouetteRevealCooldown = 18f;
	public float routePressureCooldown = 14f;
	public float environmentShiftCooldown = 10f;
	public float hardScareCategoryCooldown = 45f;
	public bool enableDebugLogs = false;

	private float nextBeatTime = -999f;
	private float lastMajorScareTime = -999f;
	private float lastPresenceCueTime = -999f;
	private readonly Queue<ScareType> recentScares = new Queue<ScareType>();
	private readonly Queue<EncounterIntent> recentEncounterIntents = new Queue<EncounterIntent>();
	private bool catchUpBeatQueued;
	private float lastAudioFakeoutTime = -999f;
	private float lastSilhouetteRevealTime = -999f;
	private float lastRoutePressureTime = -999f;
	private float lastEnvironmentShiftTime = -999f;
	private float lastHardScareCategoryTime = -999f;
	private float lastExecutedBeatTime = -999f;

	void Start()
	{
		if (horrorDirector == null)
		{
			horrorDirector = FindObjectOfType<HorrorDirector>();
		}
		if (encounterDirector == null)
		{
			encounterDirector = FindObjectOfType<EncounterDirector>();
			if (encounterDirector == null)
			{
				encounterDirector = gameObject.AddComponent<EncounterDirector>();
			}
		}
		if (jumpscareSystem == null)
		{
			jumpscareSystem = FindObjectOfType<JumpscareSystem>();
		}
		if (chaseSystem == null)
		{
			chaseSystem = FindObjectOfType<ChaseSystem>();
		}
		if (villainAI == null)
		{
			villainAI = FindObjectOfType<VillainAI>();
		}
		if (encounterDirector != null)
		{
			encounterDirector.scareScheduler = this;
			if (encounterDirector.horrorDirector == null)
			{
				encounterDirector.horrorDirector = horrorDirector;
			}
			if (encounterDirector.chaseSystem == null)
			{
				encounterDirector.chaseSystem = chaseSystem;
			}
			if (encounterDirector.jumpscareSystem == null)
			{
				encounterDirector.jumpscareSystem = jumpscareSystem;
			}
			if (encounterDirector.villainAI == null)
			{
				encounterDirector.villainAI = villainAI;
			}
		}

		lastExecutedBeatTime = Time.time;
		ScheduleNextBeat();
	}

	void OnEnable()
	{
		HorrorEvents.OnScareTriggered += HandleScareTriggered;
		HorrorEvents.OnJumpscareTriggered += HandleJumpscareTriggered;
		HorrorEvents.OnChaseStarted += HandleChaseStarted;
	}

	void OnDisable()
	{
		HorrorEvents.OnScareTriggered -= HandleScareTriggered;
		HorrorEvents.OnJumpscareTriggered -= HandleJumpscareTriggered;
		HorrorEvents.OnChaseStarted -= HandleChaseStarted;
	}

	void Update()
	{
		if (horrorDirector == null)
		{
			return;
		}

		if (catchUpBeatQueued || Time.time >= nextBeatTime)
		{
			TriggerScheduledBeat(catchUpBeatQueued);
			catchUpBeatQueued = false;
		}
		else if (Time.time - lastExecutedBeatTime >= Mathf.Max(2f, minimumScareFrequency))
		{
			TriggerScheduledBeat(forced: true);
		}
	}

	public void RequestCatchUpBeat()
	{
		catchUpBeatQueued = true;
	}

	void TriggerScheduledBeat(bool forced)
	{
		EncounterIntent nextIntent = SelectNextEncounter(horrorDirector.CurrentPhase, forced);
		bool executed = ExecuteEncounter(nextIntent, forced);
		if (executed)
		{
			lastExecutedBeatTime = Time.time;
			ScheduleNextBeat();
		}
		else
		{
			nextBeatTime = Time.time + Mathf.Max(0.25f, failedBeatRetryDelay);
		}
	}

	EncounterIntent SelectNextEncounter(HorrorPhase phase, bool forced)
	{
		List<EncounterIntent> candidates = new List<EncounterIntent>();
		if (Time.time - lastPresenceCueTime >= presenceCueGuarantee)
		{
			candidates.Add(EncounterIntent.Presence);
		}

		if (phase == HorrorPhase.Calm)
		{
			candidates.Add(EncounterIntent.Presence);
			candidates.Add(EncounterIntent.Release);
		}
		else if (phase == HorrorPhase.Build)
		{
			candidates.Add(EncounterIntent.Presence);
			candidates.Add(EncounterIntent.Probe);
			if (!forced && Random.value > nonPayoffBuildChance)
			{
				candidates.Add(EncounterIntent.Commitment);
			}
		}
		else if (phase == HorrorPhase.Threat)
		{
			candidates.Add(EncounterIntent.Probe);
			candidates.Add(EncounterIntent.Commitment);
			if (!forced && Random.value < nonPayoffBuildChance)
			{
				candidates.Add(EncounterIntent.Presence);
			}
		}
		else if (phase == HorrorPhase.Peak)
		{
			candidates.Add(EncounterIntent.Commitment);
			candidates.Add(EncounterIntent.Probe);
		}
		else if (phase == HorrorPhase.Relief)
		{
			candidates.Add(EncounterIntent.Release);
			candidates.Add(EncounterIntent.Presence);
			candidates.Add(EncounterIntent.Probe);
		}
		else
		{
			candidates.Add(EncounterIntent.Commitment);
			candidates.Add(EncounterIntent.Probe);
			candidates.Add(EncounterIntent.Presence);
		}

		if ((phase == HorrorPhase.Threat || phase == HorrorPhase.Finale) &&
			Time.time - lastMajorScareTime >= majorScareCooldown &&
			IsCategoryAvailable(EncounterCategory.HardScare))
		{
			candidates.Add(EncounterIntent.Commitment);
		}

		for (int i = candidates.Count - 1; i >= 0; i--)
		{
			if (IsRecentEncounterRepeat(candidates[i]))
			{
				candidates.RemoveAt(i);
			}
		}

		if (candidates.Count == 0)
		{
			return forced ? EncounterIntent.Presence : EncounterIntent.Probe;
		}

		return candidates[Random.Range(0, candidates.Count)];
	}

	bool ExecuteEncounter(EncounterIntent encounterIntent, bool forced)
	{
		if (enableDebugLogs)
		{
			Debug.Log("ScareScheduler beat: " + encounterIntent + " during phase " + (horrorDirector != null ? horrorDirector.CurrentPhase.ToString() : "N/A"));
		}

		recentEncounterIntents.Enqueue(encounterIntent);
		while (recentEncounterIntents.Count > Mathf.Max(1, repeatHistorySize))
		{
			recentEncounterIntents.Dequeue();
		}

		if (encounterDirector != null && encounterDirector.ExecuteEncounter(encounterIntent, forced))
		{
			return true;
		}

		ScareType fallbackScare = MapEncounterToFallbackScare(encounterIntent, forced);
		if (jumpscareSystem != null)
		{
			if (fallbackScare == ScareType.MajorJumpscare && !jumpscareSystem.IsJumpscareActive())
			{
				jumpscareSystem.ForceMajorScare(true);
				return true;
			}

			jumpscareSystem.ForceMinorScare(fallbackScare);
			return true;
		}

		HorrorEvents.RaiseScareTriggered(fallbackScare);
		return true;
	}

	void ScheduleNextBeat()
	{
		if (horrorDirector == null)
		{
			nextBeatTime = Time.time + 12f;
			return;
		}

		Vector2 interval = buildBeatInterval;
		HorrorPhase phase = horrorDirector.CurrentPhase;
		if (phase == HorrorPhase.Calm)
		{
			interval = calmBeatInterval;
		}
		else if (phase == HorrorPhase.Threat || phase == HorrorPhase.Peak)
		{
			interval = threatBeatInterval;
		}
		else if (phase == HorrorPhase.Relief)
		{
			interval = reliefBeatInterval;
		}
		else if (phase == HorrorPhase.Finale)
		{
			interval = finaleBeatInterval;
		}

		nextBeatTime = Time.time + Random.Range(interval.x, Mathf.Max(interval.x, interval.y));
	}

	bool IsRecentRepeat(ScareType scareType)
	{
		foreach (ScareType recent in recentScares)
		{
			if (recent == scareType)
			{
				return true;
			}
		}

		return false;
	}

	bool IsRecentEncounterRepeat(EncounterIntent encounterIntent)
	{
		foreach (EncounterIntent recentEncounter in recentEncounterIntents)
		{
			if (recentEncounter == encounterIntent)
			{
				return true;
			}
		}

		return false;
	}

	ScareType MapEncounterToFallbackScare(EncounterIntent encounterIntent, bool forced)
	{
		if (encounterIntent == EncounterIntent.Presence)
		{
			return ScareType.PresenceCue;
		}
		if (encounterIntent == EncounterIntent.Release)
		{
			return ScareType.ReliefBeat;
		}
		if (encounterIntent == EncounterIntent.Commitment)
		{
			return forced || Time.time - lastMajorScareTime >= majorScareCooldown ? ScareType.MajorJumpscare : ScareType.ChaseTrigger;
		}

		return Random.value < 0.5f ? ScareType.Fakeout : ScareType.RoutePressure;
	}

	public bool IsCategoryAvailable(EncounterCategory category)
	{
		float lastUseTime = -999f;
		float cooldown = audioFakeoutCooldown;

		switch (category)
		{
			case EncounterCategory.AudioFakeout:
				lastUseTime = lastAudioFakeoutTime;
				cooldown = audioFakeoutCooldown;
				break;
			case EncounterCategory.SilhouetteReveal:
				lastUseTime = lastSilhouetteRevealTime;
				cooldown = silhouetteRevealCooldown;
				break;
			case EncounterCategory.RoutePressure:
				lastUseTime = lastRoutePressureTime;
				cooldown = routePressureCooldown;
				break;
			case EncounterCategory.EnvironmentShift:
				lastUseTime = lastEnvironmentShiftTime;
				cooldown = environmentShiftCooldown;
				break;
			case EncounterCategory.HardScare:
				lastUseTime = lastHardScareCategoryTime;
				cooldown = hardScareCategoryCooldown;
				break;
		}

		return Time.time - lastUseTime >= cooldown;
	}

	public void RegisterCategoryUse(EncounterCategory category)
	{
		switch (category)
		{
			case EncounterCategory.AudioFakeout:
				lastAudioFakeoutTime = Time.time;
				break;
			case EncounterCategory.SilhouetteReveal:
				lastSilhouetteRevealTime = Time.time;
				break;
			case EncounterCategory.RoutePressure:
				lastRoutePressureTime = Time.time;
				break;
			case EncounterCategory.EnvironmentShift:
				lastEnvironmentShiftTime = Time.time;
				break;
			case EncounterCategory.HardScare:
				lastHardScareCategoryTime = Time.time;
				break;
		}
	}

	void HandleScareTriggered(ScareType scareType)
	{
		recentScares.Enqueue(scareType);
		while (recentScares.Count > Mathf.Max(1, repeatHistorySize))
		{
			recentScares.Dequeue();
		}

		if (scareType == ScareType.MajorJumpscare)
		{
			lastMajorScareTime = Time.time;
		}
		if (scareType == ScareType.PresenceCue)
		{
			lastPresenceCueTime = Time.time;
		}
	}

	void HandleJumpscareTriggered()
	{
		lastMajorScareTime = Time.time;
	}

	void HandleChaseStarted()
	{
		recentScares.Enqueue(ScareType.ChaseTrigger);
		while (recentScares.Count > Mathf.Max(1, repeatHistorySize))
		{
			recentScares.Dequeue();
		}
	}
}
