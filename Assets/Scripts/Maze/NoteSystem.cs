using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;
using System.Linq;
using System.Collections;

public class NoteSystem : MonoBehaviour
{
	[System.Serializable]
	public class Note
	{
		public string noteID;
		[TextArea(3, 10)]
		public string noteText;
		[Min(1)] public int tier = 1;
		public float triggerRadius = 2f;
		public bool readOnce = true;
	}

	public class RuntimeNote
	{
		public string Id;
		public string Text;
		public int Tier;
		public float TriggerRadius;
		public bool ReadOnce;
		public List<string> Tags = new List<string>();
		public NoteData SourceData;
	}

	[Header("Legacy Notes (kept for backwards compatibility)")]
	public List<Note> notes = new List<Note>();

	[Header("Note Data")]
	public List<NoteData> allNotes = new List<NoteData>();
	public int notesRequiredToFinish = 6;
	public int simultaneousWorldNotes = 3;

	[Header("Spawn Settings")]
	public GameObject notePrefab;
	public float minDistanceBetweenNotes = 15f;
	public float minDistanceFromStart = 10f;
	public int maxSpawnAttempts = 60;

	[Header("References")]
	public MazeGenerator mazeGenerator;
	public Transform player;
	public RunGameState gameState;

	[Header("UI")]
	public Canvas noteCanvas;
	public TextMeshProUGUI noteText;
	public Image noteBackground;
	public Button continueButton;
	public TextMeshProUGUI continueButtonText;

	[Header("HUD Counter")]
	public TextMeshProUGUI notesCounterText;
	public bool showCounter = true;
	public Vector2 counterPosition = new Vector2(20, 20);

	[Header("Visual Settings")]
	public Color backgroundColor = new Color(0.9f, 0.85f, 0.7f, 0.95f);
	public Color textColor = new Color(0.1f, 0.1f, 0.1f, 1f);
	public Font noteFont;
	public int fontSize = 24;
	public float fadeSpeed = 5f;

	private readonly List<GameObject> spawnedNotes = new List<GameObject>();
	private readonly Dictionary<GameObject, RuntimeNote> noteMap = new Dictionary<GameObject, RuntimeNote>();
	private readonly HashSet<string> availableNoteIds = new HashSet<string>();
	private readonly List<RuntimeNote> availableNotes = new List<RuntimeNote>();
	private readonly List<RuntimeNote> collectedNotes = new List<RuntimeNote>();
	private readonly HashSet<string> spawnedOrCollectedIds = new HashSet<string>();

	private List<RuntimeNote> runtimeAllNotes = new List<RuntimeNote>();
	private Queue<RuntimeNote> displayQueue = new Queue<RuntimeNote>();
	private bool isDisplayingSequence = false;
	private Coroutine displayCoroutine;
	private bool isReadingNote = false;
	private float savedTimeScale = 1f;
	private CanvasGroup canvasGroup;
	private int totalSpawnedThisRun = 0;

	void Start()
	{
		if (mazeGenerator == null)
			mazeGenerator = FindObjectOfType<MazeGenerator>();

		if (player == null)
		{
			GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
			if (playerObj != null)
				player = playerObj.transform;
		}

		if (gameState == null)
			gameState = FindObjectOfType<RunGameState>();
		SubscribeToGameState();

		BuildRuntimeLibrary();
		SetupUI();
		InitializeAvailableNotes();
		Invoke(nameof(SpawnInitialNotes), 2f);
	}

	void OnEnable()
	{
		SubscribeToGameState();
	}

	void OnDisable()
	{
		if (gameState != null)
			gameState.OnStateChanged -= HandleGameStateChanged;
	}

	void SubscribeToGameState()
	{
		if (gameState == null) return;
		gameState.OnStateChanged -= HandleGameStateChanged;
		gameState.OnStateChanged += HandleGameStateChanged;
	}

	void BuildRuntimeLibrary()
	{
		runtimeAllNotes = new List<RuntimeNote>();

		for (int i = 0; i < allNotes.Count; i++)
		{
			NoteData data = allNotes[i];
			if (data == null || string.IsNullOrEmpty(data.id)) continue;

			runtimeAllNotes.Add(new RuntimeNote
			{
				Id = data.id,
				Text = data.content,
				Tier = Mathf.Max(1, data.tier),
				ReadOnce = true,
				TriggerRadius = 2f,
				Tags = data.tags != null ? new List<string>(data.tags) : new List<string>(),
				SourceData = data
			});
		}

		// Fallback: convert legacy inline notes when ScriptableObject library is empty.
		if (runtimeAllNotes.Count == 0)
		{
			for (int i = 0; i < notes.Count; i++)
			{
				Note legacy = notes[i];
				if (legacy == null || string.IsNullOrEmpty(legacy.noteID)) continue;
				runtimeAllNotes.Add(new RuntimeNote
				{
					Id = legacy.noteID,
					Text = legacy.noteText,
					Tier = Mathf.Max(1, legacy.tier),
					TriggerRadius = legacy.triggerRadius,
					ReadOnce = legacy.readOnce
				});
			}
		}
	}

