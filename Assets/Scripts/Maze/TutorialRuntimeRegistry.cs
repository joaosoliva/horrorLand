using System.Collections.Generic;
using UnityEngine;

public enum TutorialRuntimeRole
{
    SoundboardDoorGate,
    SoundboardUseDoor,
    CorruptionDoor,
    LightDoorGate,
    ChaseGate,
    SprintDoor,
    TutorialExitGate,
    ExitDoor,
    SoundboardPickup,
    TutorialLightSpot,
    VillainAI,
    EncounterManager,
    MonsterSpawnPoint,
    MonsterRevealPoint,
    MainMazeConnector
}

public class TutorialRuntimeRegistry : MonoBehaviour
{
    private readonly Dictionary<TutorialRuntimeRole, Object> entries = new Dictionary<TutorialRuntimeRole, Object>();

    public static TutorialRuntimeRegistry Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void Register(TutorialRuntimeRole role, Object value)
    {
        if (value == null)
        {
            return;
        }

        entries[role] = value;
    }

    public bool TryGet<T>(TutorialRuntimeRole role, out T value) where T : Object
    {
        if (entries.TryGetValue(role, out Object raw) && raw is T typed)
        {
            value = typed;
            return true;
        }

        value = null;
        return false;
    }
}
