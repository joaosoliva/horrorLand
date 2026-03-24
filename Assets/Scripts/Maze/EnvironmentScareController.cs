using System.Collections.Generic;
using UnityEngine;

public enum DoorEventType
{
	Opened,
	ClosedLocked
}

public class EnvironmentScareController : MonoBehaviour
{
	[Header("References")]
	public Transform player;

	[Header("Global Cooldowns")]
	public float globalScareCooldown = 1.25f;
	public float perZoneCooldown = 5f;
	public bool debugLogs = false;

	[Header("Room Entry Scares")]
	[Range(0f, 1f)] public float roomEntryFlickerChance = 0.65f;
	[Range(0f, 1f)] public float roomRevisitFakeoutChance = 0.45f;
	public float entryFlickerDuration = 0.75f;

	[Header("Room Linger Scares")]
	[Range(0f, 1f)] public float lingerLightCutChance = 0.55f;
	public float lingerLightsOffDuration = 2f;
	public float lingerDelayedLightRestore = 1.25f;

	[Header("Door Scares")]
	[Range(0f, 1f)] public float closeBehindDoorChance = 0.65f;
	[Range(0f, 1f)] public float lightsOffBehindDoorChance = 0.55f;
	public float closeBehindDelay = 0.35f;
	public float behindDoorLightOffDuration = 1.8f;

	[Header("Audio Fallback")]
	public AudioSource[] fakeoutAudioSources;
	public AudioSource[] routePressureAudioSources;
	public AudioSource reliefAudioSource;

	private readonly List<ProceduralRoomScareNode> roomNodes = new List<ProceduralRoomScareNode>();
	private readonly List<ProceduralScareDoor> scareDoors = new List<ProceduralScareDoor>();
	private readonly List<ILightInteractable> lights = new List<ILightInteractable>();
	private readonly Dictionary<string, float> zoneLastTriggerTime = new Dictionary<string, float>();
	private float lastGlobalTriggerTime = -999f;

	void Start()
	{
		if (player == null)
		{
			GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
			if (playerObject != null)
			{
				player = playerObject.transform;
			}
		}

		AutoRegisterExistingProceduralElements();
	}

	public void RegisterRoomNode(ProceduralRoomScareNode node)
	{
		if (node == null || roomNodes.Contains(node))
		{
			return;
		}

		roomNodes.Add(node);
		node.BindController(this);
		if (debugLogs)
		{
			Debug.Log("EnvironmentScareController registered room node: " + node.zoneId);
		}
	}

	public void RegisterDoor(ProceduralScareDoor door)
	{
		if (door == null || scareDoors.Contains(door))
		{
			return;
		}

		scareDoors.Add(door);
		door.BindController(this);
		if (debugLogs)
		{
			Debug.Log("EnvironmentScareController registered door: " + door.doorId);
		}
	}

	public void RegisterLight(ILightInteractable lightInteractable)
	{
		if (lightInteractable == null || lights.Contains(lightInteractable))
		{
			return;
		}

		lights.Add(lightInteractable);
	}

	public void HandleRoomEntered(ProceduralRoomScareNode roomNode)
	{
		if (roomNode == null || !CanTrigger(roomNode.zoneId))
		{
			return;
		}

		if (roomNode.VisitState == ProceduralRoomScareNode.RoomVisitState.Entered && Random.value <= roomEntryFlickerChance)
		{
			TriggerLightsInZone(roomNode.zoneId, entryFlickerDuration, turnOff: false);
			HorrorEvents.RaiseScareTriggered(ScareType.PresenceCue);
			RegisterTrigger(roomNode.zoneId);
			return;
		}

		if (roomNode.VisitState == ProceduralRoomScareNode.RoomVisitState.Revisited && Random.value <= roomRevisitFakeoutChance)
		{
			PlayRandomAudio(fakeoutAudioSources, 0.55f);
			HorrorEvents.RaiseScareTriggered(ScareType.Fakeout);
			RegisterTrigger(roomNode.zoneId);
		}
	}

	public void HandleRoomExited(ProceduralRoomScareNode roomNode)
	{
		if (roomNode != null && roomNode.VisitState == ProceduralRoomScareNode.RoomVisitState.Entered)
		{
			roomNode.MarkCleared();
		}
	}

	public void HandleRoomLinger(ProceduralRoomScareNode roomNode)
	{
		if (roomNode == null || !CanTrigger(roomNode.zoneId))
		{
			return;
		}

		if (Random.value <= lingerLightCutChance)
		{
			TriggerLightsInZone(roomNode.zoneId, lingerLightsOffDuration, turnOff: true);
			HorrorEvents.RaiseScareTriggered(ScareType.MinorPsychological);
			RegisterTrigger(roomNode.zoneId);
		}
	}

	public void HandleDoorEvent(ProceduralScareDoor door, DoorEventType doorEventType)
	{
		if (door == null || !CanTrigger(door.zoneId))
		{
			return;
		}

		if (doorEventType == DoorEventType.Opened)
		{
			if (Random.value <= closeBehindDoorChance)
			{
				door.ActivateScare(this, ScareType.RoutePressure);
			}
			if (Random.value <= lightsOffBehindDoorChance)
			{
				TriggerLightsInZone(door.zoneId, behindDoorLightOffDuration, turnOff: true);
			}
			HorrorEvents.RaiseScareTriggered(ScareType.RoutePressure);
			RegisterTrigger(door.zoneId);
		}
	}

