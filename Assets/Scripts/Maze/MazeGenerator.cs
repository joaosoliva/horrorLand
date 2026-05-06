﻿using UnityEngine;
using System.Collections.Generic;

public class MazeGenerator : MonoBehaviour
{
	[Header("Maze Settings")]
	public MazeGenerationConfig sharedMazeConfig;
	public int width = 20;
	public int height = 20;
	public float cellSize = 2f;
	public float wallHeight = 3f;

	[Header("Textures")]
	public Texture2D wallTexture;
	public Texture2D floorTexture;
	public Color wallTint = Color.white;
	public Color floorTint = Color.white;
	public float textureScale = 1f;

	[Header("Player Spawn")]
	public bool spawnPlayerAtStart = true;
	public GameObject playerPrefab;

	private int[,] maze;
	[Header("Layout Mode")]
	public bool useTutorialBlueprint = false;
	private MazeBlueprintData activeBlueprint;
	private Material wallMaterial;
	private Material floorMaterial;
	private Vector2Int startPosition;
	
	// --- START ROOM SYSTEM ---
	[Header("Start Room Settings")]
	public Material startRoomFloorMaterial;
	public Material startRoomWallMaterial;
	public Vector2Int startRoomSize = new Vector2Int(4, 4);
	public Light roomLightPrefab;

	[Header("Procedural Scare Binding")]
	public EnvironmentScareController environmentScareController;
	public ProceduralLamp proceduralLampPrefab;
	public float corridorLampChance = 0.14f;
	public int corridorScareNodeStep = 6;

	[Header("Procedural Safe Space")]
	public bool attachSafeSpaceToProceduralLamps = true;
	public float safeSpaceRadius = 3.5f;
	public float safeSpaceDuration = 12f;
	public float safeSpaceSanityRestorePerSecond = 5f;

	[Header("Exit Door Settings")]
	public Color exitDoorColor = new Color(0.7f, 0.1f, 0.1f);
	
	private Vector2Int exitPosition;
	private DoorTrigger startRoomDoorTrigger;
	
	private float doorHeight = 6f;
	public DoorTrigger StartRoomDoorTrigger => startRoomDoorTrigger;

	void Start()
	{
		ApplySharedGenerationConfig();
		CreateMaterials();
		if (useTutorialBlueprint)
		{
			GenerateTutorialTopologyOnly();
		}
		else
		{
			GenerateMaze();
		}
		DefineExit();
    
		Vector2Int entryCell = FindMazeEntrance();
		Debug.Log($"Entry cell found at: {entryCell}");
		Debug.Log($"Maze value at entry BEFORE carve: {maze[entryCell.x, entryCell.y]}");
    
		CarveMazeEntrance(entryCell);
    
		// Check what was carved
		if (entryCell.x == 0 && entryCell.x + 1 < width)
			Debug.Log($"Maze value at ({entryCell.x + 1}, {entryCell.y}) AFTER carve: {maze[entryCell.x + 1, entryCell.y]}");
    
		BuildMazeGeometry();
		if (useTutorialBlueprint)
		{
			CompareMainMazeAndTutorialUnits();
			CreateBlueprintStageDoors();
		}
		CreateExitDoor();
		
		// CHECK IF WALL EXISTS AFTER BUILDING
		GameObject blockingWall = GameObject.Find("Wall_1_10");
		if (blockingWall != null)
			Debug.LogError("Wall_1_10 EXISTS after BuildMazeGeometry! This shouldn't happen!");
		else
			Debug.Log("Wall_1_10 does NOT exist - good!");
			
		CreateStartRoom();
		BindProceduralScareElements();
	}
	
	bool IsCellAccessible(int x, int y)
	{
		// A cell is accessible if it's connected to the maze (not an isolated boundary cell)
		// Check if it has at least one neighboring path cell inside the maze
		if (x > 0 && maze[x - 1, y] == 0) return true;
		if (x < width - 1 && maze[x + 1, y] == 0) return true;
		if (y > 0 && maze[x, y - 1] == 0) return true;
		if (y < height - 1 && maze[x, y + 1] == 0) return true;
    
		return false;
	}
	
	void DefineExit()
	{
		List<Vector2Int> deadEnds = new List<Vector2Int>();
    
		// Find all dead-end cells (cells with exactly 1 neighbor)
		for (int x = 0; x < width; x++)
		{
			for (int y = 0; y < height; y++)
			{
				if (maze[x, y] == 0) // If it's a path
				{
					int neighbors = CountPathNeighbors(x, y);
					if (neighbors == 1) // Dead-end has exactly 1 connection
					{
						deadEnds.Add(new Vector2Int(x, y));
					}
				}
			}
		}
    
		Vector2Int entrance = FindMazeEntrance();
    
		if (deadEnds.Count > 0)
		{
			// Find the dead-end farthest from the entrance
			Vector2Int farthestDeadEnd = deadEnds[0];
			float maxDistance = 0f;
        
			foreach (Vector2Int deadEnd in deadEnds)
			{
				// Skip the entrance itself if it's a dead-end
				if (deadEnd == entrance) continue;
            
				float distance = Vector2Int.Distance(deadEnd, entrance);
				if (distance > maxDistance)
				{
					maxDistance = distance;
					farthestDeadEnd = deadEnd;
				}
			}
        
			exitPosition = farthestDeadEnd;
			Debug.Log($"Exit defined at dead-end: {exitPosition} ({deadEnds.Count} dead-ends found)");
		}
		else
		{
			// Fallback: use geometric farthest cell
			Debug.LogWarning("No dead-ends found, using farthest cell as fallback");
			exitPosition = FindFarthestCellFromEntrance();
		}
	}

	int CountPathNeighbors(int x, int y)
	{
		int count = 0;
		if (x > 0 && maze[x - 1, y] == 0) count++; // Left
		if (x < width - 1 && maze[x + 1, y] == 0) count++; // Right
		if (y > 0 && maze[x, y - 1] == 0) count++; // Down
		if (y < height - 1 && maze[x, y + 1] == 0) count++; // Up
		return count;
	}

