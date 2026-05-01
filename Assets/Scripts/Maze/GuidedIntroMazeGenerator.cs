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

    [Header("Shared Foundation")]
    public MazeGenerationConfig sharedMazeConfig;

    [Header("Grid Alignment")]
    public float mazeCellSize = 2f;
    public float mazeWallHeight = 3f;
    public float tutorialFloorY = 0f;
    public Vector2Int layoutOrigin = Vector2Int.zero;

    [Header("Prefabs")]
    public GameObject soundboardPickupPrefab;
    public GameObject safeLightPrefab;
    public GameObject gatePrefab;

    [Header("Material Consistency")]
    public Material tutorialFloorMaterial;
    public Material tutorialWallMaterial;
    public Material tutorialCeilingMaterial;
    public Material tutorialDoorMaterial;
    public bool allowMaterialFallback = true;

    [Header("Stage Visual Composition")]
    public bool enableStageComposition = true;
    public float dimLightIntensity = 0.45f;
    public float focalLightIntensity = 1.4f;

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
        ValidateFinalTutorialPresentation(generatedContext);
        ValidateIntegrationChecklist(generatedContext);
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

        ApplySharedGenerationConfig();
        ResolveMaterialReferences();
    }

    private void ApplySharedGenerationConfig()
    {
        if (sharedMazeConfig == null)
        {
            MazeGenerator maze = FindObjectOfType<MazeGenerator>();
            if (maze != null)
            {
                mazeCellSize = maze.cellSize;
                mazeWallHeight = maze.wallHeight;
            }
            Debug.Log("[GuidedIntroMazeGenerator] Intro generator using maze cell size: " + mazeCellSize);
            Debug.Log("[GuidedIntroMazeGenerator] Intro generator using wall height: " + mazeWallHeight);
            return;
        }

        mazeCellSize = sharedMazeConfig.cellSize;
        mazeWallHeight = sharedMazeConfig.wallHeight;
        tutorialFloorY = sharedMazeConfig.floorY;

        if (tutorialFloorMaterial == null) tutorialFloorMaterial = sharedMazeConfig.floorMaterial;
        if (tutorialWallMaterial == null) tutorialWallMaterial = sharedMazeConfig.wallMaterial;
        if (tutorialCeilingMaterial == null) tutorialCeilingMaterial = sharedMazeConfig.ceilingMaterial;
        if (tutorialDoorMaterial == null) tutorialDoorMaterial = sharedMazeConfig.doorMaterial;

        Debug.Log("[GuidedIntroMazeGenerator] Intro generator using maze cell size: " + mazeCellSize);
        Debug.Log("[GuidedIntroMazeGenerator] Intro generator using wall height: " + mazeWallHeight);
        Debug.Log("[GuidedIntroMazeGenerator] Intro generator using shared MazeGenerationConfig.");
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
        Debug.Log("[GuidedIntroMazeGenerator] Intro layout generated through shared MazeBuilder.");

        Vector3 basePos = transform.position;
        Vector3 forward = transform.forward.sqrMagnitude <= 0.001f ? Vector3.forward : transform.forward.normalized;
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        wallHeight = mazeWallHeight;

        var segments = new (string name, TutorialZoneType zone, int startX, int endX, int startZ, int endZ)[]
        {
            ("TutorialStartCell", TutorialZoneType.TutorialSafeZone, 0, 2, 0, 2),
            ("TransitionA", TutorialZoneType.EncounterDisabledZone, 3, 3, 1, 1),
            ("SoundboardPickupRoom", TutorialZoneType.EncounterDisabledZone, 4, 6, 0, 3),
            ("SoundboardUseGate", TutorialZoneType.SanityDrainZone, 7, 7, 1, 2),
            ("CorruptionDemoCorridor", TutorialZoneType.CorruptionDemoZone, 8, 12, 1, 1),
            ("LightSpotRoom", TutorialZoneType.LightSpotZone, 13, 16, 0, 3),
            ("MonsterRevealHall", TutorialZoneType.MonsterRevealZone, 17, 24, 1, 1),
            ("HideFromMonsterChamber", TutorialZoneType.LightSpotZone, 25, 28, 0, 4),
            ("SprintRiskCorridor", TutorialZoneType.SprintDemoZone, 29, 34, 2, 2),
            ("TutorialExitGate", TutorialZoneType.TutorialExitZone, 35, 36, 1, 3),
        };

        GameObject root = CreateRoot("GeneratedTutorialLayout");
        var walkableCells = new HashSet<Vector2Int>();
        foreach (var seg in segments)
        {
            BuildGridSegment(root.transform, seg.name, seg.zone, seg.startX, seg.endX, seg.startZ, seg.endZ, basePos, right, forward, walkableCells, context);
        }

        BuildCellGeometry(root.transform, walkableCells, basePos, right, forward);
        if (enableStageComposition)
        {
            ComposeStageVisuals(root.transform, basePos, right, forward);
        }

        Vector3 startPos = GridToWorld(new Vector2Int(1,1), basePos, right, forward);
        Vector3 soundboardPos = GridToWorld(new Vector2Int(5,2), basePos, right, forward);
        Vector3 corruptionPos = GridToWorld(new Vector2Int(10,1), basePos, right, forward);
        Vector3 lightPos = GridToWorld(new Vector2Int(14,2), basePos, right, forward);
        Vector3 revealPos = GridToWorld(new Vector2Int(20,1), basePos, right, forward);
        Vector3 hidePos = GridToWorld(new Vector2Int(26,2), basePos, right, forward);
        Vector3 sprintPos = GridToWorld(new Vector2Int(31,2), basePos, right, forward);
        Vector3 exitPos = GridToWorld(new Vector2Int(35,2), basePos, right, forward);
        Vector3 connectorPos = GridToWorld(new Vector2Int(37,2), basePos, right, forward);

        context.playerSpawnPoint = CreateMarker(root.transform, "PlayerSpawnPoint", startPos + Vector3.up * 1.8f);
        context.mainMazeConnector = CreateMarker(root.transform, "MainMazeConnector", connectorPos + Vector3.up * 1.8f);
        BuildTutorialMainMazeConnector(root.transform, basePos, right, forward);

        context.soundboardGate = CreateDoorBetweenCells(root.transform, "Door_SoundboardPickupExit", new Vector2Int(6, 2), new Vector2Int(7, 2));
        context.soundboardUseDoor = CreateDoorBetweenCells(root.transform, "Door_SoundboardUseExit", new Vector2Int(7, 1), new Vector2Int(8, 1));
        context.corruptionDoor = CreateDoorBetweenCells(root.transform, "Door_CorruptionExit", new Vector2Int(12, 1), new Vector2Int(13, 1));
        context.lightGate = CreateDoorBetweenCells(root.transform, "Door_LightSpotExit", new Vector2Int(16, 1), new Vector2Int(17, 1));
        context.chaseGate = CreateDoorBetweenCells(root.transform, "Door_HideChamberExit", new Vector2Int(28, 2), new Vector2Int(29, 2));
        context.sprintDoor = CreateDoorBetweenCells(root.transform, "Door_SprintExit", new Vector2Int(34, 2), new Vector2Int(35, 2));
        context.tutorialExitGate = CreateDoorBetweenCells(root.transform, "Door_TutorialFinalExit", new Vector2Int(36, 2), new Vector2Int(37, 2));
        context.corruptionDemoTrigger = CreateTrigger(root.transform, "CorruptionDemoTrigger", corruptionPos, new Vector3(mazeCellSize * 1.5f, 2.5f, mazeCellSize * 2f), TutorialZoneType.CorruptionDemoZone, context);
        context.sprintRiskTrigger = CreateTrigger(root.transform, "SprintRiskTrigger", sprintPos, new Vector3(mazeCellSize * 1.5f, 2.5f, mazeCellSize * 2f), TutorialZoneType.SprintDemoZone, context);

        context.soundboardPickup = CreateSoundboardPickup(root.transform, soundboardPos + new Vector3(0f, 0.6f, 0f));
        context.firstLightSpot = CreateLightSpot(root.transform, "FirstLightSpot", lightPos + new Vector3(0f, 0.1f, 0f));
        context.hideLightSpot = CreateLightSpot(root.transform, "HideLightSpot", hidePos + new Vector3(0f, 0.1f, 0f));

        context.monsterRevealPoint = CreateMarker(root.transform, "MonsterRevealPoint", revealPos + forward * mazeCellSize * 2f + Vector3.up * 1f);
        context.monsterSpawnPoint = CreateMarker(root.transform, "MonsterSpawnPoint", revealPos + forward * mazeCellSize * 3.5f + Vector3.up * 0.5f);

        return context;
    }

    private void ComposeStageVisuals(Transform root, Vector3 basePos, Vector3 right, Vector3 forward)
    {
        CreateStageLight(root, "Stage1_DimLight", GridToWorld(new Vector2Int(1, 1), basePos, right, forward) + Vector3.up * (mazeWallHeight - 0.4f), new Color(0.52f, 0.56f, 0.65f), dimLightIntensity, 6f);

        Vector3 soundboardPedestalPos = GridToWorld(new Vector2Int(5, 2), basePos, right, forward);
        CreatePropCube(root, "Stage2_SoundboardPedestal", soundboardPedestalPos + new Vector3(0f, 0.45f, 0f), new Vector3(0.8f, 0.9f, 0.8f));
        CreateStageLight(root, "Stage2_FocalLight", soundboardPedestalPos + Vector3.up * (mazeWallHeight - 0.35f), new Color(1f, 0.9f, 0.74f), focalLightIntensity, 5.2f);

        CreateStageLight(root, "Stage4_FlickerA", GridToWorld(new Vector2Int(9, 1), basePos, right, forward) + Vector3.up * (mazeWallHeight - 0.35f), new Color(0.75f, 0.76f, 0.86f), 0.65f, 4.5f);
        CreateStageLight(root, "Stage4_FlickerB", GridToWorld(new Vector2Int(11, 1), basePos, right, forward) + Vector3.up * (mazeWallHeight - 0.35f), new Color(0.67f, 0.7f, 0.8f), 0.55f, 4.5f);

        CreateStageLight(root, "Stage5_SafeLight", GridToWorld(new Vector2Int(14, 2), basePos, right, forward) + Vector3.up * (mazeWallHeight - 0.45f), new Color(1f, 0.84f, 0.62f), 1.85f, 8f);

        CreateStageLight(root, "Stage6_EntryDim", GridToWorld(new Vector2Int(17, 1), basePos, right, forward) + Vector3.up * (mazeWallHeight - 0.35f), new Color(0.5f, 0.53f, 0.62f), 0.45f, 4f);
        CreateStageLight(root, "Stage6_FarSilhouette", GridToWorld(new Vector2Int(24, 1), basePos, right, forward) + Vector3.up * (mazeWallHeight - 0.35f), new Color(0.76f, 0.74f, 0.79f), 0.85f, 5.5f);

        CreateStageLight(root, "Stage7_HideSafeLight", GridToWorld(new Vector2Int(26, 3), basePos, right, forward) + Vector3.up * (mazeWallHeight - 0.45f), new Color(1f, 0.85f, 0.66f), 1.65f, 7f);

        CreateStageLight(root, "Stage8_ExitPull", GridToWorld(new Vector2Int(33, 2), basePos, right, forward) + Vector3.up * (mazeWallHeight - 0.4f), new Color(0.7f, 0.71f, 0.75f), 0.9f, 5f);

        CreateStageLight(root, "Stage9_ExitThreshold", GridToWorld(new Vector2Int(35, 2), basePos, right, forward) + Vector3.up * (mazeWallHeight - 0.35f), new Color(0.87f, 0.84f, 0.79f), 1.15f, 5.5f);
    }

    private void CreateStageLight(Transform root, string name, Vector3 pos, Color color, float intensity, float range)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(root);
        go.transform.position = pos;
        Light l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = color;
        l.intensity = intensity;
        l.range = range;
        generatedObjects.Add(go);
    }

    private void CreatePropCube(Transform root, string name, Vector3 pos, Vector3 scale)
    {
        CreateCube(root, name, pos, scale, new Color(0.18f, 0.18f, 0.18f), tutorialWallMaterial);
    }

    private void BuildGridSegment(Transform root, string name, TutorialZoneType zoneType, int startX, int endX, int startZ, int endZ,
        Vector3 basePos, Vector3 right, Vector3 forward, HashSet<Vector2Int> walkableCells, TutorialLayoutContext context)
    {
        GameObject segment = new GameObject(name);
        segment.transform.SetParent(root);
        generatedObjects.Add(segment);
        context.generatedRooms.Add(segment);

        for (int x = startX; x <= endX; x++)
        {
            for (int z = startZ; z <= endZ; z++)
            {
                Vector2Int cell = new Vector2Int(layoutOrigin.x + x, layoutOrigin.y + z);
                walkableCells.Add(cell);
                context.generatedCells.Add(GridToWorld(cell, basePos, right, forward));
            }
        }

        Vector3 center = GridRectCenter(startX, endX, startZ, endZ, basePos, right, forward);
        Vector3 size = new Vector3((endX - startX + 1) * mazeCellSize, mazeWallHeight, (endZ - startZ + 1) * mazeCellSize);
        TutorialZoneMarker marker = segment.AddComponent<TutorialZoneMarker>();
        marker.zoneType = zoneType;
        marker.worldBounds = new Bounds(center + Vector3.up * (mazeWallHeight * 0.5f), size);
        context.zoneMarkers.Add(marker);
    }

    private void BuildCellGeometry(Transform root, HashSet<Vector2Int> walkableCells, Vector3 basePos, Vector3 right, Vector3 forward)
    {
        HashSet<string> builtWalls = new HashSet<string>();
        foreach (var cell in walkableCells)
        {
            Vector3 center = GridToWorld(cell, basePos, right, forward);
            CreateCube(root, $"Floor_{cell.x}_{cell.y}", center + new Vector3(0f, -0.05f, 0f), new Vector3(mazeCellSize, 0.1f, mazeCellSize), new Color(0.08f, 0.08f, 0.08f), tutorialFloorMaterial);
            CreateCube(root, $"Ceiling_{cell.x}_{cell.y}", center + new Vector3(0f, mazeWallHeight, 0f), new Vector3(mazeCellSize, 0.1f, mazeCellSize), new Color(0.05f, 0.05f, 0.05f), tutorialCeilingMaterial);
            BuildBoundaryWallIfNeeded(root, walkableCells, builtWalls, cell, Vector2Int.up, basePos, right, forward);
            BuildBoundaryWallIfNeeded(root, walkableCells, builtWalls, cell, Vector2Int.right, basePos, right, forward);
            BuildBoundaryWallIfNeeded(root, walkableCells, builtWalls, cell, Vector2Int.down, basePos, right, forward);
            BuildBoundaryWallIfNeeded(root, walkableCells, builtWalls, cell, Vector2Int.left, basePos, right, forward);
        }
    }

    private void BuildBoundaryWallIfNeeded(Transform root, HashSet<Vector2Int> walkableCells, HashSet<string> builtWalls, Vector2Int cell, Vector2Int dir,
        Vector3 basePos, Vector3 right, Vector3 forward)
    {
        Vector2Int n = cell + dir;
        if (walkableCells.Contains(n)) return;
        string key = cell.x < n.x || (cell.x == n.x && cell.y <= n.y) ? $"{cell.x}_{cell.y}_{n.x}_{n.y}" : $"{n.x}_{n.y}_{cell.x}_{cell.y}";
        if (!builtWalls.Add(key)) return;
        Vector3 center = GridToBoundaryWorld(cell, dir, basePos, right, forward);
        bool horizontal = dir == Vector2Int.right || dir == Vector2Int.left;
        Vector3 scale = horizontal ? new Vector3(0.15f, mazeWallHeight, mazeCellSize) : new Vector3(mazeCellSize, mazeWallHeight, 0.15f);
        CreateCube(root, $"Wall_{cell.x}_{cell.y}_{dir.x}_{dir.y}", center + Vector3.up * (mazeWallHeight * 0.5f), scale, new Color(0.12f, 0.12f, 0.12f), tutorialWallMaterial);
    }

    private Vector3 GridToWorld(Vector2Int cell, Vector3 basePos, Vector3 right, Vector3 forward)
    {
        return basePos + right * (cell.x * mazeCellSize) + forward * (cell.y * mazeCellSize) + new Vector3(0f, tutorialFloorY, 0f);
    }

    private Vector3 GridToBoundaryWorld(Vector2Int cell, Vector2Int dir, Vector3 basePos, Vector3 right, Vector3 forward)
    {
        return GridToWorld(cell, basePos, right, forward) + (right * dir.x + forward * dir.y) * (mazeCellSize * 0.5f);
    }

    private Vector3 GridRectCenter(int startX, int endX, int startZ, int endZ, Vector3 basePos, Vector3 right, Vector3 forward)
    {
        float cx = (startX + endX) * 0.5f;
        float cz = (startZ + endZ) * 0.5f;
        return GridToWorld(new Vector2Int(layoutOrigin.x, layoutOrigin.y), basePos, right, forward) + right * (cx * mazeCellSize) + forward * (cz * mazeCellSize);
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

    private GameObject CreateDoorBetweenCells(Transform root, string doorName, Vector2Int cellA, Vector2Int cellB)
    {
        Vector3 boundary = MazeGridUtility.BoundaryCenterBetweenCells(cellA, cellB, mazeCellSize, tutorialFloorY);
        Quaternion rot = MazeGridUtility.DoorRotationForBoundary(cellA, cellB);

        Vector3 dir = (new Vector3(cellB.x - cellA.x, 0f, cellB.y - cellA.y)).normalized;
        string boundaryLabel = Mathf.Abs(dir.x) > Mathf.Abs(dir.z) ? "East/West" : "North/South";

        GameObject door = CreateTutorialDoor(root, doorName, boundary, rot * Vector3.forward);

        Debug.Log($"[GuidedIntroMazeGenerator] Created tutorial door between cell ({cellA.x},{cellA.y}) and ({cellB.x},{cellB.y}).");
        Debug.Log("[GuidedIntroMazeGenerator] Door boundary: " + boundaryLabel + ".");
        Debug.Log("[GuidedIntroMazeGenerator] Door position: " + boundary);
        Debug.Log("[GuidedIntroMazeGenerator] Door rotation: " + rot.eulerAngles);

        ValidateDoorBlocksPassage(door, cellA, cellB);
        return door;
    }

    private void ValidateDoorBlocksPassage(GameObject door, Vector2Int cellA, Vector2Int cellB)
    {
        if (door == null)
        {
            Debug.LogError("[GuidedIntroMazeGenerator] Door validation failed: missing door instance.");
            return;
        }

        BoxCollider triggerCollider = door.GetComponent<BoxCollider>();
        if (triggerCollider == null)
        {
            Debug.LogError("[GuidedIntroMazeGenerator] Door validation failed: missing blocking trigger collider.");
            return;
        }

        if (triggerCollider.size.x <= 0.01f || triggerCollider.size.y <= 0.01f)
        {
            Debug.LogError("[GuidedIntroMazeGenerator] Door validation failed: invalid collider dimensions.");
            return;
        }

        float centerOffset = Vector3.Distance(door.transform.position, MazeGridUtility.BoundaryCenterBetweenCells(cellA, cellB, mazeCellSize, tutorialFloorY));
        if (centerOffset > mazeCellSize * 0.35f)
        {
            Debug.LogError("[GuidedIntroMazeGenerator] Door validation failed: not centered on shared wall boundary.");
            return;
        }

        Debug.Log("[GuidedIntroMazeGenerator] Door validation passed: blocks passage.");
    }

    private GameObject CreateTutorialDoor(Transform root, string name, Vector3 position, Vector3 facingDirection)
    {
        GameObject doorRoot = new GameObject(name);
        doorRoot.transform.SetParent(root);
        doorRoot.transform.position = position;
        doorRoot.transform.rotation = Quaternion.LookRotation(facingDirection);

        float doorWidth = sharedMazeConfig != null ? sharedMazeConfig.doorWidth : 3.95f;
        float doorThickness = sharedMazeConfig != null ? sharedMazeConfig.doorThickness : 0.1f;
        float fullDoorHeight = sharedMazeConfig != null ? sharedMazeConfig.doorHeight : 2.7f;

        GameObject leftDoor = new GameObject("Door_Left");
        leftDoor.transform.SetParent(doorRoot.transform);
        leftDoor.transform.localPosition = new Vector3(-doorWidth / 2f, fullDoorHeight / 2f, 0f);
        GameObject leftVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        leftVisual.name = "LeftDoor_Visual";
        leftVisual.transform.SetParent(leftDoor.transform);
        leftVisual.transform.localScale = new Vector3(doorWidth / 2f, fullDoorHeight, doorThickness);
        leftVisual.transform.localPosition = new Vector3(doorWidth / 4f, 0f, -doorThickness / 2f);
        if (tutorialDoorMaterial != null) leftVisual.GetComponent<Renderer>().material = tutorialDoorMaterial;
        DestroyImmediate(leftVisual.GetComponent<BoxCollider>());

        GameObject rightDoor = new GameObject("Door_Right");
        rightDoor.transform.SetParent(doorRoot.transform);
        rightDoor.transform.localPosition = new Vector3(doorWidth / 2f, fullDoorHeight / 2f, 0f);
        GameObject rightVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rightVisual.name = "RightDoor_Visual";
        rightVisual.transform.SetParent(rightDoor.transform);
        rightVisual.transform.localScale = new Vector3(doorWidth / 2f, fullDoorHeight, doorThickness);
        rightVisual.transform.localPosition = new Vector3(-doorWidth / 4f, 0f, -doorThickness / 2f);
        if (tutorialDoorMaterial != null) rightVisual.GetComponent<Renderer>().material = tutorialDoorMaterial;
        DestroyImmediate(rightVisual.GetComponent<BoxCollider>());

        BoxCollider triggerCollider = doorRoot.AddComponent<BoxCollider>();
        triggerCollider.isTrigger = true;
        triggerCollider.size = new Vector3(doorWidth, fullDoorHeight, 4f);
        triggerCollider.center = new Vector3(0f, fullDoorHeight / 2f, -2f);

        DoorTrigger trigger = doorRoot.AddComponent<DoorTrigger>();
        trigger.doorWidth = doorWidth;
        trigger.doorHeight = fullDoorHeight;
        trigger.facingDirection = facingDirection;
        trigger.SetLockedState(true);

        TutorialStageDoor stageDoor = doorRoot.AddComponent<TutorialStageDoor>();
        stageDoor.doorId = name;
        stageDoor.doorTrigger = trigger;
        stageDoor.startsLocked = true;

        generatedObjects.Add(doorRoot);
        return doorRoot;
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
                if (tutorialDoorMaterial != null)
                {
                    renderer.material = tutorialDoorMaterial;
                }
                else
                {
                    renderer.material.color = new Color(0.32f, 0.08f, 0.08f);
                }
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

    private void CreateCube(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Color color, Material preferredMaterial = null)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.SetParent(parent);
        cube.transform.localPosition = localPosition;
        cube.transform.localScale = localScale;
        Renderer renderer = cube.GetComponent<Renderer>();
        if (renderer != null)
        {
            if (preferredMaterial != null)
            {
                renderer.material = preferredMaterial;
            }
            else
            {
                renderer.material.color = color;
            }
        }
    }


    private void ResolveMaterialReferences()
    {
        if (tutorialFloorMaterial != null && tutorialWallMaterial != null && tutorialCeilingMaterial != null)
        {
            return;
        }

        MazeGenerator mazeGenerator = FindObjectOfType<MazeGenerator>();
        if (mazeGenerator != null)
        {
            if (tutorialFloorMaterial == null)
            {
                tutorialFloorMaterial = mazeGenerator.startRoomFloorMaterial;
            }

            if (tutorialWallMaterial == null)
            {
                tutorialWallMaterial = mazeGenerator.startRoomWallMaterial;
            }

            if (tutorialCeilingMaterial == null)
            {
                tutorialCeilingMaterial = mazeGenerator.startRoomWallMaterial;
            }
        }

        ValidateMaterialAssignments();
    }

    private void ValidateMaterialAssignments()
    {
        if (tutorialWallMaterial == null)
            Debug.LogWarning("[GuidedIntroMazeGenerator] Missing wall material reference for tutorial. Fallback color path will be used.");
        if (tutorialFloorMaterial == null)
            Debug.LogWarning("[GuidedIntroMazeGenerator] Missing floor material reference for tutorial. Fallback color path will be used.");
        if (tutorialCeilingMaterial == null)
            Debug.LogWarning("[GuidedIntroMazeGenerator] Missing ceiling material reference for tutorial. Fallback color path will be used.");
        if (tutorialDoorMaterial == null)
            Debug.LogWarning("[GuidedIntroMazeGenerator] Missing door material/prefab reference for tutorial gate visuals.");

        if (!allowMaterialFallback)
        {
            if (tutorialWallMaterial == null || tutorialFloorMaterial == null || tutorialCeilingMaterial == null)
            {
                Debug.LogWarning("[GuidedIntroMazeGenerator] allowMaterialFallback is false but one or more materials are missing.");
            }
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

        ValidateGridAndHeight(context, errors);

        if (errors.Count == 0)
        {
            Debug.Log("[GuidedIntroMazeGenerator] TutorialLayout validation passed: all rooms aligned to grid.");
        }
        else
        {
            Log("Layout validation failed.");
        }

        return errors.Count == 0;
    }


    private void ValidateGridAndHeight(TutorialLayoutContext context, List<string> errors)
    {
        float eps = 0.05f;
        for (int i = 0; i < context.generatedCells.Count; i++)
        {
            Vector3 c = context.generatedCells[i];
            if (Mathf.Abs(c.y - tutorialFloorY) > eps)
                errors.Add($"TutorialLayout error: generated cell floor Y={c.y:F2}, expected Y={tutorialFloorY:F2}.");

            float gx = (c.x - transform.position.x) / mazeCellSize;
            float gz = (c.z - transform.position.z) / mazeCellSize;
            if (Mathf.Abs(gx - Mathf.Round(gx)) > 0.05f || Mathf.Abs(gz - Mathf.Round(gz)) > 0.05f)
                errors.Add($"TutorialLayout error: cell {i} not aligned to grid.");
        }

        if (context.soundboardGate != null && !IsOnGridBoundary(context.soundboardGate.transform.position))
            errors.Add("TutorialLayout error: SoundboardUseDoor not aligned to grid boundary.");
        if (context.lightGate != null && !IsOnGridBoundary(context.lightGate.transform.position))
            errors.Add("TutorialLayout error: LightGate not aligned to grid boundary.");
        if (context.tutorialExitGate != null && !IsOnGridBoundary(context.tutorialExitGate.transform.position))
            errors.Add("TutorialLayout error: TutorialExitGate not aligned to grid boundary.");

        HashSet<Vector2Int> cells = new HashSet<Vector2Int>();
        foreach (var w in context.generatedCells)
            cells.Add(new Vector2Int(Mathf.RoundToInt((w.x - transform.position.x) / mazeCellSize), Mathf.RoundToInt((w.z - transform.position.z) / mazeCellSize)));
        if (cells.Count > 0)
        {
            Vector2Int start = default;
            foreach (var c in cells) { start = c; break; }
            Queue<Vector2Int> q = new Queue<Vector2Int>();
            HashSet<Vector2Int> seen = new HashSet<Vector2Int>();
            q.Enqueue(start); seen.Add(start);
            Vector2Int[] dirs = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
            while (q.Count > 0)
            {
                var cur = q.Dequeue();
                foreach (var d in dirs)
                {
                    var n = cur + d;
                    if (cells.Contains(n) && seen.Add(n)) q.Enqueue(n);
                }
            }
            if (seen.Count != cells.Count)
                errors.Add("TutorialLayout error: tutorial segments are not fully connected.");
        }
    }

    private bool IsOnGridBoundary(Vector3 position)
    {
        float gx = (position.x - transform.position.x) / mazeCellSize;
        float gz = (position.z - transform.position.z) / mazeCellSize;
        bool halfX = Mathf.Abs(gx * 2f - Mathf.Round(gx * 2f)) < 0.05f;
        bool halfZ = Mathf.Abs(gz * 2f - Mathf.Round(gz * 2f)) < 0.05f;
        bool wholeX = Mathf.Abs(gx - Mathf.Round(gx)) < 0.05f;
        bool wholeZ = Mathf.Abs(gz - Mathf.Round(gz)) < 0.05f;
        return (halfX && wholeZ) || (halfZ && wholeX);
    }

    private void BuildTutorialMainMazeConnector(Transform root, Vector3 basePos, Vector3 right, Vector3 forward)
    {
        Vector3 connectorCell = GridToWorld(new Vector2Int(37, 2), basePos, right, forward);
        Vector3 beyondConnector = GridToWorld(new Vector2Int(38, 2), basePos, right, forward);

        CreateCube(root, "ConnectorFloor", connectorCell + new Vector3(0f, -0.05f, 0f), new Vector3(mazeCellSize * 2f, 0.1f, mazeCellSize), new Color(0.08f, 0.08f, 0.08f), tutorialFloorMaterial);
        CreateCube(root, "ConnectorCeiling", connectorCell + new Vector3(0f, mazeWallHeight, 0f), new Vector3(mazeCellSize * 2f, 0.1f, mazeCellSize), new Color(0.05f, 0.05f, 0.05f), tutorialCeilingMaterial);

        MazeGenerator mazeGenerator = FindObjectOfType<MazeGenerator>();
        if (mazeGenerator != null)
        {
            Vector3 mazeStart = mazeGenerator.GetStartPosition();
            if (Mathf.Abs(mazeStart.y - tutorialFloorY) > 0.15f)
            {
                Debug.LogError($"[GuidedIntroMazeGenerator] Connector error: tutorial exit Y ({tutorialFloorY:F2}) does not match maze floor Y ({mazeStart.y:F2}).");
            }
            else
            {
                Debug.Log("[GuidedIntroMazeGenerator] Tutorial connector aligned with main maze entrance.");
            }

            float dist = Vector3.Distance(new Vector3(mazeStart.x, tutorialFloorY, mazeStart.z), beyondConnector);
            if (dist > mazeCellSize * 6f)
            {
                Debug.LogError("[GuidedIntroMazeGenerator] Connector error: no valid main maze cell adjacent to tutorial exit.");
            }
        }
    }

    private void ValidateFinalTutorialPresentation(TutorialLayoutContext context)
    {
        if (context == null)
        {
            Debug.LogError("[GuidedIntroMazeGenerator] Final validation failed: null context.");
            return;
        }

        int stageDoorCount = 0;
        TutorialStageDoor[] stageDoors = FindObjectsOfType<TutorialStageDoor>();
        stageDoorCount = stageDoors.Length;

        int redGateCount = 0;
        Renderer[] allRenderers = FindObjectsOfType<Renderer>();
        for (int i = 0; i < allRenderers.Length; i++)
        {
            Renderer r = allRenderers[i];
            if (r == null || r.sharedMaterial == null)
            {
                continue;
            }

            Color c = r.sharedMaterial.color;
            if (r.gameObject.name.ToLower().Contains("gate") && c.r > 0.25f && c.g < 0.15f && c.b < 0.15f)
            {
                redGateCount++;
            }
        }

        int lockCount = 0;
        for (int i = 0; i < stageDoors.Length; i++)
        {
            if (stageDoors[i] != null && stageDoors[i].lockVisual != null)
            {
                lockCount++;
            }
        }

        if (redGateCount > 0)
            Debug.LogError($"[GuidedIntroMazeGenerator] Validation error: {redGateCount} red cube gate blockers remain.");
        else
            Debug.Log("[GuidedIntroMazeGenerator] Validation: no red cube gates remain.");

        if (stageDoorCount < 7)
            Debug.LogError($"[GuidedIntroMazeGenerator] Validation error: expected >=7 stage doors, found {stageDoorCount}.");
        else
            Debug.Log($"[GuidedIntroMazeGenerator] Validation: stage doors present ({stageDoorCount}).");

        if (lockCount < 7)
            Debug.LogError($"[GuidedIntroMazeGenerator] Validation error: expected black lock visuals on all stage doors, found {lockCount}.");
        else
            Debug.Log("[GuidedIntroMazeGenerator] Validation: stage door lock visuals present.");

        Debug.Log("[GuidedIntroMazeGenerator] Final tutorial visual validation complete.");
    }

    private void ValidateIntegrationChecklist(TutorialLayoutContext context)
    {
        if (context == null)
        {
            Debug.LogError("[GuidedIntroMazeGenerator] Integration validation failed: null context.");
            return;
        }

        bool materialsReady = tutorialFloorMaterial != null && tutorialWallMaterial != null && tutorialCeilingMaterial != null;
        Debug.Log(materialsReady
            ? "[GuidedIntroMazeGenerator] Integration validation: tutorial materials assigned."
            : "[GuidedIntroMazeGenerator] Integration validation warning: one or more tutorial materials missing.");

        bool sharedMeasures = mazeCellSize > 0.1f && mazeWallHeight > 0.1f;
        Debug.Log(sharedMeasures
            ? $"[GuidedIntroMazeGenerator] Integration validation: shared measures cell={mazeCellSize}, wall={mazeWallHeight}."
            : "[GuidedIntroMazeGenerator] Integration validation error: invalid shared dimensions.");

        int boundaryDoorFailures = 0;
        TutorialStageDoor[] stageDoors = FindObjectsOfType<TutorialStageDoor>();
        foreach (var d in stageDoors)
        {
            if (d == null) continue;
            float gx = (d.transform.position.x - transform.position.x) / mazeCellSize;
            float gz = (d.transform.position.z - transform.position.z) / mazeCellSize;
            bool halfX = Mathf.Abs(gx * 2f - Mathf.Round(gx * 2f)) < 0.06f;
            bool halfZ = Mathf.Abs(gz * 2f - Mathf.Round(gz * 2f)) < 0.06f;
            bool wholeX = Mathf.Abs(gx - Mathf.Round(gx)) < 0.06f;
            bool wholeZ = Mathf.Abs(gz - Mathf.Round(gz)) < 0.06f;
            if (!((halfX && wholeZ) || (halfZ && wholeX)))
            {
                boundaryDoorFailures++;
            }
        }

        if (boundaryDoorFailures > 0)
            Debug.LogError("[GuidedIntroMazeGenerator] Integration validation error: some tutorial doors are not on cell boundaries.");
        else
            Debug.Log("[GuidedIntroMazeGenerator] Integration validation: all tutorial doors are on valid cell boundaries.");

        if (context.generatedCells.Count == 0)
            Debug.LogError("[GuidedIntroMazeGenerator] Integration validation error: no tutorial cells generated.");
        else
            Debug.Log("[GuidedIntroMazeGenerator] Integration validation: tutorial cells generated and connected checks executed.");
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

        TutorialStageDoor[] stageDoors = FindObjectsOfType<TutorialStageDoor>();
        Gizmos.color = Color.green;
        for (int i = 0; i < stageDoors.Length; i++)
        {
            if (stageDoors[i] == null) continue;
            Gizmos.DrawWireCube(stageDoors[i].transform.position + Vector3.up, new Vector3(0.5f, 2f, 0.5f));
            if (stageDoors[i].lockVisual != null)
            {
                Gizmos.color = Color.black;
                Gizmos.DrawWireSphere(stageDoors[i].lockVisual.transform.position, 0.15f);
                Gizmos.color = Color.green;
            }
        }
    }
}