	void InitializeAvailableNotes()
	{
		availableNotes.Clear();
		availableNoteIds.Clear();

		for (int i = 0; i < runtimeAllNotes.Count; i++)
		{
			RuntimeNote note = runtimeAllNotes[i];
			if (note.SourceData == null)
			{
				AddToAvailable(note); // legacy fallback notes are treated as base notes.
			}
			else if (note.SourceData.isBaseNote)
			{
				AddToAvailable(note);
			}
		}
	}

	void SpawnInitialNotes()
	{
		if (mazeGenerator == null || player == null)
		{
			Debug.LogError("Cannot spawn notes: missing references");
			return;
		}

		EvaluateUnlocks();
		EnsureSpawnBudget();
		UpdateCounter();
	}

	void HandleGameStateChanged()
	{
		EvaluateUnlocks();
	}

	void EvaluateUnlocks()
	{
		for (int i = 0; i < runtimeAllNotes.Count; i++)
		{
			RuntimeNote note = runtimeAllNotes[i];
			if (availableNoteIds.Contains(note.Id))
				continue;

			if (note.SourceData == null)
				continue;

			if (AreUnlockConditionsMet(note.SourceData))
			{
				AddToAvailable(note);
			}
		}
	}

	bool AreUnlockConditionsMet(NoteData data)
	{
		if (data == null) return false;
		if (data.unlockConditions == null || data.unlockConditions.Count == 0) return data.isBaseNote;
		if (gameState == null) return false;

		for (int i = 0; i < data.unlockConditions.Count; i++)
		{
			UnlockCondition condition = data.unlockConditions[i];
			if (condition != null && !condition.IsMet(gameState))
				return false;
		}
		return true;
	}

	void AddToAvailable(RuntimeNote note)
	{
		if (note == null || availableNoteIds.Contains(note.Id)) return;
		availableNotes.Add(note);
		availableNoteIds.Add(note.Id);
	}

	void EnsureSpawnBudget()
	{
		while (spawnedNotes.Count < simultaneousWorldNotes && totalSpawnedThisRun < notesRequiredToFinish)
		{
			int spawnIndex = totalSpawnedThisRun;
			int targetTier = GetTargetTierForIndex(spawnIndex);
			RuntimeNote picked = PickNoteForTier(targetTier);
			if (picked == null)
			{
				// Fallback: any unlocked and unspawned note.
				picked = availableNotes.FirstOrDefault(n => !spawnedOrCollectedIds.Contains(n.Id));
			}

			if (picked == null)
			{
				Debug.LogWarning("NoteSystem could not find enough notes to satisfy spawn budget.");
				break;
			}

			Vector3 spawnPos = FindValidNotePositionForTier(targetTier);
			if (spawnPos == Vector3.zero)
			{
				spawnPos = FindValidNotePositionForTier(picked.Tier);
			}

			if (spawnPos == Vector3.zero)
			{
				Debug.LogWarning("NoteSystem could not find valid spawn position for note: " + picked.Id);
				break;
			}

			SpawnNote(spawnPos, picked);
			totalSpawnedThisRun++;
			spawnedOrCollectedIds.Add(picked.Id);
		}
	}

	int GetTargetTierForIndex(int index)
	{
		int maxTier = Mathf.Max(1, runtimeAllNotes.Count == 0 ? 1 : runtimeAllNotes.Max(n => n.Tier));
		if (notesRequiredToFinish <= 1) return 1;
		float t = Mathf.Clamp01(index / (float)(notesRequiredToFinish - 1));
		return Mathf.Clamp(Mathf.FloorToInt(t * maxTier) + 1, 1, maxTier);
	}

	RuntimeNote PickNoteForTier(int tier)
	{
		List<RuntimeNote> candidates = availableNotes
			.Where(n => n.Tier == tier && !spawnedOrCollectedIds.Contains(n.Id))
			.ToList();
		if (candidates.Count == 0) return null;
		return candidates[Random.Range(0, candidates.Count)];
	}

