using System.Collections;
using UnityEngine;

public abstract class EncounterBase : MonoBehaviour
{
	[SerializeField] private string encounterId = "encounter";
	[SerializeField] private float minRepeatDelay = 20f;
	[SerializeField] protected bool enableDebugLogs = false;

	public string EncounterId => encounterId;
	public float MinRepeatDelay => minRepeatDelay;

	protected EncounterManager manager;

	public virtual void Initialize(EncounterManager encounterManager)
	{
		manager = encounterManager;
	}

	public abstract bool CanTrigger(PreChaseEncounterContext context);
	public abstract IEnumerator Execute(PreChaseEncounterContext context);
	public abstract void ForceStop();

	protected void Log(string message)
	{
		if (!enableDebugLogs)
		{
			return;
		}

		Debug.Log($"[PreChaseEncounter::{encounterId}] {message}");
	}
}

public struct PreChaseEncounterContext
{
	public VillainAI villainAI;
	public JumpscareSystem jumpscareSystem;
	public ChaseSystem chaseSystem;
	public Transform player;
	public Transform playerView;
	public float distanceToPlayer;
	public float timestamp;
}