	Vector2Int FindFarthestCellFromEntrance()
	{
		Vector2Int entrance = FindMazeEntrance();
		Vector2Int farthest = entrance;
		float maxDistance = 0f;
    
		for (int x = 0; x < width; x++)
		{
			for (int y = 0; y < height; y++)
			{
				if (maze[x, y] == 0)
				{
					float distance = Vector2Int.Distance(new Vector2Int(x, y), entrance);
					if (distance > maxDistance)
					{
						maxDistance = distance;
						farthest = new Vector2Int(x, y);
					}
				}
			}
		}
		return farthest;
	}
	
	
	// --- DYNAMIC START ROOM SYSTEM ---
	void CreateStartRoom()
	{
		Vector2Int entryCell = FindMazeEntrance();
		Vector3 entryWorld = new Vector3(entryCell.x * cellSize, 0, entryCell.y * cellSize);

		bool onLeft = entryCell.x == 0;
		bool onRight = entryCell.x == width - 1;
		bool onBottom = entryCell.y == 0;
		bool onTop = entryCell.y == height - 1;

		Vector3 facingDir = Vector3.zero;
		Vector3 roomOrigin = Vector3.zero;

		float roomWidth = startRoomSize.x * cellSize;
		float roomDepth = startRoomSize.y * cellSize;

		// Calculate room origin based on entrance position
		if (onLeft)
		{
			facingDir = Vector3.right;
			roomOrigin = entryWorld + new Vector3(-roomWidth, 0, -roomDepth / 2 + cellSize / 2);
		}
		else if (onRight)
		{
			facingDir = Vector3.left;
			roomOrigin = entryWorld + new Vector3(cellSize, 0, -roomDepth / 2 + cellSize / 2);
		}
		else if (onBottom)
		{
			facingDir = Vector3.forward;
			roomOrigin = entryWorld + new Vector3(-roomWidth / 2 + cellSize / 2, 0, -roomDepth);
		}
		else // onTop
		{
			facingDir = Vector3.back;
			roomOrigin = entryWorld + new Vector3(-roomWidth / 2 + cellSize / 2, 0, cellSize);
		}

		GameObject startRoom = new GameObject("StartRoom");
		startRoom.transform.parent = transform;

		// Floor
		GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
		floor.transform.parent = startRoom.transform;
		floor.transform.localScale = new Vector3(roomWidth, 0.1f, roomDepth);
		floor.transform.position = roomOrigin + new Vector3(roomWidth / 2, 0, roomDepth / 2);
		floor.GetComponent<Renderer>().material = startRoomFloorMaterial != null ? startRoomFloorMaterial : floorMaterial;

		// Ceiling
		GameObject ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
		ceiling.transform.parent = startRoom.transform;
		ceiling.transform.localScale = new Vector3(roomWidth, 0.1f, roomDepth);
		ceiling.transform.position = roomOrigin + new Vector3(roomWidth / 2, wallHeight, roomDepth / 2);
		ceiling.GetComponent<Renderer>().material = startRoomWallMaterial != null ? startRoomWallMaterial : wallMaterial;

		// Create walls with door opening
		CreateStartRoomWallsWithDoor(startRoom.transform, roomOrigin, roomWidth, roomDepth, facingDir);

		if (roomLightPrefab != null)
		{
			Light light = Instantiate(roomLightPrefab, startRoom.transform);
			light.transform.position = roomOrigin + new Vector3(roomWidth / 2, wallHeight - 0.5f, roomDepth / 2);
			light.intensity = 3f;
			light.range = 10f;

			ProceduralLamp proceduralLamp = light.gameObject.GetComponent<ProceduralLamp>();
			if (proceduralLamp == null)
			{
				proceduralLamp = light.gameObject.AddComponent<ProceduralLamp>();
			}
			proceduralLamp.zoneId = "start_room";
		}

		// Door position calculation
		Vector3 doorPos;
		if (onLeft)
			doorPos = roomOrigin + new Vector3(roomWidth, 0, roomDepth / 2);
		else if (onRight)
			doorPos = roomOrigin + new Vector3(0, 0, roomDepth / 2);
		else if (onBottom)
			doorPos = roomOrigin + new Vector3(roomWidth / 2, 0, roomDepth);
		else
			doorPos = roomOrigin + new Vector3(roomWidth / 2, 0, 0);

		CreateDoubleDoor(startRoom.transform, doorPos, facingDir);
		CreateRoomScareNode(startRoom.transform, "start_room", roomOrigin + new Vector3(roomWidth / 2f, 1f, roomDepth / 2f), new Vector3(roomWidth * 0.7f, 2f, roomDepth * 0.7f), false);
		PositionExistingPlayerInStartRoom(roomOrigin, roomWidth, roomDepth);
	}
	void CreateStartRoomWallsWithDoor(Transform parent, Vector3 origin, float width, float depth, Vector3 doorDirection)
	{
		float doorWidth = 4f; // Width of the door opening
		float wallThickness = 0.2f;

		// Determine which wall has the door
		bool doorOnFront = doorDirection == Vector3.forward;
		bool doorOnBack = doorDirection == Vector3.back;
		bool doorOnLeft = doorDirection == Vector3.left;
		bool doorOnRight = doorDirection == Vector3.right;

		// Front wall (Z = depth)
		if (!doorOnFront)
		{
			CreateStartRoomWall(parent, origin, new Vector3(width, wallHeight, wallThickness), 
				new Vector3(width / 2, wallHeight / 2, depth));
		}
		else
		{
			CreateWallWithDoor(parent, origin, width, depth, wallThickness, doorWidth, doorHeight, 
				isVertical: true, isFront: true);
		}

		// Back wall (Z = 0)
		if (!doorOnBack)
		{
			CreateStartRoomWall(parent, origin, new Vector3(width, wallHeight, wallThickness), 
				new Vector3(width / 2, wallHeight / 2, 0));
		}
		else
		{
			CreateWallWithDoor(parent, origin, width, 0, wallThickness, doorWidth, doorHeight, 
				isVertical: true, isFront: false);
		}

		// Left wall (X = 0)
		if (!doorOnLeft)
		{
			CreateStartRoomWall(parent, origin, new Vector3(wallThickness, wallHeight, depth), 
				new Vector3(0, wallHeight / 2, depth / 2));
		}
		else
		{
			CreateWallWithDoor(parent, origin, depth, 0, wallThickness, doorWidth, doorHeight, 
				isVertical: false, isLeft: true);
		}

		// Right wall (X = width)
		if (!doorOnRight)
		{
			CreateStartRoomWall(parent, origin, new Vector3(wallThickness, wallHeight, depth), 
				new Vector3(width, wallHeight / 2, depth / 2));
		}
		else
		{
			CreateWallWithDoor(parent, origin, depth, width, wallThickness, doorWidth, doorHeight, 
				isVertical: false, isLeft: false);
		}
	}
	
