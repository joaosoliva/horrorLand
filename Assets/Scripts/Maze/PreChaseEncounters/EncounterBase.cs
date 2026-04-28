using System.Collections;
using UnityEngine;

public abstract class EncounterBase : MonoBehaviour
{
    [Header("Encounter Identity")]
    [SerializeField] private string encounterId = "Encounter";
    [SerializeField] private float localCooldown = 12f;

    protected EncounterManager Manager { get; private set; }
    protected VillainAI Villain => Manager != null ? Manager.Villain : null;
    protected Transform Player => Manager != null ? Manager.Player : null;

    private float lastExecutionTime = -999f;

    public string EncounterId => encounterId;
    public bool IsOnLocalCooldown => Time.time < lastExecutionTime + localCooldown;

    public void Bind(EncounterManager manager)
    {
        Manager = manager;
    }

    public bool CanRun(EncounterContext context)
    {
        if (Manager == null || IsOnLocalCooldown)
        {
            return false;
        }

        return CanTrigger(context);
    }

    public IEnumerator Run(EncounterContext context)
    {
        lastExecutionTime = Time.time;
        yield return Execute(context);
    }

    public virtual void ForceStop()
    {
    }

    protected abstract bool CanTrigger(EncounterContext context);
    protected abstract IEnumerator Execute(EncounterContext context);
}

public readonly struct EncounterContext
{
    public readonly VillainAI.AIState State;
    public readonly float DistanceToPlayer;
    public readonly bool VillainSeesPlayer;
    public readonly bool PlayerSeesVillain;
    public readonly MazeContextSnapshot MazeContext;

    public EncounterContext(VillainAI.AIState state, float distanceToPlayer, bool villainSeesPlayer, bool playerSeesVillain, MazeContextSnapshot mazeContext)
    {
        State = state;
        DistanceToPlayer = distanceToPlayer;
        VillainSeesPlayer = villainSeesPlayer;
        PlayerSeesVillain = playerSeesVillain;
        MazeContext = mazeContext;
    }
}
