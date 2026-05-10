using System.Collections.Generic;
using System.Text;
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

        if (entries.TryGetValue(role, out Object existing) && existing != null && existing != value)
        {
            Debug.LogWarning("[TutorialRuntimeRegistry] Role reassigned: " + role + " from " + existing.name + " to " + value.name);
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

    public bool Has(TutorialRuntimeRole role)
    {
        return entries.ContainsKey(role) && entries[role] != null;
    }

    public bool ValidateRequiredRoles(out string report)
    {
        TutorialRuntimeRole[] required =
        {
            TutorialRuntimeRole.SoundboardDoorGate,
            TutorialRuntimeRole.SoundboardUseDoor,
            TutorialRuntimeRole.CorruptionDoor,
            TutorialRuntimeRole.LightDoorGate,
            TutorialRuntimeRole.ChaseGate,
            TutorialRuntimeRole.SprintDoor,
            TutorialRuntimeRole.TutorialExitGate,
            TutorialRuntimeRole.ExitDoor,
            TutorialRuntimeRole.SoundboardPickup,
            TutorialRuntimeRole.TutorialLightSpot
        };

        StringBuilder sb = new StringBuilder();
        bool valid = true;
        for (int i = 0; i < required.Length; i++)
        {
            TutorialRuntimeRole role = required[i];
            if (!Has(role))
            {
                valid = false;
                sb.AppendLine("Missing role: " + role);
            }
        }

        report = sb.ToString();
        return valid;
    }

    public void LogRegistrationReport(string context)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("[TutorialRuntimeRegistry] Registration report: " + context);
        foreach (var pair in entries)
        {
            sb.AppendLine(" - " + pair.Key + " => " + (pair.Value != null ? pair.Value.name : "null"));
        }

        if (!ValidateRequiredRoles(out string missingReport))
        {
            sb.AppendLine("Required role validation failed:");
            sb.Append(missingReport);
            Debug.LogWarning(sb.ToString());
            return;
        }

        sb.AppendLine("Required role validation passed.");
        Debug.Log(sb.ToString());
    }
}