	Vector3 FindValidNotePositionForTier(int tier)
	{
		Vector3 startPos = mazeGenerator.GetStartPosition();
		Vector3 exitPos = mazeGenerator.GetExitPosition();
		int maxTier = Mathf.Max(1, runtimeAllNotes.Count == 0 ? 1 : runtimeAllNotes.Max(n => n.Tier));

		for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
		{
			int x = Random.Range(1, mazeGenerator.width - 1);
			int y = Random.Range(1, mazeGenerator.height - 1);
			if (mazeGenerator.GetMazeCell(x, y) != 0) continue;

			Vector3 worldPos = new Vector3(
				x * mazeGenerator.cellSize + mazeGenerator.cellSize / 2,
				0.1f,
				y * mazeGenerator.cellSize + mazeGenerator.cellSize / 2
			);

			if (Vector3.Distance(worldPos, startPos) < minDistanceFromStart) continue;
			if (GetZoneTierForPosition(worldPos, startPos, exitPos, maxTier) != tier) continue;
			if (IsTooCloseToExistingNotes(worldPos)) continue;
			return worldPos;
		}

		return Vector3.zero;
	}

	int GetZoneTierForPosition(Vector3 worldPos, Vector3 startPos, Vector3 exitPos, int maxTier)
	{
		float distanceFromStart = Vector3.Distance(worldPos, startPos);
		float distanceFromExit = Vector3.Distance(worldPos, exitPos);
		float total = Mathf.Max(0.001f, distanceFromStart + distanceFromExit);
		float progressToExit = distanceFromStart / total; // near start = 0, near exit = 1.
		return Mathf.Clamp(Mathf.FloorToInt(progressToExit * maxTier) + 1, 1, maxTier);
	}

	bool IsTooCloseToExistingNotes(Vector3 worldPos)
	{
		for (int i = 0; i < spawnedNotes.Count; i++)
		{
			if (spawnedNotes[i] != null && Vector3.Distance(worldPos, spawnedNotes[i].transform.position) < minDistanceBetweenNotes)
				return true;
		}
		return false;
	}

	void SpawnNote(Vector3 position, RuntimeNote note)
	{
		GameObject noteObject;

		if (notePrefab != null)
		{
			noteObject = Instantiate(notePrefab, position, Quaternion.Euler(90, Random.Range(0f, 360f), 0));
			noteObject.name = "Note_" + note.Id;

			Collider collider = noteObject.GetComponent<Collider>();
			if (collider == null)
			{
				SphereCollider sphereCollider = noteObject.AddComponent<SphereCollider>();
				sphereCollider.isTrigger = true;
				sphereCollider.radius = note.TriggerRadius;
			}
		}
		else
		{
			noteObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
			noteObject.transform.position = position;
			noteObject.transform.rotation = Quaternion.Euler(90, Random.Range(0f, 360f), 0);
			noteObject.transform.localScale = new Vector3(0.4f, 0.6f, 1f);
			noteObject.name = "Note_" + note.Id;

			Material paperMaterial = new Material(Shader.Find("Standard"));
			paperMaterial.color = new Color(0.9f, 0.85f, 0.7f);
			paperMaterial.SetFloat("_Metallic", 0f);
			paperMaterial.SetFloat("_Glossiness", 0.1f);
			noteObject.GetComponent<Renderer>().material = paperMaterial;

			SphereCollider triggerCollider = noteObject.AddComponent<SphereCollider>();
			triggerCollider.isTrigger = true;
			triggerCollider.radius = note.TriggerRadius;
			noteObject.transform.position += Vector3.up * 0.05f;
		}

		NoteTrigger noteTrigger = noteObject.AddComponent<NoteTrigger>();
		noteTrigger.noteSystem = this;
		noteTrigger.runtimeNote = note;

		noteObject.transform.SetParent(transform);
		spawnedNotes.Add(noteObject);
		noteMap[noteObject] = note;
	}

	void SetupUI()
	{
		if (noteCanvas == null)
		{
			Debug.LogError("Note Canvas not assigned!");
			return;
		}

		canvasGroup = noteCanvas.GetComponent<CanvasGroup>();
		if (canvasGroup == null)
			canvasGroup = noteCanvas.gameObject.AddComponent<CanvasGroup>();

		canvasGroup.alpha = 0f;
		noteCanvas.gameObject.SetActive(false);

		if (noteBackground != null)
			noteBackground.color = backgroundColor;

		if (noteText != null)
		{
			noteText.color = textColor;
			noteText.fontSize = fontSize;
		}

		if (continueButtonText != null)
			continueButtonText.text = "Press [SPACE] to continue";

		if (continueButton != null)
			continueButton.onClick.AddListener(CloseNote);

		UpdateCounter();
		if (notesCounterText != null)
			notesCounterText.gameObject.SetActive(showCounter);
	}

	void Update()
	{
		if (player == null) return;

		if (canvasGroup != null)
		{
			float targetAlpha = isReadingNote ? 1f : 0f;
			canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, targetAlpha, Time.unscaledDeltaTime * fadeSpeed);
		}

