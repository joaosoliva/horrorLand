using UnityEngine;

[RequireComponent(typeof(DoorTrigger))]
public class ProceduralScareDoor : MonoBehaviour, IScareTrigger
{
	[Header("Door Identity")]
	public string doorId;
	public string zoneId;

	[Header("Scare Settings")]
	public float closeBehindDelay = 0.4f;

	public string TriggerId => doorId;
	public bool IsActive => enabled && gameObject.activeInHierarchy;
	public Vector3 Position => transform.position;

	private DoorTrigger doorTrigger;
	private EnvironmentScareController controller;

	void Awake()
	{
		doorTrigger = GetComponent<DoorTrigger>();
		if (string.IsNullOrEmpty(doorId))
		{
			doorId = gameObject.name;
		}
	}

	void OnEnable()
	{
		if (doorTrigger != null)
		{
			doorTrigger.OnDoorOpened += HandleDoorOpened;
			doorTrigger.OnDoorClosedLocked += HandleDoorClosedLocked;
		}
	}

	void OnDisable()
	{
		if (doorTrigger != null)
		{
			doorTrigger.OnDoorOpened -= HandleDoorOpened;
			doorTrigger.OnDoorClosedLocked -= HandleDoorClosedLocked;
		}
	}

	public void BindController(EnvironmentScareController scareController)
	{
		controller = scareController;
	}

	public void ActivateScare(EnvironmentScareController scareController, ScareType scareType)
	{
		if (doorTrigger == null)
		{
			return;
		}

		if (scareType == ScareType.RoutePressure || scareType == ScareType.MinorPsychological)
		{
			doorTrigger.ForceCloseAndLock(closeBehindDelay);
		}
	}

	void HandleDoorOpened()
	{
		if (controller != null)
		{
			controller.HandleDoorEvent(this, DoorEventType.Opened);
		}
	}

	void HandleDoorClosedLocked()
	{
		if (controller != null)
		{
			controller.HandleDoorEvent(this, DoorEventType.ClosedLocked);
		}
	}
}
