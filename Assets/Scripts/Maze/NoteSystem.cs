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
		public float triggerRadius = 2f;
		public bool readOnce = true;
	}

	[Header("Note Configuration")]
	public List<Note> notes = new List<Note>();

	[Header("Spawn Settings")]
	public GameObject notePrefab;
	public float minDistanceBetweenNotes = 15f;
	public float minDistanceFromStart = 10f;
	public int maxSpawnAttempts = 50;

	[Header("References")]
	public MazeGenerator mazeGenerator;
	public Transform player;

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

	private List<GameObject> spawnedNotes = new List<GameObject>();
	private Dictionary<GameObject, Note> noteMap = new Dictionary<GameObject, Note>();
	private Dictionary<GameObject, bool> readNotes = new Dictionary<GameObject, bool>();
    
	private List<Note> collectedNotes = new List<Note>();
	private Queue<Note> displayQueue = new Queue<Note>();
	private bool isDisplayingSequence = false;
	private Coroutine displayCoroutine;

	private bool isReadingNote = false;
	private GameObject currentNoteObject;
	private float savedTimeScale = 1f;
	private CanvasGroup canvasGroup;

	// Track the next note to display in sequence
	private int nextDisplayIndex = 0;

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

		SetupUI();
		Invoke("SpawnNotes", 2f);
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
		{
			notesCounterText.gameObject.SetActive(showCounter);
		}
	}

	void SpawnNotes()
	{
		if (mazeGenerator == null || player == null)
		{
			Debug.LogError("Cannot spawn notes: missing references");
			return;
		}

		// Spawn all notes at once
		foreach (Note note in notes)
		{
			Vector3 spawnPos = FindValidNotePosition();
			if (spawnPos != Vector3.zero)
			{
				SpawnNote(spawnPos, note);
			}
			else
			{
				Debug.LogWarning($"Could not find valid position for note: {note.noteID}");
			}
		}

		Debug.Log($"Spawned {spawnedNotes.Count} notes in the maze");
	}

	Vector3 FindValidNotePosition()
	{
		Vector3 startPosition = mazeGenerator.GetStartPosition();
        
		for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
		{
			int x = Random.Range(1, mazeGenerator.width - 1);
			int y = Random.Range(1, mazeGenerator.height - 1);

			if (mazeGenerator.GetMazeCell(x, y) == 0) // Path cell
			{
				Vector3 worldPos = new Vector3(
					x * mazeGenerator.cellSize + mazeGenerator.cellSize / 2,
					0.1f,
					y * mazeGenerator.cellSize + mazeGenerator.cellSize / 2
				);

				// Check distance from player start
				if (Vector3.Distance(worldPos, startPosition) < minDistanceFromStart)
					continue;

				// Check distance from other notes
				bool tooClose = false;
				foreach (GameObject existingNote in spawnedNotes)
				{
					if (Vector3.Distance(worldPos, existingNote.transform.position) < minDistanceBetweenNotes)
					{
						tooClose = true;
						break;
					}
				}

				if (!tooClose)
					return worldPos;
			}
		}

		return Vector3.zero;
	}

	void SpawnNote(Vector3 position, Note note)
	{
		GameObject noteObject;

		if (notePrefab != null)
		{
			noteObject = Instantiate(notePrefab, position, Quaternion.Euler(90, Random.Range(0f, 360f), 0));
			noteObject.name = $"Note_{note.noteID}";
            
			// Ensure the prefab has a collider
			Collider collider = noteObject.GetComponent<Collider>();
			if (collider == null)
			{
				SphereCollider sphereCollider = noteObject.AddComponent<SphereCollider>();
				sphereCollider.isTrigger = true;
				sphereCollider.radius = note.triggerRadius;
			}
		}
		else
		{
			// Create simple paper representation
			noteObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
			noteObject.transform.position = position;
			noteObject.transform.rotation = Quaternion.Euler(90, Random.Range(0f, 360f), 0);
			noteObject.transform.localScale = new Vector3(0.4f, 0.6f, 1f);
			noteObject.name = $"Note_{note.noteID}";
            
			// Make it look like old paper
			Material paperMaterial = new Material(Shader.Find("Standard"));
			paperMaterial.color = new Color(0.9f, 0.85f, 0.7f);
			paperMaterial.SetFloat("_Metallic", 0f);
			paperMaterial.SetFloat("_Glossiness", 0.1f);
			noteObject.GetComponent<Renderer>().material = paperMaterial;

			// Add trigger collider
			SphereCollider triggerCollider = noteObject.AddComponent<SphereCollider>();
			triggerCollider.isTrigger = true;
			triggerCollider.radius = note.triggerRadius;

			// Add slight hover effect
			noteObject.transform.position += Vector3.up * 0.05f;
		}

		// Add NoteTrigger component for better detection
		NoteTrigger noteTrigger = noteObject.AddComponent<NoteTrigger>();
		noteTrigger.noteSystem = this;
		noteTrigger.note = note;

		noteObject.transform.SetParent(transform);
		spawnedNotes.Add(noteObject);
		noteMap[noteObject] = note;
		readNotes[noteObject] = false;
        
		Debug.Log($"Spawned note '{note.noteID}' at {position}");
	}

	void Update()
	{
		if (player == null) return;

		// Handle fade in/out
		if (canvasGroup != null)
		{
			float targetAlpha = isReadingNote ? 1f : 0f;
			canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, targetAlpha, Time.unscaledDeltaTime * fadeSpeed);
		}

		// Check for space key to close note
		if (isReadingNote && Input.GetKeyDown(KeyCode.Space))
		{
			CloseNote();
		}

		// Don't check for notes if already reading one or displaying sequence
		if (isReadingNote || isDisplayingSequence) return;

		// Fallback: Check for nearby notes (in case trigger colliders don't work)
		CheckNearbyNotesFallback();
	}

	void CheckNearbyNotesFallback()
	{
		foreach (GameObject noteObject in spawnedNotes.ToList())
		{
			if (noteObject == null || !noteObject.activeInHierarchy) continue;
            
			Note note = noteMap[noteObject];
			bool alreadyRead = readNotes[noteObject];

			// Skip if already read and set to read once
			if (alreadyRead && note.readOnce) continue;

			float distance = Vector3.Distance(player.position, noteObject.transform.position);

			if (distance <= note.triggerRadius)
			{
				Debug.Log($"Fallback detection: Player is {distance:F1} units from note '{note.noteID}'");
				CollectNote(noteObject, note);
				break;
			}
		}
	}

	// Called by NoteTrigger when player enters collider
	public void CollectNote(GameObject noteObject, Note note)
	{
		if (noteObject == null || readNotes[noteObject])
			return;

		// Hide the world object
		noteObject.SetActive(false);
        
		// Mark as read
		readNotes[noteObject] = true;
        
		// Add to collected notes
		if (!collectedNotes.Contains(note))
		{
			collectedNotes.Add(note);
            
			// Update HUD counter
			UpdateCounter();
            
			Debug.Log($"Collected note: {note.noteID}");
            
			// Get the next note in sequence to display
			if (nextDisplayIndex < notes.Count)
			{
				Note noteToDisplay = notes[nextDisplayIndex];
				displayQueue.Enqueue(noteToDisplay);
				nextDisplayIndex++;
                
				// Start display sequence if not already running
				if (!isDisplayingSequence && displayCoroutine == null)
				{
					displayCoroutine = StartCoroutine(DisplayNoteSequence());
				}
			}
		}
	}

	IEnumerator DisplayNoteSequence()
	{
		isDisplayingSequence = true;
        
		while (displayQueue.Count > 0)
		{
			Note nextNote = displayQueue.Dequeue();
            
			// Display the note
			OpenNoteImmediate(nextNote);
            
			// Wait for note to be closed
			while (isReadingNote)
			{
				yield return null;
			}
            
			// Brief pause between notes if there are more in queue
			if (displayQueue.Count > 0)
			{
				yield return new WaitForSeconds(0.5f);
			}
		}
        
		isDisplayingSequence = false;
		displayCoroutine = null;
	}

	void OpenNoteImmediate(Note note)
	{
		isReadingNote = true;

		// Pause game
		savedTimeScale = Time.timeScale;
		Time.timeScale = 0f;

		// Show UI
		if (noteCanvas != null)
			noteCanvas.gameObject.SetActive(true);

		if (noteText != null)
			noteText.text = note.noteText;

		// Show note number in UI for progression context
		if (noteText != null)
		{
			int noteNumber = notes.IndexOf(note) + 1;
			noteText.text = $"Note #{noteNumber}\n\n{note.noteText}";
		}

		// Lock cursor
		Cursor.lockState = CursorLockMode.None;
		Cursor.visible = true;

		Debug.Log($"Displaying note: {note.noteID}");
	}

	void CloseNote()
	{
		isReadingNote = false;

		// Resume game
		Time.timeScale = savedTimeScale;

		// Start fade out
		StartCoroutine(FadeOutAndHide());

		// Unlock cursor
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
			notesCounterText.text = $"Notes: {collectedNotes.Count}/{notes.Count}";
            
			RectTransform rect = notesCounterText.GetComponent<RectTransform>();
			if (rect != null)
			{
				rect.anchoredPosition = counterPosition;
			}
		}
	}

	// Public methods
	public int GetCollectedNotesCount() => collectedNotes.Count;
	public int GetTotalNotesCount() => notes.Count;
	public bool AllNotesCollected() => collectedNotes.Count >= notes.Count;
	public void SetCounterVisible(bool visible)
	{
		showCounter = visible;
		if (notesCounterText != null)
			notesCounterText.gameObject.SetActive(visible);
	}

	// Get the current story progress
	public int GetStoryProgress() => nextDisplayIndex;

	void OnDrawGizmosSelected()
	{
		foreach (GameObject noteObject in spawnedNotes)
		{
			if (noteObject != null && noteMap.ContainsKey(noteObject))
			{
				Note note = noteMap[noteObject];
				bool read = readNotes.ContainsKey(noteObject) && readNotes[noteObject];

				Gizmos.color = read ? Color.gray : Color.yellow;
				Gizmos.DrawWireSphere(noteObject.transform.position, note.triggerRadius);
                
				Gizmos.color = read ? new Color(0.5f, 0.5f, 0.5f, 0.5f) : new Color(1f, 1f, 0f, 0.5f);
				Gizmos.DrawSphere(noteObject.transform.position, 0.15f);
			}
		}
	}
}

public class NoteTrigger : MonoBehaviour
{
	public NoteSystem noteSystem;
	public NoteSystem.Note note;

	void OnTriggerEnter(Collider other)
	{
		if (other.CompareTag("Player"))
		{
			Debug.Log($"Player entered trigger of note: {note.noteID}");
			noteSystem.CollectNote(gameObject, note);
		}
	}
}