	public bool TryTriggerRoomContextScare(ProceduralRoomScareNode roomNode, ScareType scareType)
	{
		if (roomNode == null || !CanTrigger(roomNode.zoneId))
		{
			return false;
		}

		if (scareType == ScareType.PresenceCue)
		{
			TriggerLightsInZone(roomNode.zoneId, entryFlickerDuration, turnOff: false);
		}
		else if (scareType == ScareType.RoutePressure)
		{
			PlayRandomAudio(routePressureAudioSources, 0.7f);
		}
		else if (scareType == ScareType.ReliefBeat)
		{
			if (reliefAudioSource != null && reliefAudioSource.clip != null)
			{
				reliefAudioSource.PlayOneShot(reliefAudioSource.clip, 0.45f);
			}
		}
		else
		{
			PlayRandomAudio(fakeoutAudioSources, 0.45f);
		}

		HorrorEvents.RaiseScareTriggered(scareType);
		RegisterTrigger(roomNode.zoneId);
		return true;
	}

	public bool TryPlayPresenceScare(EncounterCategory category)
	{
		ProceduralRoomScareNode roomNode = GetNearestRoomNode();
		if (roomNode != null)
		{
			return TryTriggerRoomContextScare(roomNode, ScareType.PresenceCue);
		}

		return PlayRandomAudio(fakeoutAudioSources, 0.45f);
	}

	public bool TryPlayProbeScare(EncounterCategory category)
	{
		ProceduralRoomScareNode roomNode = GetNearestRoomNode();
		if (roomNode != null)
		{
			ScareType scareType = category == EncounterCategory.RoutePressure ? ScareType.RoutePressure : ScareType.Fakeout;
			return TryTriggerRoomContextScare(roomNode, scareType);
		}

		return PlayRandomAudio(routePressureAudioSources, 0.55f);
	}

	public bool TryPlayReleaseBeat()
	{
		ProceduralRoomScareNode roomNode = GetNearestRoomNode();
		if (roomNode != null)
		{
			return TryTriggerRoomContextScare(roomNode, ScareType.ReliefBeat);
		}

		if (reliefAudioSource != null && reliefAudioSource.clip != null)
		{
			reliefAudioSource.PlayOneShot(reliefAudioSource.clip, 0.45f);
			HorrorEvents.RaiseScareTriggered(ScareType.ReliefBeat);
			return true;
		}

		return false;
	}

	void AutoRegisterExistingProceduralElements()
	{
		ProceduralRoomScareNode[] existingRoomNodes = FindObjectsOfType<ProceduralRoomScareNode>();
		for (int i = 0; i < existingRoomNodes.Length; i++)
		{
			RegisterRoomNode(existingRoomNodes[i]);
		}

		ProceduralScareDoor[] existingDoors = FindObjectsOfType<ProceduralScareDoor>();
		for (int i = 0; i < existingDoors.Length; i++)
		{
			RegisterDoor(existingDoors[i]);
		}

		ProceduralLamp[] existingLamps = FindObjectsOfType<ProceduralLamp>();
		for (int i = 0; i < existingLamps.Length; i++)
		{
			RegisterLight(existingLamps[i]);
		}
	}

	void TriggerLightsInZone(string zoneId, float duration, bool turnOff)
	{
		for (int i = 0; i < lights.Count; i++)
		{
			ILightInteractable lightInteractable = lights[i];
			if (lightInteractable == null)
			{
				continue;
			}

			ProceduralLamp lamp = lightInteractable as ProceduralLamp;
			if (lamp != null && !string.IsNullOrEmpty(zoneId) && lamp.zoneId != zoneId)
			{
				continue;
			}

			if (turnOff)
			{
				lightInteractable.TurnOff(duration);
				lightInteractable.DelayedActivate(duration + lingerDelayedLightRestore);
			}
			else
			{
				lightInteractable.TriggerFlicker(duration);
			}
		}
	}

	ProceduralRoomScareNode GetNearestRoomNode()
	{
		if (player == null || roomNodes.Count == 0)
		{
			return null;
		}

		ProceduralRoomScareNode nearest = null;
		float nearestDistance = float.MaxValue;
		for (int i = 0; i < roomNodes.Count; i++)
		{
			ProceduralRoomScareNode node = roomNodes[i];
			if (node == null)
			{
				continue;
			}

			float distance = Vector3.Distance(player.position, node.transform.position);
			if (distance < nearestDistance)
			{
				nearest = node;
				nearestDistance = distance;
			}
		}

		return nearest;
	}

	bool PlayRandomAudio(AudioSource[] sources, float volume)
	{
		if (sources == null || sources.Length == 0)
		{
			return false;
		}

		List<AudioSource> validSources = new List<AudioSource>();
		for (int i = 0; i < sources.Length; i++)
		{
			if (sources[i] != null && sources[i].clip != null)
			{
				validSources.Add(sources[i]);
			}
		}

		if (validSources.Count == 0)
		{
			return false;
		}

		AudioSource source = validSources[Random.Range(0, validSources.Count)];
		source.PlayOneShot(source.clip, Mathf.Clamp01(volume));
		return true;
	}

	bool CanTrigger(string zoneId)
	{
		if (Time.time - lastGlobalTriggerTime < globalScareCooldown)
		{
			return false;
		}

		if (string.IsNullOrEmpty(zoneId))
		{
			return true;
		}

		if (!zoneLastTriggerTime.ContainsKey(zoneId))
		{
			return true;
		}

		return Time.time - zoneLastTriggerTime[zoneId] >= perZoneCooldown;
	}

	void RegisterTrigger(string zoneId)
	{
		lastGlobalTriggerTime = Time.time;
		if (!string.IsNullOrEmpty(zoneId))
		{
			zoneLastTriggerTime[zoneId] = Time.time;
		}
	}
}
