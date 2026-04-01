using UnityEngine;
using TMPro;

public class SpatialMapController : MonoBehaviour
{
	[Header("Map Settings")]
	public Transform playerTransform;
	public MazeGenerator mazeGenerator;
	
	[Header("Map Display")]
	public GameObject mapObject; // 3D plane held by player
	public Material mapMaterial;
	public float mapScale = 0.3f;
	public Vector3 mapOffset = new Vector3(0.3f, -0.2f, 0.5f); // Relative to camera
	public KeyCode mapHoldKey = KeyCode.Tab;
	
	[Header("Map Prompt UI")]
	public TextMeshProUGUI mapPromptText;
	public string defaultMapPrompt = "Hold TAB for Map";
	public string emphasizedMapPrompt = "HOLD TAB FOR MAP";
	public Color defaultPromptColor = new Color(0.8f, 0.8f, 0.8f, 0.9f);
	public Color emphasizedPromptColor = Color.white;
	public float defaultPromptScale = 1f;
	public float emphasizedPromptScale = 1.25f;
	
	[Header("Map Rendering")]
	public Color exploredColor = new Color(0.7f, 0.7f, 0.7f);
	public Color unexploredColor = new Color(0.2f, 0.2f, 0.2f);
	public Color playerColor = Color.green;
	public Color minotaurColor = Color.red;
	public Color exitColor = Color.yellow;
	public int mapResolution = 512;
	
	private Texture2D mapTexture;
	private bool[,] exploredCells;
	private bool mapVisible = false;
	private GameObject mapPlane;
	private Camera playerCamera;
	private Transform mapParent; // Parent transform for the map
	private bool needsRedraw = false; // Optimization: only redraw when needed
	private bool isPromptEmphasized;

	void Start()
	{
		playerCamera = Camera.main;
		CreateMapObject();
		InitializeExploration();
		mapObject.SetActive(false);
		RefreshMapPrompt();
	}

	void CreateMapObject()
	{
		// Create 3D plane for map
		mapPlane = GameObject.CreatePrimitive(PrimitiveType.Plane);
		mapPlane.name = "SpatialMap";
		mapPlane.transform.localScale = new Vector3(mapScale, 1f, mapScale);
		
		// Remove collider
		Destroy(mapPlane.GetComponent<Collider>());
		
		// Create texture
		mapTexture = new Texture2D(mapResolution, mapResolution, TextureFormat.RGBA32, false);
		mapTexture.filterMode = FilterMode.Point;
		mapTexture.Apply(false, false);
		
		// Create material
		mapMaterial = new Material(Shader.Find("Unlit/Texture"));
		mapMaterial.mainTexture = mapTexture;

		mapPlane.GetComponent<Renderer>().material = mapMaterial;
		
		mapObject = mapPlane;

		// Create or find parent for the map (attach to player's hand/camera)
		SetupMapParent();
	}

	void SetupMapParent()
	{
		// Look for a hand empty in the player hierarchy
		Transform handEmpty = FindHandEmpty(playerTransform);
	
		if (handEmpty != null)
		{
			mapParent = handEmpty;
		}
		else
		{
			// Fallback: create a parent attached to the camera
			GameObject mapParentObj = new GameObject("MapParent");
			mapParent = mapParentObj.transform;
			if (playerCamera != null)
			{
				mapParent.SetParent(playerCamera.transform);
			}
			else if (playerTransform != null)
			{
				mapParent.SetParent(playerTransform);
			}
		}
	
		// Parent the map to our parent transform and set the desired transform
		mapPlane.transform.SetParent(mapParent, false);
	
		// Set the exact transform values you specified
		mapPlane.transform.localPosition = new Vector3(0f, 0f, 2f);
		mapPlane.transform.localEulerAngles = new Vector3(90f, 0f, 180f);
		mapPlane.transform.localScale = new Vector3(0.2f, 1f, 0.2f);
	}

	Transform FindHandEmpty(Transform player)
	{
		if (player == null) return null;
		
		// Common names for hand/controller empties in VR setups
		string[] handNames = { "Hand", "Controller", "RightHand", "LeftHand", "RightController", "LeftController" };
		
		foreach (string handName in handNames)
		{
			Transform hand = player.Find(handName);
			if (hand != null) return hand;
		}
		
		// Recursively search children
		foreach (Transform child in player)
		{
			Transform found = FindHandEmpty(child);
			if (found != null) return found;
		}
		
		return null;
	}

	void InitializeExploration()
	{
		if (mazeGenerator != null)
		{
			exploredCells = new bool[mazeGenerator.width, mazeGenerator.height];
			// Initialize texture with unexplored color
			ClearTexture(unexploredColor);
		}
	}

