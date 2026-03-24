using UnityEngine;

[RequireComponent(typeof(Collider))]
public class HidingSpot : MonoBehaviour
{
	[Header("Hiding Tuning")]
	[Range(0f, 1f)] public float concealmentStrength = 0.9f;
	public float maxSafeDuration = 4f;
	public float reuseLockoutDuration = 6f;
	public bool requiresCrouch = false;
	public bool debugLogs = false;

	private Transform concealedPlayer;
	private float concealStartTime = -999f;
	private float lockoutUntilTime = -999f;

	public bool IsAvailable => Time.time >= lockoutUntilTime;
	public bool IsOccupied => concealedPlayer != null;
	public float OccupiedDuration => IsOccupied ? Time.time - concealStartTime : 0f;

	void Reset()
	{
		Collider triggerCollider = GetComponent<Collider>();
		if (triggerCollider != null)
		{
			triggerCollider.isTrigger = true;
		}
	}

	void Update()
	{
		if (!IsOccupied)
		{
			return;
		}

		if (Time.time - concealStartTime >= maxSafeDuration)
		{
			if (debugLogs)
			{
				Debug.Log("HidingSpot safe duration expired.");
			}
			ReleaseConcealment();
		}
	}

	void OnTriggerEnter(Collider other)
	{
		if (!IsAvailable || IsOccupied || !other.CompareTag("Player"))
		{
			return;
		}

		concealedPlayer = other.transform;
		concealStartTime = Time.time;
		if (debugLogs)
		{
			Debug.Log("Player concealed in hiding spot.");
		}
	}

	void OnTriggerExit(Collider other)
	{
		if (concealedPlayer != null && other.transform == concealedPlayer)
		{
			ReleaseConcealment();
		}
	}

	void ReleaseConcealment()
	{
		if (concealedPlayer == null)
		{
			return;
		}

		concealedPlayer = null;
		concealStartTime = -999f;
		lockoutUntilTime = Time.time + reuseLockoutDuration;
	}

	public bool IsConcealing(Transform target)
	{
		if (!IsAvailable || concealedPlayer == null || target == null)
		{
			return false;
		}

		return concealedPlayer == target;
	}

	public static bool IsPlayerConcealed(Transform player)
	{
		if (player == null)
		{
			return false;
		}

		HidingSpot[] spots = FindObjectsOfType<HidingSpot>();
		for (int i = 0; i < spots.Length; i++)
		{
			if (spots[i] != null && spots[i].IsConcealing(player))
			{
				return true;
			}
		}

		return false;
	}
}