	void CreateWallWithDoor(Transform parent, Vector3 origin, float wallLength, float wallPosition, float thickness, float doorWidth, float doorHeight, bool isVertical, bool isFront = false, bool isLeft = false)
	{
		float gapCenter = wallLength / 2;
    
		// Calculate segment dimensions for the wall with door
		float leftSegmentLength = gapCenter - doorWidth / 2;
		float rightSegmentLength = wallLength - gapCenter - doorWidth / 2;
    
		float bottomHeight = doorHeight;
		float topHeight = wallHeight - doorHeight;
    
		// Bottom wall segments (below door)
		if (leftSegmentLength > 0 && bottomHeight > 0)
		{
			if (isVertical)
			{
				// Front/Back wall bottom left segment
				CreateStartRoomWall(parent, origin, new Vector3(leftSegmentLength, bottomHeight, thickness), 
					new Vector3(leftSegmentLength / 2, bottomHeight / 2, wallPosition));
            
				// Front/Back wall bottom right segment  
				CreateStartRoomWall(parent, origin, new Vector3(rightSegmentLength, bottomHeight, thickness), 
					new Vector3(gapCenter + doorWidth / 2 + rightSegmentLength / 2, bottomHeight / 2, wallPosition));
			}
			else
			{
				// Left/Right wall bottom left segment
				CreateStartRoomWall(parent, origin, new Vector3(thickness, bottomHeight, leftSegmentLength), 
					new Vector3(wallPosition, bottomHeight / 2, leftSegmentLength / 2));
            
				// Left/Right wall bottom right segment
				CreateStartRoomWall(parent, origin, new Vector3(thickness, bottomHeight, rightSegmentLength), 
					new Vector3(wallPosition, bottomHeight / 2, gapCenter + doorWidth / 2 + rightSegmentLength / 2));
			}
		}
    
		// Top wall segment (above door) - spans the entire width
		if (topHeight > 0)
		{
			if (isVertical)
			{
				CreateStartRoomWall(parent, origin, new Vector3(wallLength, topHeight, thickness), 
					new Vector3(wallLength / 2, doorHeight + topHeight / 2, wallPosition));
			}
			else
			{
				CreateStartRoomWall(parent, origin, new Vector3(thickness, topHeight, wallLength), 
					new Vector3(wallPosition, doorHeight + topHeight / 2, wallLength / 2));
			}
		}
	}
	Vector2Int FindMazeEntrance()
	{
		List<Vector2Int> potentialEntrances = new List<Vector2Int>();
    
		for (int y = 0; y < height; y++)
		{
			if (maze[0, y] == 0 && IsCellAccessible(0, y)) potentialEntrances.Add(new Vector2Int(0, y));
			if (maze[width - 1, y] == 0 && IsCellAccessible(width - 1, y)) potentialEntrances.Add(new Vector2Int(width - 1, y));
		}
		for (int x = 0; x < width; x++)
		{
			if (maze[x, 0] == 0 && IsCellAccessible(x, 0)) potentialEntrances.Add(new Vector2Int(x, 0));
			if (maze[x, height - 1] == 0 && IsCellAccessible(x, height - 1)) potentialEntrances.Add(new Vector2Int(x, height - 1));
		}
    
		if (potentialEntrances.Count > 0)
			return potentialEntrances[Random.Range(0, potentialEntrances.Count)];
    
		return new Vector2Int(0, height / 2);
	}
	void CarveMazeEntrance(Vector2Int cell)
	{
		// Carve the entry cell itself
		maze[cell.x, cell.y] = 0;
    
		// Also carve inner connection
		if (cell.x == 0 && cell.x + 1 < width)
			maze[cell.x + 1, cell.y] = 0;
		else if (cell.x == width - 1 && cell.x - 1 >= 0)
			maze[cell.x - 1, cell.y] = 0;
		else if (cell.y == 0 && cell.y + 1 < height)
			maze[cell.x, cell.y + 1] = 0;
		else if (cell.y == height - 1 && cell.y - 1 >= 0)
			maze[cell.x, cell.y - 1] = 0;

		Debug.Log($"Carved maze entrance connection at {cell}");
	}

	void PositionExistingPlayerInStartRoom(Vector3 roomOrigin, float roomWidth, float roomDepth)
	{
		GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
		if (playerObj == null) return;

		Vector3 center = roomOrigin + new Vector3(roomWidth / 2, 1.8f, roomDepth / 2);
		playerObj.transform.position = center;

		GameObject door = GameObject.Find("StartRoomDoor");
		if (door != null)
		{
			Vector3 dir = (door.transform.position - playerObj.transform.position).normalized;
			dir.y = 0;
			if (dir != Vector3.zero)
				playerObj.transform.rotation = Quaternion.LookRotation(dir);
		}
	}

	void CreateStartRoomWall(Transform parent, Vector3 origin, Vector3 size, Vector3 offset)
	{
		GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
		wall.transform.parent = parent;
		wall.transform.localScale = size;
		wall.transform.position = origin + offset;
		wall.GetComponent<Renderer>().material = startRoomWallMaterial != null ? startRoomWallMaterial : wallMaterial;
	}

