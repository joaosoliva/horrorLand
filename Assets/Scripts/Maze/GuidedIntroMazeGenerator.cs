using System.Collections.Generic;
using HorrorLand.MenuSystem;
using UnityEngine;

public class GuidedIntroMazeGenerator : MonoBehaviour
{
    [Header("Seed")]
    public bool useFixedTutorialSeed = true;
    public int fixedTutorialSeed = 1998;
    public bool allowReplayRandomization = false;

    [Header("Build")]
    public bool generateOnStart = true;
    public bool enableTutorialGenerationDebug = true;
    public float corridorLength = 11f;
    public float corridorWidth = 4.5f;
    public float wallHeight = 3.2f;

    [Header("Prefabs")]
    public GameObject soundboardPickupPrefab;
    public GameObject safeLightPrefab;
    public GameObject gatePrefab;

    [Header("Runtime")]
    public Transform player;
    public VillainAI villainAI;
    public IntroTapeController tutorialController;

    private readonly List<GameObject> generatedObjects = new List<GameObject>();
    private TutorialLayoutContext generatedContext;
    private int runtimeSeed;

    public TutorialLayoutContext GeneratedContext => generatedContext;

    void Start()
    {
        if (!generateOnStart)
        {
            return;
        }

        GenerateLayout();
    }

    public TutorialLayoutContext GenerateLayout()
    {
        CleanupGeneratedObjects();
        ResolveReferences();

        runtimeSeed = ResolveSeed();
        Random.InitState(runtimeSeed);
        Log($"Generating tutorial layout with seed={runtimeSeed}");

        generatedContext = BuildLayout();
        bool valid = ValidateLayout(generatedContext, out List<string> errors);
        if (!valid)
        {
            for (int i = 0; i < errors.Count; i++)
            {
                Debug.LogError("[GuidedIntroMazeGenerator] " + errors[i]);
            }

            Random.InitState(fixedTutorialSeed);
            generatedContext = BuildLayout();
            ValidateLayout(generatedContext, out _);
            Log("Fallback deterministic layout applied.");
        }

        PlacePlayerAtSpawn(generatedContext);

        if (tutorialController != null)
        {
            tutorialController.ApplyGeneratedLayout(generatedContext);
        }

        return generatedContext;
    }

