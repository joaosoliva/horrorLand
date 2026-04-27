using System.Collections;
using UnityEngine;

public class BehindBackEncounter : EncounterBase
{
	[Header("Behind Back")]
	[SerializeField] private float spawnDistanceBehindPlayer = 4f;
	[SerializeField] private float lateralVariance = 1f;
	[SerializeField] private float maxWaitForReveal = 2.5f;
	[SerializeField] private LayerMask groundMask = ~0;

	private bool forceStopped = false;

	public override bool CanTrigger(PreChaseEncounterContext context)
	{
		if (context.villainAI == null || context.playerView == null)
		{
			return false;
		}

		return TryGetSpawnPosition(context.playerView, out _);
	}

	public override IEnumerator Execute(PreChaseEncounterContext context)
	{
		forceStopped = false;
		if (!context.villainAI.TryAcquireExternalControl("BehindBackEncounter"))
		{
			yield break;
		}

		if (!TryGetSpawnPosition(context.playerView, out Vector3 spawnPos))
		{
			context.villainAI.ReleaseExternalControl();
			yield break;
		}

		context.villainAI.SnapToPosition(spawnPos, true);
		Log("Spawned behind player.");

		float timeoutAt = Time.time + maxWaitForReveal;
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

		context.villainAI.TryDisappearFromPlayer("BehindBackEncounter completed");
		context.villainAI.ReleaseExternalControl();
	}

	public override void ForceStop()
	{
		forceStopped = true;
	}

	private bool TryGetSpawnPosition(Transform playerView, out Vector3 spawnPosition)
	{
		Vector3 backward = -new Vector3(playerView.forward.x, 0f, playerView.forward.z).normalized;
		Vector3 right = new Vector3(playerView.right.x, 0f, playerView.right.z).normalized;
		Vector3 candidate = playerView.position + backward * spawnDistanceBehindPlayer + right * Random.Range(-lateralVariance, lateralVariance);

		if (Physics.Raycast(candidate + Vector3.up * 6f, Vector3.down, out RaycastHit hit, 12f, groundMask))
		{
			spawnPosition = hit.point;
			return true;
		}

		spawnPosition = candidate;
		return true;
	}
}
