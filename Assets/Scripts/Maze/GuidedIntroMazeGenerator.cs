using System.Collections.Generic;
using UnityEngine;

public class GuidedIntroMazeGenerator : MonoBehaviour
{
    [Header("Build")]
    public bool generateOnStart = true;
    public bool enableTutorialGenerationDebug = true;

    [Header("Shared Foundation")]
    public MazeGenerationConfig sharedMazeConfig;

    [Header("Grid Alignment")]
    public float mazeCellSize = 2f;

    [Header("Prefabs")]
    public GameObject soundboardPickupPrefab;
    public GameObject safeLightPrefab;

    [Header("Runtime")]
    public IntroTapeController tutorialController;

    private readonly List<GameObject> generatedObjects = new List<GameObject>();
    private static int globalLayoutRequests = 0;
    private TutorialLayoutContext generatedContext;

    public TutorialLayoutContext GeneratedContext => generatedContext;

    void Start()
    {
        if (!generateOnStart) return;
        Debug.Log("[GuidedIntroMazeGenerator] Runtime tutorial layout bootstrap active.");
        GenerateLayout();
    }

    public TutorialLayoutContext GenerateLayout()
    {
        int requestId = ++globalLayoutRequests;
        Debug.Log($"[GuidedIntroMazeGenerator] GenerateLayout request={requestId}, frame={Time.frameCount}");
        ResolveReferences();
        MazeGenerator mazeGenerator = FindObjectOfType<MazeGenerator>();
        if (mazeGenerator == null)
        {
            Debug.LogError("[GuidedIntroMazeGenerator] Missing MazeGenerator. Cannot route tutorial layout generation.");
            return null;
        }

        MazeBlueprintData layout = TutorialMazeBlueprintFactory.CreateDefaultTutorialBlueprint();
        Debug.Log("Tutorial layout data created.");
        mazeGenerator.GenerateGuidedIntroMazeAndBuild(layout);

        generatedContext = BuildContextFromMazeGenerator(mazeGenerator);
        TutorialRuntimeRegistry.Instance?.LogRegistrationReport("GuidedIntroMazeGenerator.GenerateLayout");
        return generatedContext;
    }

    private void ResolveReferences()
    {
        if (tutorialController == null)
        {
            tutorialController = FindObjectOfType<IntroTapeController>();
        }

        if (sharedMazeConfig != null)
        {
            mazeCellSize = sharedMazeConfig.cellSize;
        }
    }

    private TutorialLayoutContext BuildContextFromMazeGenerator(MazeGenerator mazeGenerator)
    {
        TutorialLayoutContext context = new TutorialLayoutContext();
        TutorialRuntimeRegistry registry = TutorialRuntimeRegistry.Instance;

        if (registry != null)
        {
            registry.TryGet(TutorialRuntimeRole.SoundboardDoorGate, out context.soundboardGate);
            registry.TryGet(TutorialRuntimeRole.SoundboardUseDoor, out context.soundboardUseDoor);
            registry.TryGet(TutorialRuntimeRole.CorruptionDoor, out context.corruptionDoor);
            registry.TryGet(TutorialRuntimeRole.LightDoorGate, out context.lightGate);
            registry.TryGet(TutorialRuntimeRole.ChaseGate, out context.chaseGate);
            registry.TryGet(TutorialRuntimeRole.SprintDoor, out context.sprintDoor);
            registry.TryGet(TutorialRuntimeRole.TutorialExitGate, out context.tutorialExitGate);
        }

        GameObject playerSpawn = new GameObject("TutorialPlayerSpawnPoint");
        playerSpawn.transform.position = mazeGenerator.GetStartPosition();
        generatedObjects.Add(playerSpawn);
        context.playerSpawnPoint = playerSpawn.transform;

        PlaceTutorialPropsFromMazeCells(mazeGenerator, context);
        return context;
    }