		if (isReadingNote && Input.GetKeyDown(KeyCode.Space))
		{
			CloseNote();
		}

		if (isReadingNote || isDisplayingSequence) return;
		CheckNearbyNotesFallback();
	}

	void CheckNearbyNotesFallback()
	{
		for (int i = 0; i < spawnedNotes.Count; i++)
		{
			GameObject noteObject = spawnedNotes[i];
			if (noteObject == null || !noteObject.activeInHierarchy || !noteMap.ContainsKey(noteObject)) continue;

			RuntimeNote note = noteMap[noteObject];
			float distance = Vector3.Distance(player.position, noteObject.transform.position);
			if (distance <= note.TriggerRadius)
			{
				CollectNote(noteObject, note);
				break;
			}
		}
	}

	public void CollectNote(GameObject noteObject, RuntimeNote note)
	{
		if (noteObject == null || note == null || !noteObject.activeSelf)
			return;

		noteObject.SetActive(false);
		spawnedNotes.Remove(noteObject);

		if (!collectedNotes.Any(n => n.Id == note.Id))
		{
			collectedNotes.Add(note);
			if (gameState != null && note.SourceData != null)
				gameState.RegisterCollectedNote(note.SourceData);

			displayQueue.Enqueue(note);
			if (!isDisplayingSequence && displayCoroutine == null)
				displayCoroutine = StartCoroutine(DisplayNoteSequence());

			UpdateCounter();
			EvaluateUnlocks();
			EnsureSpawnBudget();
		}
	}

	IEnumerator DisplayNoteSequence()
	{
		isDisplayingSequence = true;

		while (displayQueue.Count > 0)
		{
			RuntimeNote nextNote = displayQueue.Dequeue();
			OpenNoteImmediate(nextNote);
			while (isReadingNote) yield return null;
			if (displayQueue.Count > 0)
				yield return new WaitForSeconds(0.5f);
		}

		isDisplayingSequence = false;
		displayCoroutine = null;
	}

	void OpenNoteImmediate(RuntimeNote note)
	{
		isReadingNote = true;
		savedTimeScale = Time.timeScale;
		Time.timeScale = 0f;

		if (noteCanvas != null)
			noteCanvas.gameObject.SetActive(true);

		if (noteText != null)
		{
			int noteNumber = collectedNotes.Count;
			noteText.text = "Note #" + noteNumber + "\n\n" + note.Text;
		}

		Cursor.lockState = CursorLockMode.None;
		Cursor.visible = true;
	}

	void CloseNote()
	{
		isReadingNote = false;
		Time.timeScale = savedTimeScale;
		StartCoroutine(FadeOutAndHide());
		Cursor.lockState = CursorLockMode.Locked;
		Cursor.visible = false;
	}

	IEnumerator FadeOutAndHide()
	{
		yield return new WaitForSecondsRealtime(1f / fadeSpeed);
		if (noteCanvas != null && !isReadingNote)
			noteCanvas.gameObject.SetActive(false);
	}

	void UpdateCounter()
	{
		if (notesCounterText != null && showCounter)
		{
			notesCounterText.text = "Notes: " + collectedNotes.Count + "/" + notesRequiredToFinish;
			RectTransform rect = notesCounterText.GetComponent<RectTransform>();
			if (rect != null) rect.anchoredPosition = counterPosition;
		}
	}

	public int GetCollectedNotesCount() => collectedNotes.Count;
	public int GetTotalNotesCount() => notesRequiredToFinish;
	public bool AllNotesCollected() => collectedNotes.Count >= notesRequiredToFinish;
	public int GetStoryProgress() => collectedNotes.Count;
	public int GetRequiredNotesToFinish() => notesRequiredToFinish;

	public void SetCounterVisible(bool visible)
	{
		showCounter = visible;
		if (notesCounterText != null)
			notesCounterText.gameObject.SetActive(visible);
	}

	void OnDrawGizmosSelected()
	{
		for (int i = 0; i < spawnedNotes.Count; i++)
		{
			GameObject noteObject = spawnedNotes[i];
			if (noteObject == null || !noteMap.ContainsKey(noteObject)) continue;
			RuntimeNote note = noteMap[noteObject];
			Gizmos.color = Color.yellow;
			Gizmos.DrawWireSphere(noteObject.transform.position, note.TriggerRadius);
			Gizmos.DrawSphere(noteObject.transform.position, 0.15f);
		}
	}
}

public class NoteTrigger : MonoBehaviour
{
	public NoteSystem noteSystem;
	public NoteSystem.RuntimeNote runtimeNote;

	void OnTriggerEnter(Collider other)
	{
		if (other.CompareTag("Player") && noteSystem != null)
		{
			noteSystem.CollectNote(gameObject, runtimeNote);
		}
	}
}