	void CreateDoubleDoor(Transform parent, Vector3 position, Vector3 facingDirection)
	{
		GameObject doorRoot = new GameObject("StartRoomDoor");
		doorRoot.transform.parent = parent;
    
		// Position at the door hole location - at floor level
		doorRoot.transform.position = position;
		doorRoot.transform.rotation = Quaternion.LookRotation(facingDirection);

		float doorWidth = 3.95f;
		float doorThickness = 0.1f;
		float doorHeight = this.doorHeight;
    
		// Create left door - positioned so it rotates from left hinge
		GameObject leftDoor = new GameObject("Door_Left");
		leftDoor.transform.parent = doorRoot.transform;
		leftDoor.transform.localPosition = new Vector3(-doorWidth / 2, doorHeight / 2, 0); // Position at left edge
		leftDoor.transform.localRotation = Quaternion.identity;
    
		// Create door visual as child of the hinge object
		GameObject leftDoorVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
		leftDoorVisual.name = "LeftDoor_Visual";
		leftDoorVisual.transform.parent = leftDoor.transform;
		leftDoorVisual.transform.localScale = new Vector3(doorWidth / 2, doorHeight, doorThickness);
		leftDoorVisual.transform.localPosition = new Vector3(doorWidth / 4, 0, -doorThickness / 2); // Offset to right from hinge
		leftDoorVisual.transform.localRotation = Quaternion.identity;
		leftDoorVisual.GetComponent<Renderer>().material.color = new Color(0.6f, 0.4f, 0.2f);
		DestroyImmediate(leftDoorVisual.GetComponent<BoxCollider>());

		// Create right door - positioned so it rotates from right hinge
		GameObject rightDoor = new GameObject("Door_Right");
		rightDoor.transform.parent = doorRoot.transform;
		rightDoor.transform.localPosition = new Vector3(doorWidth / 2, doorHeight / 2, 0); // Position at right edge
		rightDoor.transform.localRotation = Quaternion.identity;
    
		// Create door visual as child of the hinge object
		GameObject rightDoorVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
		rightDoorVisual.name = "RightDoor_Visual";
		rightDoorVisual.transform.parent = rightDoor.transform;
		rightDoorVisual.transform.localScale = new Vector3(doorWidth / 2, doorHeight, doorThickness);
		rightDoorVisual.transform.localPosition = new Vector3(-doorWidth / 4, 0, -doorThickness / 2); // Offset to left from hinge
		rightDoorVisual.transform.localRotation = Quaternion.identity;
		rightDoorVisual.GetComponent<Renderer>().material.color = new Color(0.6f, 0.4f, 0.2f);
		DestroyImmediate(rightDoorVisual.GetComponent<BoxCollider>());

		// Add door frames

		// Setup trigger collider INSIDE the room (behind the doors)
		BoxCollider triggerCollider = doorRoot.AddComponent<BoxCollider>();
		triggerCollider.isTrigger = true;
    
		// Position trigger inside the room (negative Z in local space since doors face outward)
		triggerCollider.size = new Vector3(doorWidth, doorHeight, 4f);
		triggerCollider.center = new Vector3(0, doorHeight / 2, -2f); // Behind the doors (inside room)

		// Add DoorTrigger to the root object
		DoorTrigger trigger = doorRoot.AddComponent<DoorTrigger>();
		trigger.doorWidth = doorWidth;
		trigger.doorHeight = doorHeight;
		trigger.facingDirection = facingDirection;
		startRoomDoorTrigger = trigger;

		ProceduralScareDoor scareDoor = doorRoot.AddComponent<ProceduralScareDoor>();
		scareDoor.zoneId = "start_room";
		scareDoor.closeBehindDelay = 0.4f;
	}


	void AddDoorTrigger(GameObject door, float height)
	{
		DoorTrigger trigger = door.AddComponent<DoorTrigger>();
    
		// Also add a trigger collider to the individual door for interaction
		BoxCollider doorTrigger = door.AddComponent<BoxCollider>();
		doorTrigger.isTrigger = true;
		doorTrigger.size = new Vector3(1f, height, 0.5f); // Slightly larger for easy interaction
		doorTrigger.center = new Vector3(0, 0, 0.25f);
	}

	void CreateBlueprintStageDoors()
	{
		if (activeBlueprint == null || activeBlueprint.edges == null)
		{
			return;
		}

		for (int i = 0; i < activeBlueprint.edges.Count; i++)
		{
			var edge = activeBlueprint.edges[i];
			if (!edge.requiresDoor)
			{
				continue;
			}

			Vector3 a = new Vector3(edge.a.x * cellSize + cellSize * 0.5f, 0f, edge.a.y * cellSize + cellSize * 0.5f);
			Vector3 b = new Vector3(edge.b.x * cellSize + cellSize * 0.5f, 0f, edge.b.y * cellSize + cellSize * 0.5f);
			Vector3 pos = (a + b) * 0.5f;
			Vector3 dir = (b - a).normalized;

			GameObject door = CreateDoubleDoorForBlueprint(transform, edge.doorId, pos, dir);
			Debug.Log($"[MazeGenerator] DoorId={edge.doorId} adjacent A=({edge.a.x},{edge.a.y}) B=({edge.b.x},{edge.b.y}) position={pos} rotation={door.transform.eulerAngles}");
		}
	}

	GameObject CreateDoubleDoorForBlueprint(Transform parent, string doorId, Vector3 position, Vector3 facingDirection)
	{
		GameObject doorRoot = new GameObject(doorId);
		doorRoot.transform.parent = parent;
		doorRoot.transform.position = position;
		doorRoot.transform.rotation = Quaternion.LookRotation(facingDirection);

		float doorWidth = sharedMazeConfig != null ? sharedMazeConfig.doorWidth : 3.95f;
		float doorThickness = sharedMazeConfig != null ? sharedMazeConfig.doorThickness : 0.1f;
		float fullDoorHeight = sharedMazeConfig != null ? sharedMazeConfig.doorHeight : doorHeight;

		GameObject leftDoor = new GameObject("Door_Left");
		leftDoor.transform.parent = doorRoot.transform;
		leftDoor.transform.localPosition = new Vector3(-doorWidth / 2f, fullDoorHeight / 2f, 0f);
		GameObject leftDoorVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
		leftDoorVisual.name = "LeftDoor_Visual";
		leftDoorVisual.transform.parent = leftDoor.transform;
		leftDoorVisual.transform.localScale = new Vector3(doorWidth / 2f, fullDoorHeight, doorThickness);
		leftDoorVisual.transform.localPosition = new Vector3(doorWidth / 4f, 0f, -doorThickness / 2f);
		if (sharedMazeConfig != null && sharedMazeConfig.doorMaterial != null) leftDoorVisual.GetComponent<Renderer>().material = sharedMazeConfig.doorMaterial;
		DestroyImmediate(leftDoorVisual.GetComponent<BoxCollider>());

		GameObject rightDoor = new GameObject("Door_Right");
		rightDoor.transform.parent = doorRoot.transform;
		rightDoor.transform.localPosition = new Vector3(doorWidth / 2f, fullDoorHeight / 2f, 0f);
		GameObject rightDoorVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
		rightDoorVisual.name = "RightDoor_Visual";
		rightDoorVisual.transform.parent = rightDoor.transform;
		rightDoorVisual.transform.localScale = new Vector3(doorWidth / 2f, fullDoorHeight, doorThickness);
		rightDoorVisual.transform.localPosition = new Vector3(-doorWidth / 4f, 0f, -doorThickness / 2f);
		if (sharedMazeConfig != null && sharedMazeConfig.doorMaterial != null) rightDoorVisual.GetComponent<Renderer>().material = sharedMazeConfig.doorMaterial;
		DestroyImmediate(rightDoorVisual.GetComponent<BoxCollider>());

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
		stageDoor.doorId = doorId;
		stageDoor.doorTrigger = trigger;
		stageDoor.startsLocked = true;

		return doorRoot;
	}

