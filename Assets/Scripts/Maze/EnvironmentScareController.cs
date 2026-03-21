using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnvironmentScareController : MonoBehaviour
{
	[Header("Optional Scene References")]
	public Light[] flickerLights;
	public GameObject[] shadowRevealObjects;
	public AudioSource[] fakeoutAudioSources;
	public AudioSource[] routePressureAudioSources;
	public AudioSource reliefAudioSource;
	public Animator[] slamDoorAnimators;
	public string slamDoorTriggerName = "Slam";

	[Header("Timing")]
	public float lightFlickerDuration = 0.65f;
	public float shadowRevealDuration = 0.4f;
	public float reuseCooldown = 2.5f;

	private readonly Dictionary<string, float> effectCooldowns = new Dictionary<string, float>();

	void Start()
	{
		SetShadowObjects(false);
	}

	public bool TryPlayPresenceScare(EncounterCategory category)
	{
		if (category == EncounterCategory.SilhouetteReveal)
		{
			return TryShadowReveal(ScareType.PresenceCue);
		}
		if (category == EncounterCategory.EnvironmentShift)
		{
			return TryLightFlicker(ScareType.PresenceCue);
		}

		return TryFakeoutAudio(ScareType.PresenceCue, fakeoutAudioSources);
	}

	public bool TryPlayProbeScare(EncounterCategory category)
	{
		if (category == EncounterCategory.RoutePressure)
		{
			return TryRoutePressure();
		}
		if (category == EncounterCategory.EnvironmentShift)
		{
			if (TryDoorSlam(ScareType.MinorPsychological))
			{
				return true;
			}
			return TryLightFlicker(ScareType.MinorPsychological);
		}
		if (category == EncounterCategory.SilhouetteReveal)
		{
			return TryShadowReveal(ScareType.Fakeout);
		}

		return TryFakeoutAudio(ScareType.Fakeout, fakeoutAudioSources);
	}

	public bool TryPlayReleaseBeat()
	{
		if (!CanReuse("release"))
		{
			return false;
		}

		if (reliefAudioSource != null && reliefAudioSource.clip != null)
		{
			reliefAudioSource.PlayOneShot(reliefAudioSource.clip, 0.45f);
			MarkUsed("release");
			HorrorEvents.RaiseScareTriggered(ScareType.ReliefBeat);
			return true;
		}

		return TryLightFlicker(ScareType.ReliefBeat);
	}

	bool TryLightFlicker(ScareType scareType)
	{
		if (flickerLights == null || flickerLights.Length == 0 || !CanReuse("flicker"))
		{
			return false;
		}

		StartCoroutine(FlickerRoutine());
		MarkUsed("flicker");
		HorrorEvents.RaiseScareTriggered(scareType);
		return true;
	}

	bool TryShadowReveal(ScareType scareType)
	{
		if (shadowRevealObjects == null || shadowRevealObjects.Length == 0 || !CanReuse("shadow"))
		{
			return false;
		}

		GameObject shadowObject = shadowRevealObjects[Random.Range(0, shadowRevealObjects.Length)];
		if (shadowObject == null)
		{
			return false;
		}

		StartCoroutine(ShadowRevealRoutine(shadowObject));
		MarkUsed("shadow");
		HorrorEvents.RaiseScareTriggered(scareType);
		return true;
	}

	bool TryFakeoutAudio(ScareType scareType, AudioSource[] sources)
	{
		if (sources == null || sources.Length == 0 || !CanReuse("audio"))
		{
			return false;
		}

		List<AudioSource> validSources = new List<AudioSource>();
		foreach (AudioSource source in sources)
		{
			if (source != null && source.clip != null)
			{
				validSources.Add(source);
			}
		}

		if (validSources.Count == 0)
		{
			return false;
		}

		AudioSource chosenSource = validSources[Random.Range(0, validSources.Count)];
		chosenSource.PlayOneShot(chosenSource.clip, Mathf.Clamp(Random.Range(0.35f, 0.7f), 0f, 1f));
		MarkUsed("audio");
		HorrorEvents.RaiseScareTriggered(scareType);
		return true;
	}

	bool TryDoorSlam(ScareType scareType)
	{
		if (slamDoorAnimators == null || slamDoorAnimators.Length == 0 || !CanReuse("door"))
		{
			return false;
		}

		List<Animator> validAnimators = new List<Animator>();
		foreach (Animator animator in slamDoorAnimators)
		{
			if (animator != null)
			{
				validAnimators.Add(animator);
			}
		}

		if (validAnimators.Count == 0)
		{
			return false;
		}

		validAnimators[Random.Range(0, validAnimators.Count)].SetTrigger(slamDoorTriggerName);
		MarkUsed("door");
		HorrorEvents.RaiseScareTriggered(scareType);
		return true;
	}

	bool TryRoutePressure()
	{
		if (TryFakeoutAudio(ScareType.RoutePressure, routePressureAudioSources))
		{
			return true;
		}

		if (TryDoorSlam(ScareType.RoutePressure))
		{
			return true;
		}

		return false;
	}

	IEnumerator FlickerRoutine()
	{
		float elapsed = 0f;
		Dictionary<Light, bool> initialStates = new Dictionary<Light, bool>();
		foreach (Light flickerLight in flickerLights)
		{
			if (flickerLight != null)
			{
				initialStates[flickerLight] = flickerLight.enabled;
			}
		}

		while (elapsed < lightFlickerDuration)
		{
			foreach (Light flickerLight in flickerLights)
			{
				if (flickerLight != null)
				{
					flickerLight.enabled = Random.value > 0.35f;
				}
			}

			elapsed += 0.06f;
			yield return new WaitForSeconds(0.06f);
		}

		foreach (KeyValuePair<Light, bool> pair in initialStates)
		{
			if (pair.Key != null)
			{
				pair.Key.enabled = pair.Value;
			}
		}
	}

	IEnumerator ShadowRevealRoutine(GameObject shadowObject)
	{
		SetShadowObjects(false);
		shadowObject.SetActive(true);
		yield return new WaitForSeconds(shadowRevealDuration);
		shadowObject.SetActive(false);
	}

	void SetShadowObjects(bool active)
	{
		if (shadowRevealObjects == null)
		{
			return;
		}

		foreach (GameObject shadowObject in shadowRevealObjects)
		{
			if (shadowObject != null)
			{
				shadowObject.SetActive(active);
			}
		}
	}

	bool CanReuse(string key)
	{
		if (!effectCooldowns.ContainsKey(key))
		{
			return true;
		}

		return Time.time - effectCooldowns[key] >= reuseCooldown;
	}

	void MarkUsed(string key)
	{
		effectCooldowns[key] = Time.time;
	}
}