	void ClearTexture(Color color)
	{
		for (int x = 0; x < mapResolution; x++)
		{
			for (int y = 0; y < mapResolution; y++)
			{
				mapTexture.SetPixel(x, y, color);
			}
		}
		mapTexture.Apply();
	}

	void Update()
	{
		bool shouldShowMap = Input.GetKey(mapHoldKey);
		if (shouldShowMap != mapVisible)
		{
			SetMapVisible(shouldShowMap);
		}

		if (mapVisible)
		{
			bool explorationChanged = UpdateExploration();
			
			// Only redraw if something changed (optimization)
			if (explorationChanged || needsRedraw)
			{
				RenderMap();
				needsRedraw = false;
			}
		}
	}

	void SetMapVisible(bool visible)
	{
		mapVisible = visible;
		mapObject.SetActive(mapVisible);
		needsRedraw = true;
	}

	public void SetMapPromptEmphasis(bool emphasized)
	{
		if (isPromptEmphasized == emphasized)
		{
			return;
		}

		isPromptEmphasized = emphasized;
		RefreshMapPrompt();
	}

	void RefreshMapPrompt()
	{
		if (mapPromptText == null)
		{
			return;
		}

		mapPromptText.text = isPromptEmphasized ? emphasizedMapPrompt : defaultMapPrompt;
		mapPromptText.color = isPromptEmphasized ? emphasizedPromptColor : defaultPromptColor;
		float scale = isPromptEmphasized ? emphasizedPromptScale : defaultPromptScale;
		mapPromptText.rectTransform.localScale = Vector3.one * Mathf.Max(0.01f, scale);
	}

	bool UpdateExploration()
	{
		if (playerTransform == null || mazeGenerator == null) return false;

		bool changed = false;
		
		// Mark nearby cells as explored
		int cellX = Mathf.FloorToInt(playerTransform.position.x / mazeGenerator.cellSize);
		int cellZ = Mathf.FloorToInt(playerTransform.position.z / mazeGenerator.cellSize);
		int visionRange = 3;

		for (int x = -visionRange; x <= visionRange; x++)
		{
			for (int z = -visionRange; z <= visionRange; z++)
			{
				int checkX = cellX + x;
				int checkZ = cellZ + z;
				if (checkX >= 0 && checkX < mazeGenerator.width && 
					checkZ >= 0 && checkZ < mazeGenerator.height)
				{
					if (!exploredCells[checkX, checkZ])
					{
						exploredCells[checkX, checkZ] = true;
						changed = true;
					}
				}
			}
		}
		
		return changed;
	}

	void RenderMap()
	{
		if (mazeGenerator == null) return;

		int cellPixelSize = mapResolution / Mathf.Max(mazeGenerator.width, mazeGenerator.height);

		// Only redraw explored cells that need updating
		for (int x = 0; x < mazeGenerator.width; x++)
		{
			for (int y = 0; y < mazeGenerator.height; y++)
			{
				if (exploredCells[x, y])
				{
					Color cellColor = mazeGenerator.GetMazeCell(x, y) == 1 ? Color.black : exploredColor;

					// Fill cell pixels
					for (int px = 0; px < cellPixelSize; px++)
					{
						for (int py = 0; py < cellPixelSize; py++)
						{
							mapTexture.SetPixel(x * cellPixelSize + px, y * cellPixelSize + py, cellColor);
						}
					}
				}
			}
		}

		// Draw player position
		DrawMapIcon(playerTransform.position, playerColor, cellPixelSize);
		
		// Draw exit position
		Vector3 exitWorldPos = mazeGenerator.GetExitPosition();
		DrawMapIcon(exitWorldPos, exitColor, cellPixelSize);

		mapTexture.Apply();
	}

	void DrawMapIcon(Vector3 worldPos, Color color, int cellPixelSize)
	{
		int pixelX = Mathf.FloorToInt((worldPos.x / mazeGenerator.cellSize) * cellPixelSize);
		int pixelY = Mathf.FloorToInt((worldPos.z / mazeGenerator.cellSize) * cellPixelSize);
		
		int iconSize = Mathf.Max(1, cellPixelSize); // Smaller, more performant icon
		
		for (int i = -iconSize; i <= iconSize; i++)
		{
			for (int j = -iconSize; j <= iconSize; j++)
			{
				SetPixelSafe(pixelX + i, pixelY + j, color);
			}
		}
	}

	void SetPixelSafe(int x, int y, Color color)
	{
		if (x >= 0 && x < mapResolution && y >= 0 && y < mapResolution)
		{
			mapTexture.SetPixel(x, y, color);
		}
	}
}