	void CreateExitDoor()
	{
		Vector3 exitWorldCenter = GetExitPosition();
		Vector3 doorFacingDirection = GetExitDoorFacingDirection();
		Vector3 doorWorldPosition = exitWorldCenter + (doorFacingDirection * (cellSize * 0.5f));

		GameObject exitDoorRoot = new GameObject("MazeExitDoor");
		exitDoorRoot.transform.SetParent(transform);
		exitDoorRoot.transform.position = new Vector3(doorWorldPosition.x, 0f, doorWorldPosition.z);
		exitDoorRoot.transform.rotation = Quaternion.LookRotation(doorFacingDirection);

		float doorWidth = 3.95f;
		float doorThickness = 0.1f;
		float fullDoorHeight = doorHeight;
		float frameThickness = 0.2f;

		CreateExitWallFrame(exitDoorRoot.transform, doorWidth, fullDoorHeight, frameThickness);

		GameObject leftDoor = new GameObject("Door_Left");
		leftDoor.transform.SetParent(exitDoorRoot.transform);
		leftDoor.transform.localPosition = new Vector3(-doorWidth / 2f, fullDoorHeight / 2f, 0f);
		leftDoor.transform.localRotation = Quaternion.identity;

		GameObject leftDoorVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
		leftDoorVisual.name = "LeftDoor_Visual";
		leftDoorVisual.transform.SetParent(leftDoor.transform);
		leftDoorVisual.transform.localScale = new Vector3(doorWidth / 2f, fullDoorHeight, doorThickness);
		leftDoorVisual.transform.localPosition = new Vector3(doorWidth / 4f, 0f, -doorThickness / 2f);
		leftDoorVisual.transform.localRotation = Quaternion.identity;
		leftDoorVisual.GetComponent<Renderer>().material.color = exitDoorColor;
		DestroyImmediate(leftDoorVisual.GetComponent<BoxCollider>());

		GameObject rightDoor = new GameObject("Door_Right");
		rightDoor.transform.SetParent(exitDoorRoot.transform);
		rightDoor.transform.localPosition = new Vector3(doorWidth / 2f, fullDoorHeight / 2f, 0f);
		rightDoor.transform.localRotation = Quaternion.identity;

		GameObject rightDoorVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
		rightDoorVisual.name = "RightDoor_Visual";
		rightDoorVisual.transform.SetParent(rightDoor.transform);
		rightDoorVisual.transform.localScale = new Vector3(doorWidth / 2f, fullDoorHeight, doorThickness);
		rightDoorVisual.transform.localPosition = new Vector3(-doorWidth / 4f, 0f, -doorThickness / 2f);
		rightDoorVisual.transform.localRotation = Quaternion.identity;
		rightDoorVisual.GetComponent<Renderer>().material.color = exitDoorColor;
		DestroyImmediate(rightDoorVisual.GetComponent<BoxCollider>());

		BoxCollider exitTriggerCollider = exitDoorRoot.AddComponent<BoxCollider>();
		exitTriggerCollider.isTrigger = true;
		exitTriggerCollider.size = new Vector3(doorWidth + 1f, fullDoorHeight, 4f);
		exitTriggerCollider.center = new Vector3(0f, fullDoorHeight / 2f, 0f);

		exitDoorRoot.AddComponent<MazeExitDoor>();
	}

	void CreateExitWallFrame(Transform doorRoot, float doorWidth, float doorHeight, float frameThickness)
	{
		float sideSegmentWidth = Mathf.Max(0.25f, (cellSize - doorWidth) * 0.5f);
		float topHeight = Mathf.Max(0.25f, wallHeight - doorHeight);

		CreateExitFrameSegment("ExitWall_Left", doorRoot, new Vector3(sideSegmentWidth, doorHeight, frameThickness), new Vector3(-(doorWidth * 0.5f) - (sideSegmentWidth * 0.5f), doorHeight * 0.5f, 0f));
		CreateExitFrameSegment("ExitWall_Right", doorRoot, new Vector3(sideSegmentWidth, doorHeight, frameThickness), new Vector3((doorWidth * 0.5f) + (sideSegmentWidth * 0.5f), doorHeight * 0.5f, 0f));

		if (topHeight > 0.01f)
		{
			CreateExitFrameSegment("ExitWall_Top", doorRoot, new Vector3(cellSize, topHeight, frameThickness), new Vector3(0f, doorHeight + (topHeight * 0.5f), 0f));
		}
	}

	void CreateExitFrameSegment(string segmentName, Transform parent, Vector3 localScale, Vector3 localPosition)
	{
		GameObject segment = GameObject.CreatePrimitive(PrimitiveType.Cube);
		segment.name = segmentName;
		segment.transform.SetParent(parent);
		segment.transform.localPosition = localPosition;
		segment.transform.localRotation = Quaternion.identity;
		segment.transform.localScale = localScale;
		segment.GetComponent<Renderer>().material = wallMaterial;
	}

	Vector3 GetExitDoorFacingDirection()
	{
		Vector2Int[] offsets = new Vector2Int[]
		{
			Vector2Int.up,
			Vector2Int.right,
			Vector2Int.down,
			Vector2Int.left
		};

		for (int i = 0; i < offsets.Length; i++)
		{
			Vector2Int neighbor = exitPosition + offsets[i];
			if (neighbor.x < 0 || neighbor.x >= width || neighbor.y < 0 || neighbor.y >= height)
			{
				continue;
			}

			if (maze[neighbor.x, neighbor.y] == 0)
			{
				Vector2 direction = (Vector2)(exitPosition - neighbor);
				return new Vector3(direction.x, 0f, direction.y).normalized;
			}
		}

		return Vector3.forward;
	}


	void ApplySharedGenerationConfig()
	{
		if (sharedMazeConfig == null)
		{
			return;
		}

		cellSize = sharedMazeConfig.cellSize;
		wallHeight = sharedMazeConfig.wallHeight;
		if (startRoomFloorMaterial == null) startRoomFloorMaterial = sharedMazeConfig.floorMaterial;
		if (startRoomWallMaterial == null) startRoomWallMaterial = sharedMazeConfig.wallMaterial;
		Debug.Log("[MazeGenerator] Using shared MazeGenerationConfig.");
	}

