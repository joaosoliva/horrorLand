using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EncounterManager : MonoBehaviour
{
	[Header("References")]
	[SerializeField] private VillainAI villainAI;
	[SerializeField] private JumpscareSystem jumpscareSystem;
	[SerializeField] private ChaseSystem chaseSystem;
	[SerializeField] private Transform player;
	[SerializeField] private Transform playerView;
	[SerializeField] private EncounterBase[] encounterPool;

	[Header("Scheduling")]
	[SerializeField] private bool autoEnforceVisibleThreatResponse = true;
	[SerializeField] private float globalCooldown = 8f;
	[SerializeField] private int encountersBeforeForcedChase = 3;
	[SerializeField] private float visibilityFailSafeCooldown = 2f;
	[SerializeField] private float idleVisibleGrace = 0.2f;
	[SerializeField] private bool disableWhenChaseStarts = true;
	[SerializeField] private bool enableDebugLogs = false;

	private readonly Dictionary<string, float> encounterLastUsedTime = new Dictionary<string, float>();
	private EncounterBase activeEncounter;
	private Coroutine activeEncounterRoutine;
	private int completedEncounterCount = 0;
	private float nextGlobalAllowedTime = -999f;
	private float visibleSinceTime = -999f;
	private float lastVisibleFailSafeActionTime = -999f;

	public bool HasActiveEncounter => activeEncounter != null;
	public int CompletedEncounterCount => completedEncounterCount;

	void Awake()
	{
		InitializeEncounterPool();
	}

	void OnEnable()
	{
		HorrorEvents.OnChaseStarted += HandleChaseStarted;
	}

	void OnDisable()
	{
		HorrorEvents.OnChaseStarted -= HandleChaseStarted;
	}

	void Update()
	{
		if (!autoEnforceVisibleThreatResponse || villainAI == null || villainAI.IsChasing)
		{
			visibleSinceTime = -999f;
			return;
		}

		bool playerSeeingVillain = villainAI.CanPlayerSeeVillain();
		if (!playerSeeingVillain)
		{
			visibleSinceTime = -999f;
			return;
		}

		if (visibleSinceTime < 0f)
		{
			visibleSinceTime = Time.time;
		}

		if (Time.time - visibleSinceTime < idleVisibleGrace)
		{
			return;
		}

		if (Time.time - lastVisibleFailSafeActionTime < visibilityFailSafeCooldown)
		{
			return;
		}

		if (HasActiveEncounter)
		{
			return;
		}

		if (!TryTriggerEncounter("Visibility fail-safe", true))
		{
			ForceImmediateVisibleResponse();
		}
	}

	public bool TryTriggerEncounterFromGate(string reason)
	{
		return TryTriggerEncounter($"First chase gate: {reason}", false);
	}

	public bool TryTriggerEncounter(string reason, bool bypassGlobalCooldown)
	{
		if (villainAI == null || player == null)
		{
			return false;
		}

		if (chaseSystem != null && chaseSystem.IsChaseActive)
		{
			return false;
		}

		if (HasActiveEncounter)
		{
			return false;
		}

		if (!bypassGlobalCooldown && Time.time < nextGlobalAllowedTime)
		{
			return false;
		}

		PreChaseEncounterContext context = BuildContext();
		EncounterBase selected = SelectEncounter(context, bypassGlobalCooldown);
		if (selected == null)
		{
			return false;
		}

		activeEncounterRoutine = StartCoroutine(RunEncounter(selected, context, reason));
		return true;
	}

	private PreChaseEncounterContext BuildContext()
	{
		Transform effectivePlayerView = playerView != null ? playerView : player;
		return new PreChaseEncounterContext
		{
			villainAI = villainAI,
			jumpscareSystem = jumpscareSystem,
			chaseSystem = chaseSystem,
			player = player,
			playerView = effectivePlayerView,
			distanceToPlayer = Vector3.Distance(villainAI.transform.position, player.position),
			timestamp = Time.time
		};
	}

	private EncounterBase SelectEncounter(PreChaseEncounterContext context, bool bypassGlobalCooldown)
	{
		if (encounterPool == null || encounterPool.Length == 0)
		{
			return null;
		}

		List<EncounterBase> candidates = new List<EncounterBase>();
		for (int i = 0; i < encounterPool.Length; i++)
		{
			EncounterBase encounter = encounterPool[i];
			if (encounter == null || !encounter.isActiveAndEnabled)
			{
				continue;
			}

			if (!encounter.CanTrigger(context))
			{
				continue;
			}

			if (!bypassGlobalCooldown && encounterLastUsedTime.TryGetValue(encounter.EncounterId, out float lastUsed) && Time.time - lastUsed < encounter.MinRepeatDelay)
			{
				continue;
			}

			candidates.Add(encounter);
		}

		if (candidates.Count == 0)
		{
			return null;
		}

		return candidates[Random.Range(0, candidates.Count)];
	}

	private IEnumerator RunEncounter(EncounterBase selected, PreChaseEncounterContext context, string reason)
	{
		activeEncounter = selected;
		lastVisibleFailSafeActionTime = Time.time;
		if (enableDebugLogs)
		{
			Debug.Log($"[EncounterManager] Starting encounter '{selected.EncounterId}'. Reason: {reason}");
		}

		yield return selected.Execute(context);

		encounterLastUsedTime[selected.EncounterId] = Time.time;
		nextGlobalAllowedTime = Time.time + globalCooldown;
		completedEncounterCount++;
		activeEncounter = null;
		activeEncounterRoutine = null;

		if (enableDebugLogs)
		{
			Debug.Log($"[EncounterManager] Encounter complete: '{selected.EncounterId}'. Completed count: {completedEncounterCount}");
		}

		if (chaseSystem != null && completedEncounterCount >= Mathf.Max(1, encountersBeforeForcedChase) && !chaseSystem.IsChaseActive)
		{
			chaseSystem.RequestDirectorChase("Pre-chase encounter budget exhausted");
		}
	}

	private void InitializeEncounterPool()
	{
		if (encounterPool == null)
		{
			return;
		}

		for (int i = 0; i < encounterPool.Length; i++)
		{
			if (encounterPool[i] != null)
			{
				encounterPool[i].Initialize(this);
			}
		}
	}

	private void ForceImmediateVisibleResponse()
	{
		lastVisibleFailSafeActionTime = Time.time;
		if (jumpscareSystem != null)
		{
			jumpscareSystem.ForceMajorScare(false);
		}

		if (villainAI != null)
		{
			villainAI.TryDisappearFromPlayer("Visibility fail-safe response");
		}
	}

	private void HandleChaseStarted()
	{
		if (activeEncounter != null)
		{
			activeEncounter.ForceStop();
		}

		if (activeEncounterRoutine != null)
		{
			StopCoroutine(activeEncounterRoutine);
			activeEncounterRoutine = null;
		}

		activeEncounter = null;
		visibleSinceTime = -999f;
		if (disableWhenChaseStarts)
		{
			enabled = false;
		}
	}
}
