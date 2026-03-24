using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ProceduralRoomScareNode : MonoBehaviour, IScareTrigger
{
	public enum RoomVisitState
	{
		Unvisited,
		Entered,
		Cleared,
		Revisited
	}

	[Header("Node Identity")]
	public string zoneId;
	public bool isCorridorNode = false;
	public float lingerTriggerSeconds = 3.5f;

	[Header("Debug")]
	public bool debugLogs = false;

	public string TriggerId => zoneId;
	public bool IsActive => enabled && gameObject.activeInHierarchy;
	public Vector3 Position => transform.position;
	public RoomVisitState VisitState => visitState;

	private RoomVisitState visitState = RoomVisitState.Unvisited;
	private EnvironmentScareController controller;
	private Coroutine lingerCoroutine;

	void Reset()
	{
		Collider triggerCollider = GetComponent<Collider>();
		if (triggerCollider != null)
		{
			triggerCollider.isTrigger = true;
		}
	}

	public void BindController(EnvironmentScareController scareController)
	{
		controller = scareController;
	}

	public void MarkCleared()
	{
		visitState = RoomVisitState.Cleared;
	}

	public void ActivateScare(EnvironmentScareController scareController, ScareType scareType)
	{
		if (controller == null)
		{
			controller = scareController;
		}

		if (controller != null)
		{
			controller.TryTriggerRoomContextScare(this, scareType);
		}
	}

	void OnTriggerEnter(Collider other)
	{
		if (!other.CompareTag("Player"))
		{
			return;
		}

		if (visitState == RoomVisitState.Unvisited)
		{
			visitState = RoomVisitState.Entered;
		}
		else if (visitState == RoomVisitState.Cleared)
		{
			visitState = RoomVisitState.Revisited;
		}

		if (controller != null)
		{
			controller.HandleRoomEntered(this);
		}

		if (lingerCoroutine != null)
		{
			StopCoroutine(lingerCoroutine);
		}
		lingerCoroutine = StartCoroutine(LingerRoutine());
	}

	void OnTriggerExit(Collider other)
	{
		if (!other.CompareTag("Player"))
		{
			return;
		}

		if (lingerCoroutine != null)
		{
			StopCoroutine(lingerCoroutine);
			lingerCoroutine = null;
		}

		if (controller != null)
		{
			controller.HandleRoomExited(this);
		}
	}

	IEnumerator LingerRoutine()
	{
		yield return new WaitForSeconds(Mathf.Max(0.5f, lingerTriggerSeconds));
		if (controller != null)
		{
			controller.HandleRoomLinger(this);
		}
		lingerCoroutine = null;
	}
}
