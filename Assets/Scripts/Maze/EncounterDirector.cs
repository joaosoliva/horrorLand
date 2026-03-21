using System.Collections.Generic;
using UnityEngine;

public enum EncounterIntent
{
	Presence,
	Probe,
	Commitment,
	Release
}

public enum EncounterCategory
{
	AudioFakeout,
	SilhouetteReveal,
	RoutePressure,
	EnvironmentShift,
	HardScare
}

public class EncounterDirector : MonoBehaviour
{
	[Header("References")]
	public HorrorDirector horrorDirector;
	public ScareScheduler scareScheduler;
	public ChaseSystem chaseSystem;
	public JumpscareSystem jumpscareSystem;
	public VillainAI villainAI;
	public EnvironmentScareController environmentScareController;

	[Header("Tuning")]
	[Range(0f, 1f)] public float commitmentChaseBias = 0.7f;
	[Range(0f, 1f)] public float probeRoutePressureBias = 0.45f;
	[Range(0f, 1f)] public float releaseEnvironmentalBias = 0.65f;
	public bool enableDebugLogs = false;

	void Start()
	{
		if (horrorDirector == null)
		{
			horrorDirector = FindObjectOfType<HorrorDirector>();
		}
		if (scareScheduler == null)
		{
			scareScheduler = FindObjectOfType<ScareScheduler>();
		}
		if (chaseSystem == null)
		{
			chaseSystem = FindObjectOfType<ChaseSystem>();
		}
		if (jumpscareSystem == null)
		{
			jumpscareSystem = FindObjectOfType<JumpscareSystem>();
		}
		if (villainAI == null)
		{
			villainAI = FindObjectOfType<VillainAI>();
		}
		if (environmentScareController == null)
		{
			environmentScareController = FindObjectOfType<EnvironmentScareController>();
			if (environmentScareController == null)
			{
				environmentScareController = gameObject.AddComponent<EnvironmentScareController>();
			}
		}
	}

	public bool ExecuteEncounter(EncounterIntent intent, bool forced)
	{
		if (enableDebugLogs)
		{
			Debug.Log("EncounterDirector executing " + intent + " encounter.");
		}

		switch (intent)
		{
			case EncounterIntent.Presence:
				return ExecutePresenceEncounter();
			case EncounterIntent.Probe:
				return ExecuteProbeEncounter();
			case EncounterIntent.Commitment:
				return ExecuteCommitmentEncounter(forced);
			case EncounterIntent.Release:
				return ExecuteReleaseEncounter();
		}

		return false;
	}

	bool ExecutePresenceEncounter()
	{
		List<EncounterCategory> priorities = new List<EncounterCategory>
		{
			EncounterCategory.SilhouetteReveal,
			EncounterCategory.AudioFakeout,
			EncounterCategory.EnvironmentShift
		};

		foreach (EncounterCategory category in priorities)
		{
			if (!CanUseCategory(category))
			{
				continue;
			}

			if (environmentScareController != null && environmentScareController.TryPlayPresenceScare(category))
			{
				RegisterCategoryUse(category);
				return true;
			}
		}

		if (jumpscareSystem != null)
		{
			jumpscareSystem.ForceMinorScare(ScareType.PresenceCue);
			RegisterCategoryUse(EncounterCategory.AudioFakeout);
			return true;
		}

		return false;
	}

	bool ExecuteProbeEncounter()
	{
		List<EncounterCategory> priorities = new List<EncounterCategory>();
		if (Random.value < probeRoutePressureBias)
		{
			priorities.Add(EncounterCategory.RoutePressure);
		}
		priorities.Add(EncounterCategory.AudioFakeout);
		priorities.Add(EncounterCategory.EnvironmentShift);
		priorities.Add(EncounterCategory.SilhouetteReveal);

		foreach (EncounterCategory category in priorities)
		{
			if (!CanUseCategory(category))
			{
				continue;
			}

			if (environmentScareController != null && environmentScareController.TryPlayProbeScare(category))
			{
				RegisterCategoryUse(category);
				return true;
			}
		}

		if (jumpscareSystem != null)
		{
			jumpscareSystem.ForceMinorScare(Random.value < 0.5f ? ScareType.Fakeout : ScareType.RoutePressure);
			RegisterCategoryUse(EncounterCategory.AudioFakeout);
			return true;
		}

		return false;
	}

	bool ExecuteCommitmentEncounter(bool forced)
	{
		bool canUseHardScare = CanUseCategory(EncounterCategory.HardScare);
		bool chasePreferred = forced || Random.value < commitmentChaseBias;
		if (chasePreferred && chaseSystem != null && chaseSystem.RequestDirectorChase("EncounterDirector commitment encounter"))
		{
			RegisterCategoryUse(EncounterCategory.RoutePressure);
			return true;
		}

		if (canUseHardScare && jumpscareSystem != null && !jumpscareSystem.IsJumpscareActive())
		{
			jumpscareSystem.ForceMajorScare(true);
			RegisterCategoryUse(EncounterCategory.HardScare);
			return true;
		}

		if (environmentScareController != null && CanUseCategory(EncounterCategory.SilhouetteReveal) && environmentScareController.TryPlayPresenceScare(EncounterCategory.SilhouetteReveal))
		{
			RegisterCategoryUse(EncounterCategory.SilhouetteReveal);
			return true;
		}

		return ExecuteProbeEncounter();
	}

	bool ExecuteReleaseEncounter()
	{
		if (environmentScareController != null && Random.value < releaseEnvironmentalBias && environmentScareController.TryPlayReleaseBeat())
		{
			return true;
		}

		if (jumpscareSystem != null)
		{
			jumpscareSystem.ForceMinorScare(ScareType.ReliefBeat);
			return true;
		}

		return false;
	}

	bool CanUseCategory(EncounterCategory category)
	{
		return scareScheduler == null || scareScheduler.IsCategoryAvailable(category);
	}

	void RegisterCategoryUse(EncounterCategory category)
	{
		if (scareScheduler != null)
		{
			scareScheduler.RegisterCategoryUse(category);
		}
	}
}
