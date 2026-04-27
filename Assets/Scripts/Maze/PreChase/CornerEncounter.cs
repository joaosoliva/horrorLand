using System.Collections;
using UnityEngine;

public class CornerEncounter : EncounterBase
{
	[Header("Corner")]
	[SerializeField] private float forwardBlockedDistance = 2.5f;
	[SerializeField] private float sideOpenDistance = 4f;
	[SerializeField] private float cornerOffsetForward = 1.1f;
	[SerializeField] private float cornerOffsetSide = 2f;
	[SerializeField] private float maxRevealWait = 2f;
	[SerializeField] private LayerMask obstacleMask = ~0;

	private bool forceStopped = false;
	private int cachedSide = 0;

	public override bool CanTrigger(PreChaseEncounterContext context)
	{
		cachedSide = DetectCornerSide(context.playerView);
		return cachedSide != 0;
	}

	public override IEnumerator Execute(PreChaseEncounterContext context)
	{
		forceStopped = false;
		if (context.villainAI == null || context.playerView == null)
		{
			yield break;
		}

		if (!context.villainAI.TryAcquireExternalControl("CornerEncounter"))
		{
			yield break;
		}

		if (cachedSide == 0)
		{
			cachedSide = DetectCornerSide(context.playerView);
		}

		if (cachedSide == 0)
		{
			context.villainAI.ReleaseExternalControl();
			yield break;
		}

		Vector3 forward = new Vector3(context.playerView.forward.x, 0f, context.playerView.forward.z).normalized;
		Vector3 right = new Vector3(context.playerView.right.x, 0f, context.playerView.right.z).normalized;
		Vector3 spawn = context.playerView.position + forward * cornerOffsetForward + right * (cornerOffsetSide * cachedSide);
		context.villainAI.SnapToPosition(spawn, true);
		Log("Spawned around blind corner.");

		float timeoutAt = Time.time + maxRevealWait;
		while (!forceStopped && Time.time < timeoutAt)
		{
			if (context.villainAI.CanPlayerSeeVillain())
			{
				break;
			}
			yield return null;
		}

		if (!forceStopped && context.jumpscareSystem != null)
		{
			context.jumpscareSystem.ForceMajorScare(false);
		}

		context.villainAI.TryDisappearFromPlayer("CornerEncounter completed");
		context.villainAI.ReleaseExternalControl();
	}

	public override void ForceStop()
	{
		forceStopped = true;
	}

	private int DetectCornerSide(Transform playerView)
	{
		if (playerView == null)
		{
			return 0;
		}

		Vector3 origin = playerView.position + Vector3.up * 1.1f;
		Vector3 forward = new Vector3(playerView.forward.x, 0f, playerView.forward.z).normalized;
		Vector3 right = new Vector3(playerView.right.x, 0f, playerView.right.z).normalized;

		bool forwardBlocked = Physics.Raycast(origin, forward, forwardBlockedDistance, obstacleMask);
		if (!forwardBlocked)
		{
			return 0;
		}

		bool rightOpen = !Physics.Raycast(origin, right, sideOpenDistance, obstacleMask);
		bool leftOpen = !Physics.Raycast(origin, -right, sideOpenDistance, obstacleMask);

		if (rightOpen == leftOpen)
		{
			return 0;
		}

		return rightOpen ? 1 : -1;
	}
}