	void CompareMainMazeAndTutorialUnits()
	{
		float mainCellSize = sharedMazeConfig != null ? sharedMazeConfig.cellSize : cellSize;
		float tutorialCellSize = cellSize;
		float mainWallHeight = sharedMazeConfig != null ? sharedMazeConfig.wallHeight : wallHeight;
		float tutorialWallHeight = wallHeight;

		float mainRoom20 = 20f * mainCellSize;
		float tutorialRoom20 = 20f * tutorialCellSize;

		bool cellMatch = Mathf.Abs(mainCellSize - tutorialCellSize) < 0.001f;
		bool wallMatch = Mathf.Abs(mainWallHeight - tutorialWallHeight) < 0.001f;
		bool roomMatch = Mathf.Abs(mainRoom20 - tutorialRoom20) < 0.001f;

		Debug.Log($"[MazeGenerator] CompareMainMazeAndTutorialUnits main cell size={mainCellSize}, tutorial cell size={tutorialCellSize}");
		Debug.Log($"[MazeGenerator] CompareMainMazeAndTutorialUnits main wall height={mainWallHeight}, tutorial wall height={tutorialWallHeight}");
		Debug.Log($"[MazeGenerator] CompareMainMazeAndTutorialUnits main 20x20 bounds={mainRoom20}, tutorial 20x20 bounds={tutorialRoom20}");
		Debug.Log($"[MazeGenerator] CompareMainMazeAndTutorialUnits pass={(cellMatch && wallMatch && roomMatch)}");
	}

	void CreateMaterials()
	{
		wallMaterial = new Material(Shader.Find("Standard"));
		wallMaterial.color = wallTint;
		if (wallTexture != null) wallMaterial.mainTexture = wallTexture;
		wallMaterial.SetFloat("_Metallic", 0.1f);
		wallMaterial.SetFloat("_Glossiness", 0.3f);

		floorMaterial = new Material(Shader.Find("Standard"));
		floorMaterial.color = floorTint;
		if (floorTexture != null) floorMaterial.mainTexture = floorTexture;
		floorMaterial.SetFloat("_Metallic", 0f);
		floorMaterial.SetFloat("_Glossiness", 0.2f);
	}

	void GenerateTutorialTopologyOnly()
	{
		activeBlueprint = TutorialMazeBlueprintFactory.CreateDefaultTutorialBlueprint();
		width = activeBlueprint.width;
		height = activeBlueprint.height;
		maze = new int[activeBlueprint.width, activeBlueprint.height];
		for (int x = 0; x < activeBlueprint.width; x++)
			for (int y = 0; y < activeBlueprint.height; y++)
				maze[x, y] = 1;

		for (int i = 0; i < activeBlueprint.cells.Count; i++)
		{
			var c = activeBlueprint.cells[i].cell;
			if (c.x >= 0 && c.x < activeBlueprint.width && c.y >= 0 && c.y < activeBlueprint.height)
				maze[c.x, c.y] = 0;
		}

		for (int i = 0; i < activeBlueprint.edges.Count; i++)
		{
			var e = activeBlueprint.edges[i];
			Debug.Log($"[MazeGenerator] Tutorial edge {i}: ({e.a.x},{e.a.y}) -> ({e.b.x},{e.b.y}), door={e.requiresDoor}, id={e.doorId}");
		}

		Debug.Log($"[MazeGenerator] Tutorial blueprint topology generated. Cells={activeBlueprint.cells.Count}, Edges={activeBlueprint.edges.Count}");
	}

	void GenerateMaze()
	{
		maze = new int[width, height];
		for (int x = 0; x < width; x++)
			for (int y = 0; y < height; y++)
				maze[x, y] = 1;

		Stack<Vector2Int> stack = new Stack<Vector2Int>();
		startPosition = new Vector2Int(1, 1);
		maze[startPosition.x, startPosition.y] = 0;
		stack.Push(startPosition);

		Vector2Int[] directions = {
			new Vector2Int(0, 2), new Vector2Int(2, 0),
			new Vector2Int(0, -2), new Vector2Int(-2, 0)
		};

		while (stack.Count > 0)
		{
			Vector2Int current = stack.Peek();
			List<Vector2Int> neighbors = new List<Vector2Int>();
			foreach (Vector2Int dir in directions)
			{
				Vector2Int next = current + dir;
				if (next.x > 0 && next.x < width - 1 && next.y > 0 && next.y < height - 1 && maze[next.x, next.y] == 1)
					neighbors.Add(next);
			}

			if (neighbors.Count > 0)
			{
				Vector2Int chosen = neighbors[Random.Range(0, neighbors.Count)];
				Vector2Int wall = current + (chosen - current) / 2;
				maze[wall.x, wall.y] = 0;
				maze[chosen.x, chosen.y] = 0;
				stack.Push(chosen);
			}
			else stack.Pop();
		}
	}

	void BuildMazeGeometry()
	{
		GameObject wallsParent = new GameObject("Walls");
		wallsParent.transform.parent = transform;
		wallsParent.isStatic = true;

		GameObject floorParent = new GameObject("Floor");
		floorParent.transform.parent = transform;
		floorParent.isStatic = true;

		CreateFloor(floorParent.transform);
		CreateCeiling(floorParent.transform);

		for (int x = 0; x < width; x++)
			for (int y = 0; y < height; y++)
				if (maze[x, y] == 1)
					CreateWall(x, y, wallsParent.transform);

		BuildProceduralCorridorScareElements(floorParent.transform);

		//CreateBoundaryWalls(wallsParent.transform);
	}

	void BuildProceduralCorridorScareElements(Transform parent)
	{
		int corridorIndex = 0;
		for (int x = 1; x < width - 1; x++)
		{
			for (int y = 1; y < height - 1; y++)
			{
				if (maze[x, y] != 0)
				{
					continue;
				}

				Vector3 worldCenter = new Vector3(x * cellSize + cellSize / 2f, 0f, y * cellSize + cellSize / 2f);
				string zoneId = "corridor_" + x + "_" + y;

				if (corridorScareNodeStep > 0 && corridorIndex % corridorScareNodeStep == 0)
				{
					CreateRoomScareNode(parent, zoneId, worldCenter + Vector3.up, new Vector3(cellSize * 0.85f, 2f, cellSize * 0.85f), true);
				}

				if (Random.value <= corridorLampChance)
				{
					CreateProceduralLamp(parent, worldCenter + new Vector3(0f, wallHeight - 0.4f, 0f), zoneId);
				}

				corridorIndex++;
			}
		}
	}

