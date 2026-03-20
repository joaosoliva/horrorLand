using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class VillainAI : MonoBehaviour
{
	[Header("AI References")]
	public Transform player;
	public MazeGenerator mazeGenerator;
	public ChaseSystem chaseSystem;

	public enum AIState
	{
		Patrolling,
		Chasing,
		Searching
	}

	[Header("AI States")]
	[SerializeField] private AIState currentState = AIState.Patrolling;
	public AIState CurrentState => currentState;
	public bool IsPatrolling => currentState == AIState.Patrolling;
	public bool IsChasing => currentState == AIState.Chasing;
	public bool IsSearching => currentState == AIState.Searching;

	[Header("Detection Settings")]
	public float detectionRadius = 15f;
	public float chaseRadius = 20f;
	public float loseRadius = 25f;
	public float fieldOfView = 100f;

	[Header("Movement Settings")]
	public float patrolSpeed = 2f;
	public float chaseSpeed = 5f;
	public float rotationSpeed = 5f;

	[Header("Pathfinding Settings")]
	public float repathInterval = 0.5f;
	public float nodeReachedDistance = 0.3f;

	[Header("Search Settings")]
	public float searchTime = 5f;
	public int searchPoints = 3;

	[Header("Soundboard Reaction")]
	public float hearingRange = 18f;
	[Range(0f, 1f)] public float minimumSoundboardInvestigateChance = 0.2f;
	[Range(0f, 1f)] public float maximumSoundboardInvestigateChance = 0.85f;
	public float awarenessBoostFromNoise = 4f;
	public float awarenessBoostDuration = 3f;

	[Header("Spawn Settings")]
	[Tooltip("Minimum distance from player when spawning")]
	public float minSpawnDistance = 20f;
	[Tooltip("Maximum distance from player when spawning")]
	public float maxSpawnDistance = 30f;
	[Tooltip("Maximum attempts to find valid spawn position")]
	public int maxSpawnAttempts = 50;

	[Header("Dread System")]
	[Tooltip("Chance per AI update to teleport closer when patrolling (0–1).")]
	public float dreadReappearChance = 0.01f;
	[Tooltip("Min and max distance from player when dread teleport triggers.")]
	public Vector2 dreadTeleportRange = new Vector2(8f, 15f);
	[Tooltip("Maximum chase speed increase over time.")]
	public float maxSpeedBoost = 2f;
	[Tooltip("Seconds until full speed boost is reached.")]
	public float boostTime = 10f;

	private Vector3 currentTarget;
	private Vector3 lastKnownPlayerPosition;
	private float lastDetectionTime;
	private ProceduralVillain proceduralVillain;
	private List<Vector3> currentPath = new List<Vector3>();
	private int currentPathIndex = 0;
	private float lastRepathTime = 0f;
	private bool isInitialized = false;
	private float dreadTimer = 0f;
	private float lastStateChangeTime = 0f;
	private bool isMovingToNextSearchPoint = false;
	
	[Header("Progressive Difficulty")]
	[Tooltip("Time in seconds before villain becomes fully active")]
	public float initialGracePeriod = 30f;
	[Tooltip("How quickly detection improves over time (0-1)")]
	public float difficultyRampSpeed = 0.1f;
	[Tooltip("Minimum detection radius during grace period")]
	public float minInitialDetectionRadius = 5f;
	[Tooltip("Maximum detection radius at full difficulty")]
	public float maxFinalDetectionRadius = 15f;
	[Tooltip("Minimum FOV during grace period")]
	public float minInitialFOV = 60f;
	[Tooltip("Maximum FOV at full difficulty")]
	public float maxFinalFOV = 100f;

	private float gameStartTime;
	private float currentDifficulty = 0f;
	private float currentDetectionRadius;
	private float currentFOV;
	private float awarenessBoostUntilTime = -999f;
	private float currentAwarenessBoost = 0f;
	
	

	void Start()
	{
		proceduralVillain = GetComponent<ProceduralVillain>();
		if (chaseSystem == null)
		{
			chaseSystem = FindObjectOfType<ChaseSystem>();
		}

		gameStartTime = Time.time;
		currentDifficulty = 0f;
		UpdateDifficultySettings();
		StartCoroutine(InitializeAI());
	}

	void OnEnable()
	{
		HorrorEvents.OnSoundboardPlayed += HandleSoundboardPlayed;
	}

	void OnDisable()
	{
		HorrorEvents.OnSoundboardPlayed -= HandleSoundboardPlayed;
	}

	IEnumerator InitializeAI()
	{
		yield return new WaitForSeconds(1f);

		if (mazeGenerator == null)
		{
			mazeGenerator = FindObjectOfType<MazeGenerator>();
			if (mazeGenerator == null)
			{
				Debug.LogError("MazeGenerator not found! VillainAI will not work properly.");
				yield break;
			}
		}

		yield return new WaitForSeconds(0.5f);

		if (player == null)
		{
			GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
			if (playerObj != null)
				player = playerObj.transform;
		}

		if (player == null)
		{
			Debug.LogError("Player not found! VillainAI cannot initialize.");
			yield break;
		}

		// Spawn at a valid position far from player
		Vector3 spawnPosition = FindValidSpawnPosition();
		if (spawnPosition != Vector3.zero)
		{
			transform.position = spawnPosition;
			Debug.Log($"Villain spawned at valid position: {spawnPosition}, distance from player: {Vector3.Distance(spawnPosition, player.position):F1}");
		}
		else
		{
			Debug.LogWarning("Could not find valid spawn position, using fallback");
			transform.position = new Vector3(
				mazeGenerator.width * mazeGenerator.cellSize * 0.8f,
				0f,
				mazeGenerator.height * mazeGenerator.cellSize * 0.8f
			);
		}

		SetRandomPatrolTarget();
		isInitialized = true;

		StartCoroutine(AIUpdateRoutine());
		StartCoroutine(DifficultyRampRoutine());
		TransitionToState(AIState.Patrolling, "Initialization complete");
		Debug.Log("VillainAI initialized successfully. Grace period active.");
	}

	IEnumerator DifficultyRampRoutine()
	{
		while (true)
		{
			if (!isInitialized)
			{
				yield return new WaitForSeconds(1f);
				continue;
			}

			float timeSinceStart = Time.time - gameStartTime;
        
			// Calculate difficulty based on time and player progress
			if (timeSinceStart < initialGracePeriod)
			{
				// During grace period, difficulty ramps up slowly
				currentDifficulty = Mathf.Clamp01(timeSinceStart / initialGracePeriod);
			}
			else
			{
				// After grace period, continue ramping up more slowly
				currentDifficulty = Mathf.Clamp01(1f + (timeSinceStart - initialGracePeriod) * difficultyRampSpeed);
			}

			UpdateDifficultySettings();
			yield return new WaitForSeconds(5f); // Update difficulty every 5 seconds
		}
	}
	
	void UpdateDifficultySettings()
	{
		// Scale detection radius based on difficulty
		currentDetectionRadius = Mathf.Lerp(minInitialDetectionRadius, maxFinalDetectionRadius, currentDifficulty);
    
		// Scale FOV based on difficulty
		currentFOV = Mathf.Lerp(minInitialFOV, maxFinalFOV, currentDifficulty);
    
		// Increase dread reappear chance over time
		dreadReappearChance = Mathf.Lerp(0.001f, 0.02f, currentDifficulty);
    
		// Increase patrol speed slightly over time
		patrolSpeed = Mathf.Lerp(1.5f, 2.5f, currentDifficulty);
	}
	
	Vector3 FindValidSpawnPosition()
	{
		List<Vector2Int> validCells = new List<Vector2Int>();

		// INCREASED minimum spawn distance
		float effectiveMinSpawnDistance = Mathf.Lerp(minSpawnDistance * 1.5f, minSpawnDistance, currentDifficulty);
		float effectiveMaxSpawnDistance = maxSpawnDistance * 1.2f;

		// Find all valid path cells in the maze
		for (int x = 1; x < mazeGenerator.width - 1; x++)
		{
			for (int y = 1; y < mazeGenerator.height - 1; y++)
			{
				if (GetMazeCellSafe(x, y) == 0) // Path cell
				{
					Vector3 worldPos = MazeCellToWorld(new Vector2Int(x, y));
					float distanceToPlayer = Vector3.Distance(worldPos, player.position);

					// Use increased distances during grace period
					if (distanceToPlayer >= effectiveMinSpawnDistance && distanceToPlayer <= effectiveMaxSpawnDistance)
					{
						validCells.Add(new Vector2Int(x, y));
					}
				}
			}
		}

		// If we have valid cells, choose one randomly
		if (validCells.Count > 0)
		{
			Vector2Int chosenCell = validCells[Random.Range(0, validCells.Count)];
			return MazeCellToWorld(chosenCell);
		}

		// Fallback: Find ANY valid cell far from player
		validCells.Clear();
		for (int x = 1; x < mazeGenerator.width - 1; x++)
		{
			for (int y = 1; y < mazeGenerator.height - 1; y++)
			{
				if (GetMazeCellSafe(x, y) == 0)
				{
					Vector3 worldPos = MazeCellToWorld(new Vector2Int(x, y));
					float distanceToPlayer = Vector3.Distance(worldPos, player.position);

					if (distanceToPlayer >= minSpawnDistance)
					{
						validCells.Add(new Vector2Int(x, y));
					}
				}
			}
		}

		if (validCells.Count > 0)
		{
			Vector2Int chosenCell = validCells[Random.Range(0, validCells.Count)];
			return MazeCellToWorld(chosenCell);
		}

		// Last resort: any valid cell
		for (int x = 1; x < mazeGenerator.width - 1; x++)
		{
			for (int y = 1; y < mazeGenerator.height - 1; y++)
			{
				if (GetMazeCellSafe(x, y) == 0)
				{
					return MazeCellToWorld(new Vector2Int(x, y));
				}
			}
		}

		return Vector3.zero;
	}

	Vector3 FindValidDreadTeleportPosition()
	{
		List<Vector2Int> validCells = new List<Vector2Int>();

		// Find all valid cells within dread teleport range
		for (int x = 1; x < mazeGenerator.width - 1; x++)
		{
			for (int y = 1; y < mazeGenerator.height - 1; y++)
			{
				if (GetMazeCellSafe(x, y) == 0) // Path cell
				{
					Vector3 worldPos = MazeCellToWorld(new Vector2Int(x, y));
					float distanceToPlayer = Vector3.Distance(worldPos, player.position);

					// Check if within dread teleport range
					if (distanceToPlayer >= dreadTeleportRange.x && distanceToPlayer <= dreadTeleportRange.y)
					{
						validCells.Add(new Vector2Int(x, y));
					}
				}
			}
		}

		if (validCells.Count > 0)
		{
			Vector2Int chosenCell = validCells[Random.Range(0, validCells.Count)];
			return MazeCellToWorld(chosenCell);
		}

		// Fallback: don't teleport
		return transform.position;
	}

	void Update()
	{
		if (!isInitialized) return;
		if (Time.time >= awarenessBoostUntilTime)
		{
			currentAwarenessBoost = 0f;
		}

		HandleMovement();
	}

	IEnumerator AIUpdateRoutine()
	{
		while (true)
		{
			if (!isInitialized)
			{
				yield return new WaitForSeconds(1f);
				continue;
			}

			if (player == null)
			{
				GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
				if (playerObj != null)
					player = playerObj.transform;
				yield return new WaitForSeconds(1f);
				continue;
			}

			float distanceToPlayer = Vector3.Distance(transform.position, player.position);

			// Dread System: Teleport to valid position near player
			if (IsPatrolling && Random.value < dreadReappearChance)
			{
				Vector3 dreadPosition = FindValidDreadTeleportPosition();
				if (dreadPosition != transform.position)
				{
					transform.position = dreadPosition;
					FindPathTo(player.position);
					Debug.Log($"Villain reappeared nearby at distance: {Vector3.Distance(dreadPosition, player.position):F1}");
				}
			}

			// State Logic
			if (IsChasing)
			{
				HandleChaseState(distanceToPlayer);
			}
			else if (IsSearching)
			{
				HandleSearchState(distanceToPlayer);
			}
			else
			{
				HandlePatrolState(distanceToPlayer);
			}

			// Check for stuck behavior
			CheckForStuckBehavior();

			yield return new WaitForSeconds(0.2f);
		}
	}

	void HandleMovement()
	{
		if (currentPath == null || currentPath.Count == 0 || currentPathIndex >= currentPath.Count)
		{
			if (IsPatrolling && currentPath.Count == 0)
			{
				SetRandomPatrolTarget();
			}
			else if (IsSearching && currentPath.Count == 0)
			{
				Vector3 searchPoint = GetNextSearchPoint();
				FindPathTo(searchPoint);
			}
			return;
		}

		Vector3 targetPosition = currentPath[currentPathIndex];
		targetPosition.y = 0f;

		Vector3 direction = (targetPosition - transform.position).normalized;

		// Calculate speed with dread boost
		float currentSpeed = IsChasing ? chaseSpeed : patrolSpeed;
		if (IsChasing)
		{
			float chaseDuration = Time.time - lastDetectionTime;
			float boost = Mathf.Min(maxSpeedBoost, (chaseDuration / boostTime) * maxSpeedBoost);
			currentSpeed += boost;
		}

		Vector3 newPosition = transform.position + direction * currentSpeed * Time.deltaTime;
		newPosition.y = 0f;
		transform.position = newPosition;

		if (direction != Vector3.zero)
		{
			Vector3 horizontalDirection = new Vector3(direction.x, 0f, direction.z).normalized;
			if (horizontalDirection != Vector3.zero)
			{
				Quaternion targetRotation = Quaternion.LookRotation(horizontalDirection);
				transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
			}
		}

		Vector2 currentXZ = new Vector2(transform.position.x, transform.position.z);
		Vector2 targetXZ = new Vector2(targetPosition.x, targetPosition.z);
		if (Vector2.Distance(currentXZ, targetXZ) <= nodeReachedDistance)
		{
			currentPathIndex++;
			if (currentPathIndex >= currentPath.Count)
			{
				if (IsPatrolling)
				{
					SetRandomPatrolTarget();
				}
				else if (IsSearching)
				{
					StartCoroutine(GetNextSearchPointAfterDelay());
				}
			}
		}

		if (IsChasing && Time.time - lastRepathTime > repathInterval)
		{
			FindPathTo(player.position);
			lastRepathTime = Time.time;
		}
	}

	IEnumerator GetNextSearchPointAfterDelay()
	{
		yield return new WaitForSeconds(1f);
		if (IsSearching)
		{
			Vector3 searchPoint = GetNextSearchPoint();
			FindPathTo(searchPoint);
		}
	}

	void HandlePatrolState(float distanceToPlayer)
	{
		// During grace period, reduce detection chances
		if (currentDifficulty < 0.3f)
		{
			// Only detect if very close during early game
			if (distanceToPlayer <= currentDetectionRadius * 0.5f && CanSeePlayer())
			{
				RequestChaseStart("Player detected during patrol/search");
			}
		}
		else
		{
			// Normal detection after grace period
			if (CanSeePlayer() || distanceToPlayer <= currentDetectionRadius * 1.5f)
			{
				RequestChaseStart("Player detected during patrol/search");
			}
		}
	}

	void HandleChaseState(float distanceToPlayer)
	{
		if (proceduralVillain != null)
		{
			proceduralVillain.player = player;
		}

		if (CanSeePlayer())
		{
			lastKnownPlayerPosition = player.position;
			lastDetectionTime = Time.time;
		}

		bool playerOutOfRange = distanceToPlayer > loseRadius;
		bool cannotSeePlayer = !CanSeePlayer();
		bool lostPlayerForTooLong = Time.time - lastDetectionTime > 3f;

		if (playerOutOfRange || (cannotSeePlayer && lostPlayerForTooLong))
		{
			StartSearch();
			return;
		}
	}

	void HandleSearchState(float distanceToPlayer)
	{
		if (CanSeePlayer() || distanceToPlayer <= GetEffectiveDetectionRadius())
		{
			RequestChaseStart("Player detected during patrol/search");
			return;
		}

		bool searchTimeExpired = Time.time - lastDetectionTime > searchTime;
		bool noSearchPointsLeft = currentPathIndex >= currentPath.Count && !isMovingToNextSearchPoint;

		if (searchTimeExpired || noSearchPointsLeft)
		{
			ReturnToPatrol();
		}
	}

	void CheckForStuckBehavior()
	{
		if (Time.time - lastStateChangeTime > 10f && currentPath.Count == 0)
		{
			Debug.LogWarning("AI appears stuck, resetting behavior...");

			if (IsChasing)
			{
				StartSearch();
			}
			else if (IsSearching)
			{
				ReturnToPatrol();
			}
			else
			{
				SetRandomPatrolTarget();
			}
		}
	}

	IEnumerator SearchRoutine()
	{
		for (int i = 0; i < searchPoints; i++)
		{
			isMovingToNextSearchPoint = true;

			yield return new WaitUntil(() => currentPathIndex >= currentPath.Count || !IsSearching);

			if (!IsSearching)
			{
				isMovingToNextSearchPoint = false;
				yield break;
			}

			Vector3 nextPoint = GetNextSearchPoint();
			FindPathTo(nextPoint);

			isMovingToNextSearchPoint = false;
			yield return new WaitForSeconds(1f);
		}

		ReturnToPatrol();
	}

	public bool CanSeePlayer()
	{
		if (player == null) return false;

		float distanceToPlayer = Vector3.Distance(transform.position, player.position);

		// Use current detection radius instead of fixed one
		if (distanceToPlayer > GetEffectiveDetectionRadius())
		{
			return false;
		}

		Vector3 directionToPlayer = (player.position - transform.position).normalized;
		float angleToPlayer = Vector3.Angle(transform.forward, directionToPlayer);

		// Use current FOV instead of fixed one
		if (angleToPlayer > currentFOV / 2 && distanceToPlayer > 3f)
		{
			return false;
		}

		if (HasLineOfSight(transform.position, player.position))
		{
			return true;
		}

		return false;
	}

	void HandleSoundboardPlayed(string soundTag, float loudness)
	{
		if (!isInitialized || player == null || IsChasing)
		{
			return;
		}

		float distanceToNoise = Vector3.Distance(transform.position, player.position);
		float effectiveHearingRange = Mathf.Max(0.01f, hearingRange * Mathf.Lerp(0.65f, 1.2f, Mathf.Clamp01(loudness)));
		if (distanceToNoise > effectiveHearingRange)
		{
			return;
		}

		currentAwarenessBoost = Mathf.Max(currentAwarenessBoost, awarenessBoostFromNoise * Mathf.Clamp01(loudness));
		awarenessBoostUntilTime = Time.time + awarenessBoostDuration;

		float proximityFactor = 1f - Mathf.Clamp01(distanceToNoise / effectiveHearingRange);
		float difficultyFactor = Mathf.Lerp(0.8f, 1.15f, currentDifficulty);
		float investigateChance = Mathf.Clamp01(Mathf.Lerp(minimumSoundboardInvestigateChance, maximumSoundboardInvestigateChance, loudness) * (0.45f + proximityFactor) * difficultyFactor);
		if (Random.value > investigateChance)
		{
			return;
		}

		lastKnownPlayerPosition = player.position;
		lastDetectionTime = Time.time;
		BeginSearch(lastKnownPlayerPosition, $"Investigating soundboard noise: {soundTag}");
	}

	bool HasLineOfSight(Vector3 from, Vector3 to)
	{
		int layerMask = ~(1 << LayerMask.NameToLayer("IgnoreRaycast"));

		Vector3 fromAdjusted = from + Vector3.up * 1f;
		Vector3 toAdjusted = to + Vector3.up * 1f;

		Vector3 direction = (toAdjusted - fromAdjusted).normalized;
		float distance = Vector3.Distance(fromAdjusted, toAdjusted);

		Debug.DrawRay(fromAdjusted, direction * distance, Color.yellow, 0.1f);

		RaycastHit[] hits = Physics.RaycastAll(fromAdjusted, direction, distance, layerMask);
		System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

		foreach (RaycastHit hit in hits)
		{
			if (hit.transform == transform || hit.transform.IsChildOf(transform))
				continue;

			if (hit.transform == player || hit.transform.IsChildOf(player))
			{
				return true;
			}

			return false;
		}

		return true;
	}

	void SetRandomPatrolTarget()
	{
		if (!isInitialized || mazeGenerator == null) return;

		Vector3 randomTarget = GetRandomValidPosition();
		if (randomTarget != Vector3.zero)
		{
			FindPathTo(randomTarget);
		}
	}

	Vector3 GetRandomValidPosition()
	{
		if (!isInitialized || mazeGenerator == null)
		{
			return transform.position + Random.insideUnitSphere * 5f;
		}

		List<Vector2Int> validPositions = new List<Vector2Int>();

		for (int x = 1; x < mazeGenerator.width - 1; x++)
		{
			for (int y = 1; y < mazeGenerator.height - 1; y++)
			{
				if (GetMazeCellSafe(x, y) == 0)
				{
					validPositions.Add(new Vector2Int(x, y));
				}
			}
		}

		if (validPositions.Count > 0)
		{
			Vector2Int randomCell = validPositions[Random.Range(0, validPositions.Count)];
			return MazeCellToWorld(randomCell);
		}

		return transform.position + new Vector3(Random.Range(-3f, 3f), 0, Random.Range(-3f, 3f));
	}

	void FindPathTo(Vector3 target)
	{
		if (!isInitialized || mazeGenerator == null)
		{
			currentPath = new List<Vector3> { new Vector3(target.x, 0f, target.z) };
			currentPathIndex = 0;
			return;
		}

		Vector2Int startCell = WorldToMazeCell(transform.position);
		Vector2Int targetCell = WorldToMazeCell(target);

		if (!IsValidCell(targetCell))
		{
			targetCell = FindNearestValidCell(targetCell);
		}

		currentPath = FindPathAStar(startCell, targetCell);
		currentPathIndex = 0;

		for (int i = 0; i < currentPath.Count; i++)
		{
			currentPath[i] = MazeCellToWorld(new Vector2Int((int)currentPath[i].x, (int)currentPath[i].z));
		}

		if (currentPath.Count > 0)
		{
			currentPath[currentPath.Count - 1] = new Vector3(target.x, 0f, target.z);
		}
	}

	Vector2Int FindNearestValidCell(Vector2Int startCell)
	{
		if (!isInitialized) return startCell;

		Queue<Vector2Int> queue = new Queue<Vector2Int>();
		HashSet<Vector2Int> visited = new HashSet<Vector2Int>();

		queue.Enqueue(startCell);
		visited.Add(startCell);

		Vector2Int[] directions = {
			new Vector2Int(0, 1), new Vector2Int(1, 0),
			new Vector2Int(0, -1), new Vector2Int(-1, 0)
		};

		while (queue.Count > 0)
		{
			Vector2Int current = queue.Dequeue();

			if (IsValidCell(current))
			{
				return current;
			}

			foreach (Vector2Int dir in directions)
			{
				Vector2Int next = current + dir;
				if (!visited.Contains(next) &&
					next.x >= 0 && next.x < mazeGenerator.width &&
					next.y >= 0 && next.y < mazeGenerator.height)
				{
					queue.Enqueue(next);
					visited.Add(next);
				}
			}
		}

		return startCell;
	}

	List<Vector3> FindPathAStar(Vector2Int start, Vector2Int target)
	{
		List<Vector3> path = new List<Vector3>();

		if (!IsValidCell(start) || !IsValidCell(target))
		{
			return path;
		}

		Dictionary<Vector2Int, Vector2Int> cameFrom = new Dictionary<Vector2Int, Vector2Int>();
		Queue<Vector2Int> frontier = new Queue<Vector2Int>();
		HashSet<Vector2Int> visited = new HashSet<Vector2Int>();

		frontier.Enqueue(start);
		visited.Add(start);
		cameFrom[start] = start;

		Vector2Int[] directions = {
			new Vector2Int(0, 1), new Vector2Int(1, 0),
			new Vector2Int(0, -1), new Vector2Int(-1, 0)
		};

		while (frontier.Count > 0)
		{
			Vector2Int current = frontier.Dequeue();

			if (current == target)
			{
				path = ReconstructPath(cameFrom, start, target);
				break;
			}

			foreach (Vector2Int dir in directions)
			{
				Vector2Int next = current + dir;

				if (IsValidCell(next) && !visited.Contains(next))
				{
					frontier.Enqueue(next);
					visited.Add(next);
					cameFrom[next] = current;
				}
			}
		}

		return path;
	}

	List<Vector3> ReconstructPath(Dictionary<Vector2Int, Vector2Int> cameFrom, Vector2Int start, Vector2Int target)
	{
		List<Vector3> path = new List<Vector3>();
		Vector2Int current = target;

		while (current != start)
		{
			path.Add(new Vector3(current.x, 0, current.y));
			current = cameFrom[current];

			if (path.Count > 1000)
			{
				Debug.LogError("Path reconstruction too long - possible loop");
				break;
			}
		}

		path.Reverse();
		return path;
	}

	bool IsValidCell(Vector2Int cell)
	{
		if (!isInitialized || mazeGenerator == null) return false;

		return cell.x >= 0 && cell.x < mazeGenerator.width &&
			cell.y >= 0 && cell.y < mazeGenerator.height &&
			GetMazeCellSafe(cell.x, cell.y) == 0;
	}

	int GetMazeCellSafe(int x, int y)
	{
		if (!isInitialized || mazeGenerator == null) return 1;

		try
		{
			return mazeGenerator.GetMazeCell(x, y);
		}
			catch (System.Exception e)
			{
				Debug.LogWarning($"Error getting maze cell at ({x}, {y}): {e.Message}");
				return 1;
			}
	}

	Vector2Int WorldToMazeCell(Vector3 worldPos)
	{
		if (!isInitialized || mazeGenerator == null) return Vector2Int.zero;

		return new Vector2Int(
			Mathf.FloorToInt(worldPos.x / mazeGenerator.cellSize),
			Mathf.FloorToInt(worldPos.z / mazeGenerator.cellSize)
		);
	}

	Vector3 MazeCellToWorld(Vector2Int cell)
	{
		if (!isInitialized || mazeGenerator == null) return Vector3.zero;

		return new Vector3(
			cell.x * mazeGenerator.cellSize + mazeGenerator.cellSize / 2,
			0f,
			cell.y * mazeGenerator.cellSize + mazeGenerator.cellSize / 2
		);
	}

	void StartChase()
	{
		BeginDirectedChase("Fallback", "Player detected");
	}

	void RequestChaseStart(string reason)
	{
		if (chaseSystem != null)
		{
			if (chaseSystem.RequestChase(reason))
			{
				return;
			}

			if (chaseSystem.IsChaseActive || Time.time < chaseSystem.NextChaseAllowedTime)
			{
				lastKnownPlayerPosition = player != null ? player.position : lastKnownPlayerPosition;
				lastDetectionTime = Time.time;
				return;
			}
		}

		StartChase();
	}

	void StartSearch()
	{
		BeginSearch(GetNextSearchPoint(), "Lost visual on player");
	}

	Vector3 GetNextSearchPoint()
	{
		float angle = Random.Range(0f, 360f);
		float distance = Random.Range(2f, 5f);

		Vector3 offset = new Vector3(
			Mathf.Cos(angle * Mathf.Deg2Rad) * distance,
			0f,
			Mathf.Sin(angle * Mathf.Deg2Rad) * distance
		);

		Vector3 searchPoint = lastKnownPlayerPosition + offset;
		searchPoint.y = 0f;

		Vector2Int searchCell = WorldToMazeCell(searchPoint);
		if (IsValidCell(searchCell))
		{
			return MazeCellToWorld(searchCell);
		}

		return lastKnownPlayerPosition;
	}

	void ReturnToPatrol()
	{
		TransitionToState(AIState.Patrolling, "Search expired");
		SetRandomPatrolTarget();
		Debug.Log("Returned to patrol mode.");
	}

	void BeginSearch(Vector3 searchPoint, string reason)
	{
		TransitionToState(AIState.Searching, reason);
		StopCoroutine("SearchRoutine");
		isMovingToNextSearchPoint = false;
		FindPathTo(searchPoint);
		StartCoroutine(SearchRoutine());
		Debug.Log("Started searching for player at point: " + searchPoint);
	}

	public void ForceChase()
	{
		RequestChaseStart("Player detected during patrol/search");
	}

	public float TimeSinceLastPlayerDetection => Time.time - lastDetectionTime;

	public void BeginDirectedChase(string chasePatternName, string reason)
	{
		TransitionToState(AIState.Chasing, $"{reason} ({chasePatternName})");
		StopCoroutine("SearchRoutine");
		lastDetectionTime = Time.time;
		if (player != null)
		{
			lastKnownPlayerPosition = player.position;
			FindPathTo(player.position);
		}
		Debug.Log($"Started {chasePatternName} chase. Reason: {reason}");
	}

	public void ForceSearchAtLastKnownPosition(string reason)
	{
		BeginSearch(lastKnownPlayerPosition, reason);
	}

	public string GetAIState()
	{
		return currentState.ToString();
	}

	void TransitionToState(AIState newState, string reason)
	{
		if (currentState == newState)
		{
			return;
		}

		AIState previousState = currentState;
		currentState = newState;
		lastStateChangeTime = Time.time;

		if (previousState == AIState.Searching && newState != AIState.Searching)
		{
			StopCoroutine("SearchRoutine");
			isMovingToNextSearchPoint = false;
		}

		if (previousState != AIState.Chasing && newState == AIState.Chasing)
		{
			HorrorEvents.RaiseChaseStarted();
		}
		else if (previousState == AIState.Chasing && newState != AIState.Chasing)
		{
			HorrorEvents.RaiseChaseEnded();
		}

		Debug.Log($"VillainAI transition: {previousState} -> {newState}. Reason: {reason}");
	}

	float GetEffectiveDetectionRadius()
	{
		return currentDetectionRadius + currentAwarenessBoost;
	}

	void OnDrawGizmosSelected()
	{
		if (!isInitialized) return;

		Gizmos.color = Color.yellow;
		Gizmos.DrawWireSphere(transform.position, detectionRadius);

		Gizmos.color = Color.red;
		Gizmos.DrawWireSphere(transform.position, chaseRadius);

		Gizmos.color = Color.blue;
		Vector3 leftBound = Quaternion.Euler(0, -fieldOfView / 2, 0) * transform.forward * detectionRadius;
		Vector3 rightBound = Quaternion.Euler(0, fieldOfView / 2, 0) * transform.forward * detectionRadius;
		Gizmos.DrawRay(transform.position, leftBound);
		Gizmos.DrawRay(transform.position, rightBound);

		Gizmos.color = IsChasing ? Color.red : Color.green;
		for (int i = 0; i < currentPath.Count - 1; i++)
		{
			Gizmos.DrawLine(currentPath[i], currentPath[i + 1]);
			Gizmos.DrawWireSphere(currentPath[i], 0.2f);
		}

		if (currentPath.Count > 0)
		{
			Gizmos.DrawWireSphere(currentPath[currentPath.Count - 1], 0.3f);
		}

		if (currentPathIndex < currentPath.Count)
		{
			Gizmos.color = Color.magenta;
			Gizmos.DrawWireSphere(currentPath[currentPathIndex], 0.4f);
		}

		// Draw spawn range visualization when selected
		if (player != null)
		{
			Gizmos.color = new Color(0, 1, 0, 0.2f);
			Gizmos.DrawWireSphere(player.position, minSpawnDistance);
            
			Gizmos.color = new Color(1, 1, 0, 0.2f);
			Gizmos.DrawWireSphere(player.position, maxSpawnDistance);
		}
	}
}
