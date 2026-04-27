using System.Collections;
using UnityEngine;

public class LongHallEncounter : EncounterBase
{
	[Header("Long Hall")]
	[SerializeField] private float requiredHallLength = 14f;
	[SerializeField] private float hallwayProbeHeight = 1.2f;
	[SerializeField] private float sideWallProbeDistance = 2.5f;
	[SerializeField] private float spawnDistanceAhead = 12f;
	[SerializeField] private float sprintSpeed = 14f;
	[SerializeField] private float jumpscareDistance = 3.1f;
	[SerializeField] private float maxSprintTime = 2.5f;
	[SerializeField] private LayerMask obstacleMask = ~0;

	private bool forceStopped = false;

	public override bool CanTrigger(PreChaseEncounterContext context)
	{
		if (context.playerView == null)
		{
			return false;
		}

		if (!HasLongHall(context.playerView.position, context.playerView.forward, out _))
		{
			return false;
		}

		return true;
	}

	public override IEnumerator Execute(PreChaseEncounterContext context)
	{
		forceStopped = false;
		if (context.villainAI == null || context.player == null)
		{
			yield break;
		}

		if (!context.villainAI.TryAcquireExternalControl("LongHallEncounter"))
		{
			yield break;
		}

		Vector3 forward = new Vector3(context.playerView.forward.x, 0f, context.playerView.forward.z).normalized;
		Vector3 spawnPos = context.player.position + forward * spawnDistanceAhead;
		context.villainAI.SnapToPosition(spawnPos, true);
		Log("Spawned at long hall endpoint and sprinting.");

		float timeoutAt = Time.time + maxSprintTime;
		while (!forceStopped && Time.time < timeoutAt)
		{
			Vector3 current = context.villainAI.transform.position;
			Vector3 target = context.player.position;
			target.y = current.y;
			Vector3 next = Vector3.MoveTowards(current, target, sprintSpeed * Time.deltaTime);
			context.villainAI.SnapToPosition(next, true);

			float distance = Vector3.Distance(next, context.player.position);
			if (distance <= jumpscareDistance)
			{
				break;
			}

			yield return null;
		}

		if (!forceStopped && context.jumpscareSystem != null)
		{
			context.jumpscareSystem.ForceMajorScare(false);
		}

		context.villainAI.TryDisappearFromPlayer("LongHallEncounter completed");
		context.villainAI.ReleaseExternalControl();
	}

	public override void ForceStop()
	{
		forceStopped = true;
	}

	private bool HasLongHall(Vector3 origin, Vector3 forward, out float clearDistance)
	{
		origin.y += hallwayProbeHeight;
		Vector3 planarForward = new Vector3(forward.x, 0f, forward.z).normalized;
		if (planarForward.sqrMagnitude <= 0.001f)
		{
			clearDistance = 0f;
			return false;
		}

		if (Physics.Raycast(origin, planarForward, out RaycastHit forwardHit, requiredHallLength, obstacleMask))
		{
			clearDistance = forwardHit.distance;
			return false;
		}

		Vector3 right = Vector3.Cross(Vector3.up, planarForward).normalized;
		bool leftWall = Physics.Raycast(origin, -right, sideWallProbeDistance, obstacleMask);
		bool rightWall = Physics.Raycast(origin, right, sideWallProbeDistance, obstacleMask);
		clearDistance = requiredHallLength;
		return leftWall && rightWall;
	}
}
