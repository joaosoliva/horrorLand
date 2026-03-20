using System.Collections.Generic;
using UnityEngine;

public class ScareScheduler : MonoBehaviour
{
	[Header("References")]
	public HorrorDirector horrorDirector;
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
	public float presenceCueGuarantee = 25f;
	public int repeatHistorySize = 2;
	public bool enableDebugLogs = false;

	private float nextBeatTime = -999f;
	private float lastMajorScareTime = -999f;
	private float lastPresenceCueTime = -999f;
	private readonly Queue<ScareType> recentScares = new Queue<ScareType>();
	private bool catchUpBeatQueued;

	void Start()
	{
		if (horrorDirector == null)
		{
			horrorDirector = FindObjectOfType<HorrorDirector>();
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
	}

	public void RequestCatchUpBeat()
	{
		catchUpBeatQueued = true;
	}

	void TriggerScheduledBeat(bool forced)
	{
		ScareType nextScare = SelectNextScare(horrorDirector.CurrentPhase, forced);
		ExecuteScare(nextScare);
		ScheduleNextBeat();
	}

	ScareType SelectNextScare(HorrorPhase phase, bool forced)
	{
		List<ScareType> candidates = new List<ScareType>();
		if (Time.time - lastPresenceCueTime >= presenceCueGuarantee)
		{
			candidates.Add(ScareType.PresenceCue);
		}

		if (phase == HorrorPhase.Calm)
		{
			candidates.Add(ScareType.PresenceCue);
			candidates.Add(ScareType.MinorPsychological);
			candidates.Add(ScareType.ReliefBeat);
		}
		else if (phase == HorrorPhase.Build)
		{
			candidates.Add(ScareType.MinorPsychological);
			candidates.Add(ScareType.PresenceCue);
			candidates.Add(ScareType.Fakeout);
		}
		else if (phase == HorrorPhase.Threat)
		{
			candidates.Add(ScareType.PresenceCue);
			candidates.Add(ScareType.Fakeout);
			candidates.Add(ScareType.ChaseTrigger);
			candidates.Add(ScareType.RoutePressure);
		}
		else if (phase == HorrorPhase.Peak)
		{
			candidates.Add(ScareType.Fakeout);
			candidates.Add(ScareType.MinorPsychological);
		}
		else if (phase == HorrorPhase.Relief)
		{
			candidates.Add(ScareType.ReliefBeat);
			candidates.Add(ScareType.MinorPsychological);
			candidates.Add(ScareType.PresenceCue);
		}
		else
		{
			candidates.Add(ScareType.ChaseTrigger);
			candidates.Add(ScareType.MajorJumpscare);
			candidates.Add(ScareType.PresenceCue);
			candidates.Add(ScareType.Fakeout);
			candidates.Add(ScareType.RoutePressure);
		}

		if ((phase == HorrorPhase.Threat || phase == HorrorPhase.Finale) && Time.time - lastMajorScareTime >= majorScareCooldown)
		{
			candidates.Add(ScareType.MajorJumpscare);
		}

		for (int i = candidates.Count - 1; i >= 0; i--)
		{
			if (IsRecentRepeat(candidates[i]))
			{
				candidates.RemoveAt(i);
			}
		}

		if (candidates.Count == 0)
		{
			return forced ? ScareType.PresenceCue : ScareType.MinorPsychological;
		}

		return candidates[Random.Range(0, candidates.Count)];
	}

	void ExecuteScare(ScareType scareType)
	{
		if (enableDebugLogs)
		{
			Debug.Log("ScareScheduler beat: " + scareType + " during phase " + (horrorDirector != null ? horrorDirector.CurrentPhase.ToString() : "N/A"));
		}

		if (scareType == ScareType.ChaseTrigger)
		{
			if (chaseSystem != null && chaseSystem.RequestDirectorChase("Director scheduled chase beat"))
			{
				return;
			}

			scareType = ScareType.PresenceCue;
		}

		if (scareType == ScareType.MajorJumpscare)
		{
			if (jumpscareSystem != null && !jumpscareSystem.IsJumpscareActive())
			{
				jumpscareSystem.ForceMajorScare(true);
				lastMajorScareTime = Time.time;
				return;
			}

			scareType = ScareType.Fakeout;
		}

		if (jumpscareSystem != null)
		{
			jumpscareSystem.ForceMinorScare(scareType);
			if (scareType == ScareType.PresenceCue)
			{
				lastPresenceCueTime = Time.time;
			}
			return;
		}

		HorrorEvents.RaiseScareTriggered(scareType);
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