	void CreateProceduralLamp(Transform parent, Vector3 worldPosition, string zoneId)
	{
		ProceduralLamp lampComponent = null;
		if (proceduralLampPrefab != null)
		{
			lampComponent = Instantiate(proceduralLampPrefab, worldPosition, Quaternion.identity, parent);
		}
		else
		{
			GameObject lampObject = new GameObject("ProceduralLamp_" + zoneId);
			lampObject.transform.SetParent(parent);
			lampObject.transform.position = worldPosition;
			Light light = lampObject.AddComponent<Light>();
			light.type = LightType.Point;
			light.range = cellSize * 3f;
			light.intensity = 2.2f;
			lampComponent = lampObject.AddComponent<ProceduralLamp>();
		}

		if (lampComponent != null)
		{
			lampComponent.zoneId = zoneId;
			lampComponent.lightId = lampComponent.gameObject.name;

			if (attachSafeSpaceToProceduralLamps)
			{
				SafeSpaceZone safeZone = lampComponent.GetComponent<SafeSpaceZone>();
				if (safeZone == null)
				{
					safeZone = lampComponent.gameObject.AddComponent<SafeSpaceZone>();
				}

				safeZone.activeRadius = safeSpaceRadius;
				safeZone.activeDuration = safeSpaceDuration;
				safeZone.sanityRestorePerSecond = safeSpaceSanityRestorePerSecond;
				safeZone.safeAreaLight = lampComponent.GetComponent<Light>();
			}
		}
	}

	void CreateRoomScareNode(Transform parent, string zoneId, Vector3 worldPosition, Vector3 triggerSize, bool isCorridor)
	{
		GameObject nodeObject = new GameObject("ScareNode_" + zoneId);
		nodeObject.transform.SetParent(parent);
		nodeObject.transform.position = worldPosition;

		BoxCollider trigger = nodeObject.AddComponent<BoxCollider>();
		trigger.isTrigger = true;
		trigger.size = triggerSize;

		ProceduralRoomScareNode scareNode = nodeObject.AddComponent<ProceduralRoomScareNode>();
		scareNode.zoneId = zoneId;
		scareNode.isCorridorNode = isCorridor;
		scareNode.lingerTriggerSeconds = isCorridor ? 4.5f : 3f;
	}

	void BindProceduralScareElements()
	{
		if (environmentScareController == null)
		{
			environmentScareController = FindObjectOfType<EnvironmentScareController>();
		}
		if (environmentScareController == null)
		{
			return;
		}

		ProceduralRoomScareNode[] nodes = FindObjectsOfType<ProceduralRoomScareNode>();
		for (int i = 0; i < nodes.Length; i++)
		{
			environmentScareController.RegisterRoomNode(nodes[i]);
		}

		ProceduralScareDoor[] doors = FindObjectsOfType<ProceduralScareDoor>();
		for (int i = 0; i < doors.Length; i++)
		{
			environmentScareController.RegisterDoor(doors[i]);
		}

		ProceduralLamp[] lamps = FindObjectsOfType<ProceduralLamp>();
		for (int i = 0; i < lamps.Length; i++)
		{
			environmentScareController.RegisterLight(lamps[i]);
		}
	}

	void CreateFloor(Transform parent)
	{
		GameObject floor = new GameObject("Floor");
		floor.transform.parent = parent;
		floor.isStatic = true;
		MeshFilter mf = floor.AddComponent<MeshFilter>();
		MeshRenderer mr = floor.AddComponent<MeshRenderer>();
        
		float floorWidth = width * cellSize;
		float floorDepth = height * cellSize;

		Mesh mesh = new Mesh();
		Vector3[] vertices = {
			new Vector3(0, 0, 0), new Vector3(floorWidth, 0, 0),
			new Vector3(0, 0, floorDepth), new Vector3(floorWidth, 0, floorDepth)
		};
		int[] triangles = { 0, 2, 1, 2, 3, 1 };
		Vector2[] uvs = {
			new Vector2(0, 0),
			new Vector2(width * textureScale, 0),
			new Vector2(0, height * textureScale),
			new Vector2(width * textureScale, height * textureScale)
		};
		mesh.vertices = vertices;
		mesh.triangles = triangles;
		mesh.uv = uvs;
		mesh.RecalculateNormals();

		mf.mesh = mesh;
		mr.material = floorMaterial;
		floor.AddComponent<MeshCollider>().sharedMesh = mesh;
	}

	void CreateWall(int x, int y, Transform parent)
	{
		GameObject wall = new GameObject($"Wall_{x}_{y}");
		wall.transform.parent = parent;
		wall.transform.position = new Vector3(x * cellSize + cellSize / 2, wallHeight / 2, y * cellSize + cellSize / 2);
		wall.isStatic = true;

		MeshFilter mf = wall.AddComponent<MeshFilter>();
		MeshRenderer mr = wall.AddComponent<MeshRenderer>();
		BoxCollider bc = wall.AddComponent<BoxCollider>();

		mf.mesh = CreateCubeMesh(cellSize, wallHeight, cellSize);
		mr.material = wallMaterial;
		bc.size = new Vector3(cellSize, wallHeight, cellSize);
	}
	