    private void ResolveReferences()
    {
        if (tutorialController == null)
        {
            tutorialController = FindObjectOfType<IntroTapeController>();
        }

        if (villainAI == null)
        {
            villainAI = FindObjectOfType<VillainAI>();
        }

        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                player = playerObj.transform;
            }
        }
    }

    private int ResolveSeed()
    {
        bool isReplay = PlayerPrefs.GetInt(MenuPrefsKeys.ForceTutorialReplay, 0) == 1;
        if (useFixedTutorialSeed && (!isReplay || !allowReplayRandomization))
        {
            return fixedTutorialSeed;
        }

        return System.DateTime.UtcNow.Millisecond + Random.Range(1, 999999);
    }

    private TutorialLayoutContext BuildLayout()
    {
        TutorialLayoutContext context = new TutorialLayoutContext();

        Vector3 basePos = transform.position;
        Vector3 forward = transform.forward.sqrMagnitude <= 0.001f ? Vector3.forward : transform.forward.normalized;
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

        float jitter = Random.Range(-1.1f, 1.1f);
        float segment = corridorLength;

        Vector3 startPos = basePos;
        Vector3 soundboardPos = startPos + forward * segment + right * jitter;
        Vector3 useGatePos = soundboardPos + forward * (segment * 0.7f);
        Vector3 corruptionPos = useGatePos + forward * (segment * 0.8f);
        Vector3 lightPos = corruptionPos + forward * segment;
        Vector3 revealPos = lightPos + forward * segment;
        Vector3 hidePos = revealPos + forward * (segment * 0.9f);
        Vector3 sprintPos = hidePos + forward * (segment * 0.8f);
        Vector3 exitPos = sprintPos + forward * (segment * 0.85f);
        Vector3 connectorPos = exitPos + forward * (segment * 0.75f);

        if (enableTutorialGenerationDebug)
        {
            Log($"Segment TutorialStartCell at {startPos}");
            Log($"Segment SoundboardPickupRoom at {soundboardPos}");
            Log($"Segment SoundboardUseGate at {useGatePos}");
            Log($"Segment CorruptionDemoCorridor at {corruptionPos}");
            Log($"Segment LightSpotRoom at {lightPos}");
            Log($"Segment MonsterRevealHall at {revealPos}");
            Log($"Segment HideFromMonsterChamber at {hidePos}");
            Log($"Segment SprintRiskCorridor at {sprintPos}");
            Log($"Segment TutorialExitGate at {exitPos}");
            Log($"MainMazeConnector at {connectorPos}");
        }

        GameObject root = CreateRoot("GeneratedTutorialLayout");
        BuildSegmentRoom(root.transform, "TutorialStartCell", startPos, segment, TutorialZoneType.TutorialSafeZone, context);
        BuildSegmentRoom(root.transform, "SoundboardPickupRoom", soundboardPos, segment, TutorialZoneType.EncounterDisabledZone, context);
        BuildSegmentRoom(root.transform, "SoundboardUseGate", useGatePos, segment * 0.7f, TutorialZoneType.SanityDrainZone, context);
        BuildSegmentRoom(root.transform, "CorruptionDemoCorridor", corruptionPos, segment * 0.8f, TutorialZoneType.CorruptionDemoZone, context);
        BuildSegmentRoom(root.transform, "LightSpotRoom", lightPos, segment, TutorialZoneType.LightSpotZone, context);
        BuildSegmentRoom(root.transform, "MonsterRevealHall", revealPos, segment, TutorialZoneType.MonsterRevealZone, context);
        BuildSegmentRoom(root.transform, "HideFromMonsterChamber", hidePos, segment, TutorialZoneType.LightSpotZone, context);
        BuildSegmentRoom(root.transform, "SprintRiskCorridor", sprintPos, segment * 0.8f, TutorialZoneType.SprintDemoZone, context);
        BuildSegmentRoom(root.transform, "TutorialExitGate", exitPos, segment * 0.8f, TutorialZoneType.TutorialExitZone, context);

        context.playerSpawnPoint = CreateMarker(root.transform, "PlayerSpawnPoint", startPos + Vector3.up * 1.8f);
        context.mainMazeConnector = CreateMarker(root.transform, "MainMazeConnector", connectorPos + Vector3.up * 1.8f);

        context.soundboardGate = CreateGate(root.transform, "SoundboardGate", useGatePos + forward * 1.4f);
        context.lightGate = CreateGate(root.transform, "LightGate", lightPos + forward * 1.2f);
        context.chaseGate = CreateGate(root.transform, "ChaseGate", revealPos + forward * 0.9f);
        context.tutorialExitGate = CreateGate(root.transform, "TutorialExitGate", exitPos + forward * 1.2f);
        context.corruptionDemoTrigger = CreateTrigger(root.transform, "CorruptionDemoTrigger", corruptionPos, new Vector3(corridorWidth * 0.75f, 2.5f, 2.5f), TutorialZoneType.CorruptionDemoZone, context);
        context.sprintRiskTrigger = CreateTrigger(root.transform, "SprintRiskTrigger", sprintPos, new Vector3(corridorWidth * 0.75f, 2.5f, 3f), TutorialZoneType.SprintDemoZone, context);

        context.soundboardPickup = CreateSoundboardPickup(root.transform, soundboardPos + new Vector3(0f, 0.6f, 0f));
        context.firstLightSpot = CreateLightSpot(root.transform, "FirstLightSpot", lightPos + new Vector3(0f, 0.1f, 0f));
        context.hideLightSpot = CreateLightSpot(root.transform, "HideLightSpot", hidePos + new Vector3(0f, 0.1f, 0f));

        context.monsterRevealPoint = CreateMarker(root.transform, "MonsterRevealPoint", revealPos + forward * 1.5f + Vector3.up * 1f);
        context.monsterSpawnPoint = CreateMarker(root.transform, "MonsterSpawnPoint", revealPos + forward * 2.7f + Vector3.up * 0.5f);

        return context;
    }

    private void BuildSegmentRoom(Transform root, string name, Vector3 center, float depth, TutorialZoneType zoneType, TutorialLayoutContext context)
    {
        GameObject room = new GameObject(name);
        room.transform.SetParent(root);
        room.transform.position = center;
        generatedObjects.Add(room);
        context.generatedRooms.Add(room);
        context.generatedCells.Add(center);

        CreateCube(room.transform, "Floor", new Vector3(0f, -0.05f, 0f), new Vector3(corridorWidth, 0.1f, depth), new Color(0.08f, 0.08f, 0.08f));
        CreateCube(room.transform, "Ceiling", new Vector3(0f, wallHeight, 0f), new Vector3(corridorWidth, 0.1f, depth), new Color(0.05f, 0.05f, 0.05f));
        CreateCube(room.transform, "WallL", new Vector3(-corridorWidth * 0.5f, wallHeight * 0.5f, 0f), new Vector3(0.15f, wallHeight, depth), new Color(0.12f, 0.12f, 0.12f));
        CreateCube(room.transform, "WallR", new Vector3(corridorWidth * 0.5f, wallHeight * 0.5f, 0f), new Vector3(0.15f, wallHeight, depth), new Color(0.12f, 0.12f, 0.12f));

        Light ambience = room.AddComponent<Light>();
        ambience.type = LightType.Point;
        ambience.range = depth * 1.3f;
        ambience.intensity = zoneType == TutorialZoneType.TutorialSafeZone ? 0.2f : 0.55f;
        ambience.color = zoneType == TutorialZoneType.LightSpotZone ? new Color(1f, 0.89f, 0.7f) : new Color(0.55f, 0.57f, 0.62f);

        TutorialZoneMarker marker = room.AddComponent<TutorialZoneMarker>();
        marker.zoneType = zoneType;
        marker.worldBounds = new Bounds(center + Vector3.up * (wallHeight * 0.5f), new Vector3(corridorWidth, wallHeight, depth));
        context.zoneMarkers.Add(marker);
    }

    private SoundboardPickup CreateSoundboardPickup(Transform root, Vector3 pos)
    {
        GameObject pickupObject;
        if (soundboardPickupPrefab != null)
        {
            pickupObject = Instantiate(soundboardPickupPrefab, pos, Quaternion.identity, root);
        }
        else
        {
            pickupObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pickupObject.transform.SetParent(root);
            pickupObject.transform.position = pos;
            pickupObject.transform.localScale = new Vector3(0.45f, 0.2f, 0.3f);
        }

        pickupObject.name = "TutorialSoundboardPickup";
        Collider pickupCollider = pickupObject.GetComponent<Collider>();
        if (pickupCollider == null)
        {
            pickupCollider = pickupObject.AddComponent<BoxCollider>();
        }
        pickupCollider.isTrigger = true;

        SoundboardPickup pickup = pickupObject.GetComponent<SoundboardPickup>();
        if (pickup == null)
        {
            pickup = pickupObject.AddComponent<SoundboardPickup>();
        }

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            PlaySound soundboard = playerObj.GetComponentInChildren<PlaySound>(true);
            if (soundboard != null)
            {
                pickup.soundboardRuntimeObject = soundboard.gameObject;
            }
        }

        generatedObjects.Add(pickupObject);
        return pickup;
    }

    private SafeSpaceZone CreateLightSpot(Transform root, string name, Vector3 pos)
    {
        GameObject lightObj;
        if (safeLightPrefab != null)
        {
            lightObj = Instantiate(safeLightPrefab, pos, Quaternion.identity, root);
        }
        else
        {
            lightObj = new GameObject(name);
            lightObj.transform.SetParent(root);
            lightObj.transform.position = pos;
            Light light = lightObj.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 4.5f;
            light.intensity = 1.8f;
            light.color = new Color(1f, 0.87f, 0.68f);
        }

        lightObj.name = name;
        SphereCollider trigger = lightObj.GetComponent<SphereCollider>();
        if (trigger == null)
        {
            trigger = lightObj.AddComponent<SphereCollider>();
        }
        trigger.isTrigger = true;
        trigger.radius = 3f;

        SafeSpaceZone safeZone = lightObj.GetComponent<SafeSpaceZone>();
        if (safeZone == null)
        {
            safeZone = lightObj.AddComponent<SafeSpaceZone>();
        }

        safeZone.activeDuration = 14f;
        safeZone.holdDuration = 0.6f;
        safeZone.canOnlyActivateOnce = false;

        generatedObjects.Add(lightObj);
        return safeZone;
    }

    private Transform CreateMarker(Transform root, string name, Vector3 position)
    {
        GameObject marker = new GameObject(name);
        marker.transform.SetParent(root);
        marker.transform.position = position;
        generatedObjects.Add(marker);
        return marker.transform;
    }

    private GameObject CreateGate(Transform root, string name, Vector3 position)
    {
        GameObject gate;
        if (gatePrefab != null)
        {
            gate = Instantiate(gatePrefab, position, Quaternion.identity, root);
        }
        else
        {
            gate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gate.transform.SetParent(root);
            gate.transform.position = position;
            gate.transform.localScale = new Vector3(corridorWidth * 0.9f, 2.7f, 0.2f);
            Renderer renderer = gate.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = new Color(0.32f, 0.08f, 0.08f);
            }
        }

        gate.name = name;
        generatedObjects.Add(gate);
        return gate;
    }

    private GameObject CreateTrigger(Transform root, string name, Vector3 center, Vector3 size, TutorialZoneType zoneType, TutorialLayoutContext context)
    {
        GameObject triggerObj = new GameObject(name);
        triggerObj.transform.SetParent(root);
        triggerObj.transform.position = center + Vector3.up * 1.2f;

        BoxCollider trigger = triggerObj.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.size = size;

        TutorialZoneMarker marker = triggerObj.AddComponent<TutorialZoneMarker>();
        marker.zoneType = zoneType;
        marker.worldBounds = new Bounds(triggerObj.transform.position, size);
        context.zoneMarkers.Add(marker);

        generatedObjects.Add(triggerObj);
        return triggerObj;
    }

    private GameObject CreateRoot(string name)
    {
        GameObject root = new GameObject(name);
        root.transform.SetParent(transform);
        generatedObjects.Add(root);
        return root;
    }

    private void CreateCube(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Color color)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.SetParent(parent);
        cube.transform.localPosition = localPosition;
        cube.transform.localScale = localScale;
        Renderer renderer = cube.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = color;
        }
    }

    private bool ValidateLayout(TutorialLayoutContext context, out List<string> errors)
    {
        errors = new List<string>();
        if (context == null)
        {
            errors.Add("Context is null.");
            return false;
        }

        if (context.playerSpawnPoint == null) errors.Add("Missing player spawn point.");
        if (context.soundboardPickup == null) errors.Add("Missing soundboard pickup.");
        if (context.soundboardGate == null) errors.Add("Missing soundboard gate.");
        if (context.lightGate == null) errors.Add("Missing light gate.");
        if (context.chaseGate == null) errors.Add("Missing chase gate.");
        if (context.firstLightSpot == null) errors.Add("Missing first light spot.");
        if (context.hideLightSpot == null) errors.Add("Missing hide light spot.");
        if (context.monsterRevealPoint == null) errors.Add("Missing monster reveal point.");
        if (context.monsterSpawnPoint == null) errors.Add("Missing monster spawn point.");
        if (context.sprintRiskTrigger == null) errors.Add("Missing sprint risk trigger.");
        if (context.tutorialExitGate == null) errors.Add("Missing tutorial exit gate.");
        if (context.mainMazeConnector == null) errors.Add("Missing main maze connector.");

        if (context.monsterRevealPoint != null && context.playerSpawnPoint != null)
        {
            float revealDistance = Vector3.Distance(context.monsterRevealPoint.position, context.playerSpawnPoint.position);
            if (revealDistance < corridorLength * 2f)
            {
                errors.Add("Monster reveal hall too short for readable reveal.");
            }
        }

        if (context.generatedCells.Count >= 2)
        {
            for (int i = 1; i < context.generatedCells.Count; i++)
            {
                float distance = Vector3.Distance(context.generatedCells[i - 1], context.generatedCells[i]);
                if (distance < 1f)
                {
                    errors.Add($"Segment reachability failed between index {i - 1} and {i} (distance too small).");
                    break;
                }
            }
        }

        if (context.IsValid())
        {
            Log("Layout validation passed.");
        }
        else
        {
            Log("Layout validation failed.");
        }

        return errors.Count == 0;
    }

    private void PlacePlayerAtSpawn(TutorialLayoutContext context)
    {
        if (context == null || context.playerSpawnPoint == null || player == null)
        {
            return;
        }

        player.position = context.playerSpawnPoint.position;
        player.rotation = Quaternion.LookRotation((context.soundboardPickup.transform.position - context.playerSpawnPoint.position).normalized);

        if (villainAI != null && context.monsterSpawnPoint != null)
        {
            villainAI.transform.position = context.monsterSpawnPoint.position;
            villainAI.enabled = true;
        }
    }

    private void CleanupGeneratedObjects()
    {
        for (int i = generatedObjects.Count - 1; i >= 0; i--)
        {
            if (generatedObjects[i] != null)
            {
                Destroy(generatedObjects[i]);
            }
        }
        generatedObjects.Clear();
    }

    private void Log(string message)
    {
        if (enableTutorialGenerationDebug)
        {
            Debug.Log("[GuidedIntroMazeGenerator] " + message);
        }
    }

    void OnDrawGizmosSelected()
    {
        if (!enableTutorialGenerationDebug || generatedContext == null)
        {
            return;
        }

        Gizmos.color = Color.cyan;
        for (int i = 0; i < generatedContext.generatedCells.Count; i++)
        {
            Vector3 p = generatedContext.generatedCells[i];
            Gizmos.DrawWireCube(p + Vector3.up * 0.5f, new Vector3(1.2f, 1f, 1.2f));
        }

        if (generatedContext.playerSpawnPoint != null && generatedContext.mainMazeConnector != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(generatedContext.playerSpawnPoint.position, generatedContext.mainMazeConnector.position);
        }

        if (generatedContext.monsterRevealPoint != null && generatedContext.monsterSpawnPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(generatedContext.monsterSpawnPoint.position, generatedContext.monsterRevealPoint.position);
        }
    }
}
