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
    private readonly Dictionary<TutorialRuntimeRole, int> registrationCounts = new Dictionary<TutorialRuntimeRole, int>();

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
        Register(role, value, "unknown");
    }

    public void Register(TutorialRuntimeRole role, Object value, string source)
    {
        if (value == null)
        {
            return;
        }

        int nextCount = registrationCounts.TryGetValue(role, out int count) ? count + 1 : 1;
        string hierarchyPath = GetHierarchyPath(value);
        int instanceId = value.GetInstanceID();
        float t = Time.time;
        registrationCounts[role] = nextCount;
        if (nextCount > 1)
        {
            Debug.LogError("[TutorialRuntimeRegistry] Duplicate semantic registration: role=" + role + ", count=" + nextCount + ", source=" + source + ", instanceId=" + instanceId + ", path=" + hierarchyPath + ", t=" + t + "\nStack:" + new System.Diagnostics.StackTrace(1, true));
        }

        if (entries.TryGetValue(role, out Object existing) && existing != null && existing != value)
        {
            Debug.LogWarning("[TutorialRuntimeRegistry] Role reassigned: " + role + " from " + existing.name + " to " + value.name + " via " + source);
        }

        Debug.Log("[TutorialRuntimeRegistry] Register role=" + role + ", source=" + source + ", instanceId=" + instanceId + ", path=" + hierarchyPath + ", t=" + t);
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


    public bool ValidateUniqueRegistrations(out string report)
    {
        StringBuilder sb = new StringBuilder();
        bool valid = true;
        foreach (var pair in registrationCounts)
        {
            if (pair.Value > 1)
            {
                valid = false;
                sb.AppendLine("Duplicate role registration: " + pair.Key + " count=" + pair.Value);
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
            int count = registrationCounts.TryGetValue(pair.Key, out int roleCount) ? roleCount : 0;
            sb.AppendLine(" - " + pair.Key + " => " + (pair.Value != null ? pair.Value.name : "null") + ", registrations=" + count + ", instanceId=" + (pair.Value != null ? pair.Value.GetInstanceID().ToString() : "null") + ", path=" + GetHierarchyPath(pair.Value));
        }

        if (!ValidateRequiredRoles(out string missingReport))
        {
            sb.AppendLine("Required role validation failed:");
            sb.Append(missingReport);
            Debug.LogWarning(sb.ToString());
            return;
        }

        if (!ValidateUniqueRegistrations(out string duplicateReport))
        {
            sb.AppendLine("Unique registration validation failed:");
            sb.Append(duplicateReport);
            Debug.LogError(sb.ToString());
            return;
        }

        sb.AppendLine("Required role validation passed.");
        sb.AppendLine("Unique registration validation passed.");
        Debug.Log(sb.ToString());
    }

    private static string GetHierarchyPath(Object obj)
    {
        if (!(obj is Component component))
        {
            return obj != null ? obj.name : "null";
        }

        Transform t = component.transform;
        StringBuilder sb = new StringBuilder(t.name);
        while (t.parent != null)
        {
            t = t.parent;
            sb.Insert(0, t.name + "/");
        }

        return sb.ToString();
    }
}