	void CreateCeiling(Transform parent)
	{
		GameObject ceiling = new GameObject("Ceiling");
		ceiling.transform.parent = parent;
		ceiling.isStatic = true;

		MeshFilter mf = ceiling.AddComponent<MeshFilter>();
		MeshRenderer mr = ceiling.AddComponent<MeshRenderer>();

		float cw = width * cellSize;
		float cd = height * cellSize;
		float ch = wallHeight;

		Mesh mesh = new Mesh();
		Vector3[] vertices = {
			new Vector3(0, ch, 0), new Vector3(cw, ch, 0),
			new Vector3(0, ch, cd), new Vector3(cw, ch, cd)
		};
		int[] triangles = { 0, 1, 2, 1, 3, 2 };
		Vector2[] uvs = { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
		mesh.vertices = vertices;
		mesh.triangles = triangles;
		mesh.uv = uvs;
		mesh.RecalculateNormals();

		mf.mesh = mesh;

		Material ceilingMat = new Material(Shader.Find("Standard"));
		ceilingMat.color = Color.black;
		mr.material = ceilingMat;
	}

	void CreateBoundaryWalls(Transform parent)
	{
		float thickness = 0.5f;
		float floorWidth = width * cellSize;
		float floorDepth = height * cellSize;

		Vector2Int entryCell = FindMazeEntrance();
		float entryX = entryCell.x * cellSize + cellSize / 2;
		float entryZ = entryCell.y * cellSize + cellSize / 2;
		float skipSize = cellSize * 1.5f;

		// North
		if (entryCell.y != height - 1)
			CreateBoundaryWall("BoundaryNorth", parent, new Vector3(floorWidth / 2, wallHeight / 2, floorDepth + thickness / 2), new Vector3(floorWidth + thickness * 2, wallHeight, thickness));
		else
		{
			float gapCenter = entryX;
			CreateBoundaryWall("BoundaryNorth_Left", parent, new Vector3(gapCenter / 2, wallHeight / 2, floorDepth + thickness / 2), new Vector3(gapCenter - skipSize / 2, wallHeight, thickness));
			CreateBoundaryWall("BoundaryNorth_Right", parent, new Vector3((floorWidth + gapCenter) / 2, wallHeight / 2, floorDepth + thickness / 2), new Vector3(floorWidth - gapCenter - skipSize / 2, wallHeight, thickness));
		}

		// South
		if (entryCell.y != 0)
			CreateBoundaryWall("BoundarySouth", parent, new Vector3(floorWidth / 2, wallHeight / 2, -thickness / 2), new Vector3(floorWidth + thickness * 2, wallHeight, thickness));
		else
		{
			float gapCenter = entryX;
			CreateBoundaryWall("BoundarySouth_Left", parent, new Vector3(gapCenter / 2, wallHeight / 2, -thickness / 2), new Vector3(gapCenter - skipSize / 2, wallHeight, thickness));
			CreateBoundaryWall("BoundarySouth_Right", parent, new Vector3((floorWidth + gapCenter) / 2, wallHeight / 2, -thickness / 2), new Vector3(floorWidth - gapCenter - skipSize / 2, wallHeight, thickness));
		}

		// East
		if (entryCell.x != width - 1)
			CreateBoundaryWall("BoundaryEast", parent, new Vector3(floorWidth + thickness / 2, wallHeight / 2, floorDepth / 2), new Vector3(thickness, wallHeight, floorDepth));
		else
		{
			float gapCenter = entryZ;
			CreateBoundaryWall("BoundaryEast_Top", parent, new Vector3(floorWidth + thickness / 2, wallHeight / 2, gapCenter / 2), new Vector3(thickness, wallHeight, gapCenter - skipSize / 2));
			CreateBoundaryWall("BoundaryEast_Bottom", parent, new Vector3(floorWidth + thickness / 2, wallHeight / 2, (floorDepth + gapCenter) / 2), new Vector3(thickness, wallHeight, floorDepth - gapCenter - skipSize / 2));
		}

		// West
		if (entryCell.x != 0)
			CreateBoundaryWall("BoundaryWest", parent, new Vector3(-thickness / 2, wallHeight / 2, floorDepth / 2), new Vector3(thickness, wallHeight, floorDepth));
		else
		{
			float gapCenter = entryZ;
			CreateBoundaryWall("BoundaryWest_Top", parent, new Vector3(-thickness / 2, wallHeight / 2, gapCenter / 2), new Vector3(thickness, wallHeight, gapCenter - skipSize / 2));
			CreateBoundaryWall("BoundaryWest_Bottom", parent, new Vector3(-thickness / 2, wallHeight / 2, (floorDepth + gapCenter) / 2), new Vector3(thickness, wallHeight, floorDepth - gapCenter - skipSize / 2));
		}
	}

	void CreateBoundaryWall(string name, Transform parent, Vector3 position, Vector3 size)
	{
		GameObject wall = new GameObject(name);
		wall.transform.parent = parent;
		wall.transform.position = position;
		wall.isStatic = true;

		MeshFilter mf = wall.AddComponent<MeshFilter>();
		MeshRenderer mr = wall.AddComponent<MeshRenderer>();
		BoxCollider bc = wall.AddComponent<BoxCollider>();

		Mesh mesh = CreateCubeMesh(size.x, size.y, size.z);
		mf.mesh = mesh;
		mr.material = wallMaterial;
		bc.size = size;
	}

	Mesh CreateCubeMesh(float width, float height, float depth)
	{
		Mesh mesh = new Mesh();
		float w = width / 2, h = height / 2, d = depth / 2;

		Vector3[] vertices = new Vector3[24];
		// Front
		vertices[0] = new Vector3(-w, -h, d); vertices[1] = new Vector3(w, -h, d);
		vertices[2] = new Vector3(w, h, d); vertices[3] = new Vector3(-w, h, d);
		// Back
		vertices[4] = new Vector3(w, -h, -d); vertices[5] = new Vector3(-w, -h, -d);
		vertices[6] = new Vector3(-w, h, -d); vertices[7] = new Vector3(w, h, -d);
		// Top
		vertices[8] = new Vector3(-w, h, d); vertices[9] = new Vector3(w, h, d);
		vertices[10] = new Vector3(w, h, -d); vertices[11] = new Vector3(-w, h, -d);
		// Bottom
		vertices[12] = new Vector3(-w, -h, -d); vertices[13] = new Vector3(w, -h, -d);
		vertices[14] = new Vector3(w, -h, d); vertices[15] = new Vector3(-w, -h, d);
		// Right
		vertices[16] = new Vector3(w, -h, d); vertices[17] = new Vector3(w, -h, -d);
		vertices[18] = new Vector3(w, h, -d); vertices[19] = new Vector3(w, h, d);
		// Left
		vertices[20] = new Vector3(-w, -h, -d); vertices[21] = new Vector3(-w, -h, d);
		vertices[22] = new Vector3(-w, h, d); vertices[23] = new Vector3(-w, h, -d);

		int[] triangles = {
			0,1,3, 1,2,3, 4,5,7, 5,6,7, 8,9,11, 9,10,11,
			12,13,15, 13,14,15, 16,17,19, 17,18,19, 20,21,23, 21,22,23
		};

		Vector2[] uvs = new Vector2[24];
		for (int i = 0; i < 6; i++)
		{
			int idx = i * 4;
			uvs[idx] = new Vector2(0, 0);
			uvs[idx + 1] = new Vector2(textureScale, 0);
			uvs[idx + 2] = new Vector2(textureScale, textureScale);
			uvs[idx + 3] = new Vector2(0, textureScale);
		}

		mesh.vertices = vertices;
		mesh.triangles = triangles;
		mesh.uv = uvs;
		mesh.RecalculateNormals();
		return mesh;
	}

	void SpawnPlayer()
	{
		Vector3 spawnPos = new Vector3(startPosition.x * cellSize + cellSize / 2, 1.8f, startPosition.y * cellSize + cellSize / 2);
		GameObject player = Instantiate(playerPrefab, spawnPos, Quaternion.identity);
		player.name = "Player";
	}

	public Vector3 GetStartPosition()
	{
		return new Vector3(startPosition.x * cellSize + cellSize / 2, 1.8f, startPosition.y * cellSize + cellSize / 2);
	}
	
	public int GetMazeCell(int x, int y)
	{
		if (x >= 0 && x < width && y >= 0 && y < height)
			return maze[x, y];
		return 1;
	}

	public Vector3 GetExitPosition()
	{
		return new Vector3(exitPosition.x * cellSize + cellSize / 2, 1f, exitPosition.y * cellSize + cellSize / 2);
	}

	public Vector2Int GetExitCell()
	{
		return exitPosition;
	}
}