    private void PlaceTutorialPropsFromMazeCells(MazeGenerator mazeGenerator, TutorialLayoutContext context)
    {
        Vector3 soundboardPos = GetStageCenter(mazeGenerator, "TutorialStage_SoundboardPickup");
        Vector3 corruptionPos = GetStageCenter(mazeGenerator, "TutorialStage_CorruptionDemo");
        Vector3 lightPos = GetStageCenter(mazeGenerator, "TutorialStage_LightSpot");
        Vector3 revealPos = GetStageCenter(mazeGenerator, "TutorialStage_MonsterReveal");
        Vector3 hidePos = GetStageCenter(mazeGenerator, "TutorialStage_HideFromMonster");
        Vector3 sprintPos = GetStageCenter(mazeGenerator, "TutorialStage_SprintRisk");
        Vector3 connectorPos = GetStageCenter(mazeGenerator, "TutorialStage_MainMazeConnector");

        context.soundboardPickup = CreateSoundboardPickup(transform, soundboardPos + Vector3.up * 0.6f);
        context.firstLightSpot = CreateLightSpot(transform, "FirstLightSpot", lightPos + Vector3.up * 0.1f);
        context.hideLightSpot = CreateLightSpot(transform, "HideLightSpot", hidePos + Vector3.up * 0.1f);
        context.corruptionDemoTrigger = CreateTrigger(transform, "CorruptionDemoTrigger", corruptionPos, new Vector3(mazeCellSize * 1.5f, 2.5f, mazeCellSize * 2f), TutorialZoneType.CorruptionDemoZone, context);
        context.sprintRiskTrigger = CreateTrigger(transform, "SprintRiskTrigger", sprintPos, new Vector3(mazeCellSize * 1.5f, 2.5f, mazeCellSize * 2f), TutorialZoneType.SprintDemoZone, context);
        context.monsterRevealPoint = CreateMarker(transform, "MonsterRevealPoint", revealPos + Vector3.up * 1f);
        context.monsterSpawnPoint = CreateMarker(transform, "MonsterSpawnPoint", revealPos + Vector3.forward * mazeCellSize * 2f + Vector3.up * 0.5f);
        context.mainMazeConnector = CreateMarker(transform, "MainMazeConnector", connectorPos + Vector3.up * 1.8f);

        TutorialRuntimeRegistry registry = TutorialRuntimeRegistry.Instance;
        if (registry != null)
        {
            registry.Register(TutorialRuntimeRole.SoundboardPickup, context.soundboardPickup);
            registry.Register(TutorialRuntimeRole.TutorialLightSpot, context.firstLightSpot);
            registry.Register(TutorialRuntimeRole.MonsterSpawnPoint, context.monsterSpawnPoint);
            registry.Register(TutorialRuntimeRole.MonsterRevealPoint, context.monsterRevealPoint);
            registry.Register(TutorialRuntimeRole.MainMazeConnector, context.mainMazeConnector);
        }
    }

    private Vector3 GetStageCenter(MazeGenerator mazeGenerator, string stageTag)
    {
        List<Vector2Int> stageCells = mazeGenerator.GetCellsByTutorialStage(stageTag);
        if (stageCells.Count == 0)
        {
            return mazeGenerator.GetStartPosition();
        }

        Vector3 sum = Vector3.zero;
        for (int i = 0; i < stageCells.Count; i++)
        {
            sum += mazeGenerator.GetCellCenter(stageCells[i]);
        }

        Vector3 average = sum / stageCells.Count;
        average.y = 0f;
        return average;
    }

    private SoundboardPickup CreateSoundboardPickup(Transform root, Vector3 pos)
    {
        GameObject pickupObject = soundboardPickupPrefab != null
            ? Instantiate(soundboardPickupPrefab, pos, Quaternion.identity, root)
            : GameObject.CreatePrimitive(PrimitiveType.Cube);

        if (soundboardPickupPrefab == null)
        {
            pickupObject.transform.SetParent(root);
            pickupObject.transform.position = pos;
            pickupObject.transform.localScale = new Vector3(0.45f, 0.2f, 0.3f);
        }

        pickupObject.name = "TutorialSoundboardPickup";
        Collider pickupCollider = pickupObject.GetComponent<Collider>() ?? pickupObject.AddComponent<BoxCollider>();
        pickupCollider.isTrigger = true;

        SoundboardPickup pickup = pickupObject.GetComponent<SoundboardPickup>() ?? pickupObject.AddComponent<SoundboardPickup>();
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
        }

        lightObj.name = name;
        SphereCollider trigger = lightObj.GetComponent<SphereCollider>() ?? lightObj.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = 3f;

        SafeSpaceZone safeZone = lightObj.GetComponent<SafeSpaceZone>() ?? lightObj.AddComponent<SafeSpaceZone>();
        safeZone.activeDuration = 14f;
        safeZone.holdDuration = 0.6f;
        safeZone.canOnlyActivateOnce = false;

        generatedObjects.Add(lightObj);
        return safeZone;
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

    private Transform CreateMarker(Transform root, string name, Vector3 position)
    {
        GameObject marker = new GameObject(name);
        marker.transform.SetParent(root);
        marker.transform.position = position;
        generatedObjects.Add(marker);
        return marker.transform;
    }
}
