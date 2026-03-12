using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
	[Header("Game References")]
	public NoteSystem noteSystem;
	public VillainAI villainAI;
	public MazeGenerator mazeGenerator;
	public Transform player;
    
	[Header("Win Settings")]
	public int notesRequiredToWin = 6;
	public float winDelay = 2f;
    
	[Header("Lose Settings")]
	public float loseDistance = 2f;
	public float loseDelay = 1f;
    
	[Header("UI References")]
	public Canvas gameOverCanvas;
	public TextMeshProUGUI gameOverText;
	public TextMeshProUGUI resultText;
	public TextMeshProUGUI restartHintText;
	public TextMeshProUGUI notesProgressText;
    
	[Header("Game Over Visuals")]
	public Image backgroundOverlay;
	public Color winBackgroundColor = new Color(0f, 0.5f, 0f, 0.8f);
	public Color loseBackgroundColor = new Color(0.5f, 0f, 0f, 0.8f);
	public AudioClip winSound;
	public AudioClip loseSound;
    
	[Header("Pause Settings")]
	public bool pauseOnGameOver = true;
    
	private bool gameEnded = false;
	private AudioSource audioSource;
	private CanvasGroup canvasGroup;
	private float originalTimeScale;

	void Start()
	{
		// Set up references
		if (noteSystem == null)
			noteSystem = FindObjectOfType<NoteSystem>();
            
		if (villainAI == null)
			villainAI = FindObjectOfType<VillainAI>();
			
		if (mazeGenerator == null)
			mazeGenerator = FindObjectOfType<MazeGenerator>();
            
		if (player == null)
		{
			GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
			if (playerObj != null)
				player = playerObj.transform;
		}
        
		// Set up audio
		audioSource = GetComponent<AudioSource>();
		if (audioSource == null)
			audioSource = gameObject.AddComponent<AudioSource>();
        
		// Initialize UI
		InitializeUI();
        
		// Store original time scale
		originalTimeScale = Time.timeScale;
        
		Debug.Log("Game Manager initialized");
	}

	void InitializeUI()
	{
		// Set up canvas group for fading
		if (gameOverCanvas != null)
		{
			canvasGroup = gameOverCanvas.GetComponent<CanvasGroup>();
			if (canvasGroup == null)
				canvasGroup = gameOverCanvas.gameObject.AddComponent<CanvasGroup>();
            
			canvasGroup.alpha = 0f;
			gameOverCanvas.gameObject.SetActive(false);
		}
        
		// Update progress text
		UpdateProgressText();
	}

	void Update()
	{
		if (gameEnded)
		{
			// Check for space key to restart when game is over
			if (Input.GetKeyDown(KeyCode.Space))
			{
				RestartGame();
			}
			return;
		}
        
		// Check win conditions
		CheckWinConditions();
        
		// Check lose condition
		CheckLoseCondition();
        
		// Update progress UI
		if (Time.frameCount % 30 == 0) // Update every 30 frames for performance
			UpdateProgressText();
	}

	void CheckWinConditions()
	{
		if (noteSystem == null || mazeGenerator == null) return;
        
		// Win Condition 1: Collect required notes
		int collectedNotes = noteSystem.GetCollectedNotesCount();
		if (collectedNotes >= notesRequiredToWin)
		{
			WinGame("[FINAL #02] Agora tudo faz sentido.");
			return;
		}
        
		// Win Condition 2: Reach the maze exit
		if (HasPlayerReachedExit())
		{
			WinGame("[FINAL #01] Você encontrou a saída, mas o mistério continua...");
			return;
		}
	}

	bool HasPlayerReachedExit()
	{
		if (player == null || mazeGenerator == null) return false;
		
		Vector3 exitPosition = mazeGenerator.GetExitPosition();
		float distanceToExit = Vector3.Distance(player.position, exitPosition);
		
		// Consider player reached exit if within 2 cells distance
		float exitReachDistance = mazeGenerator.cellSize * 2f;
		
		return distanceToExit <= exitReachDistance;
	}

	void CheckLoseCondition()
	{
		if (villainAI == null || player == null) return;
        
		float distanceToVillain = Vector3.Distance(player.position, villainAI.transform.position);
        
		// Lose Condition: Close distance AND villain can see player (line of sight)
		if (distanceToVillain <= loseDistance && VillainCanSeePlayer())
		{
			LoseGame();
		}
	}

	bool VillainCanSeePlayer()
	{
		if (villainAI == null || player == null) return false;
		
		// Use the villain's existing line of sight check
		return villainAI.CanSeePlayer();
	}

	void WinGame(string message)
	{
		if (gameEnded) return;
        
		gameEnded = true;
		Debug.Log("YOU WIN! " + message);
        
		StartCoroutine(WinGameRoutine(message));
	}

	IEnumerator WinGameRoutine(string message)
	{
		// Wait a moment before showing win screen (use unscaled time)
		yield return new WaitForSecondsRealtime(winDelay);
        
		// Play win sound
		if (winSound != null && audioSource != null)
			audioSource.PlayOneShot(winSound);
        
		// Show win screen
		ShowGameOverScreen("VITÓRIA", message, true);
	}

	void LoseGame()
	{
		if (gameEnded) return;
        
		gameEnded = true;
		Debug.Log("VOCÊ PERDEU!");
        
		StartCoroutine(LoseGameRoutine());
	}

	IEnumerator LoseGameRoutine()
	{
		// Wait a moment before showing lose screen (use unscaled time)
		yield return new WaitForSecondsRealtime(loseDelay);
        
		// Play lose sound
		if (loseSound != null && audioSource != null)
			audioSource.PlayOneShot(loseSound);
        
		// Show lose screen
		ShowGameOverScreen("GAME OVER", "Você foi pego pelo vilão", false);
	}

	void ShowGameOverScreen(string title, string message, bool isWin)
	{
		// Pause game if enabled
		if (pauseOnGameOver)
		{
			Time.timeScale = 0f;
		}
        
		// Show cursor
		Cursor.lockState = CursorLockMode.None;
		Cursor.visible = true;
        
		// Set up UI
		if (gameOverCanvas != null)
		{
			gameOverCanvas.gameObject.SetActive(true);
            
			// Set texts
			if (gameOverText != null)
				gameOverText.text = title;
                
			if (resultText != null)
				resultText.text = message;
			
			// Set restart hint text
			if (restartHintText != null)
				restartHintText.text = "Pressione ESPAÇO para reiniciar";
            
			// Set background color
			if (backgroundOverlay != null)
				backgroundOverlay.color = isWin ? winBackgroundColor : loseBackgroundColor;
            
			// Start fade in (using unscaled time)
			StartCoroutine(FadeInCanvas());
		}
        
		// Disable player controls if needed
		DisablePlayerControls();
        
		// Stop villain AI
		if (villainAI != null)
			villainAI.enabled = false;
	}

	IEnumerator FadeInCanvas()
	{
		if (canvasGroup == null) yield break;
        
		float fadeTime = 1f;
		float elapsed = 0f;
        
		while (elapsed < fadeTime)
		{
			// Use unscaled delta time so the fade works even when game is paused
			canvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / fadeTime);
			elapsed += Time.unscaledDeltaTime;
			yield return null;
		}
        
		canvasGroup.alpha = 1f;
	}

	void DisablePlayerControls()
	{
		// Disable player movement
		MonoBehaviour[] playerComponents = player.GetComponents<MonoBehaviour>();
		foreach (MonoBehaviour component in playerComponents)
		{
			if (component != this && component.enabled)
			{
				component.enabled = false;
			}
		}
        
		// Disable character controller if exists
		CharacterController characterController = player.GetComponent<CharacterController>();
		if (characterController != null)
			characterController.enabled = false;
	}

	void UpdateProgressText()
	{
		if (notesProgressText != null && noteSystem != null)
		{
			int collected = noteSystem.GetCollectedNotesCount();
			notesProgressText.text = $"Notes: {collected}/{notesRequiredToWin}";
		}
	}

	// ========== GAME FLOW METHODS ==========
    
	void RestartGame()
	{
		Debug.Log("Restarting game...");
        
		// Resume time scale first
		Time.timeScale = originalTimeScale;
        
		// Reload the current scene for a proper restart
		string currentSceneName = SceneManager.GetActiveScene().name;
		SceneManager.LoadScene(currentSceneName);
	}
    
	void QuitGame()
	{
		Debug.Log("Quitting game...");
        
        #if UNITY_EDITOR
		UnityEditor.EditorApplication.isPlaying = false;
        #else
		Application.Quit();
        #endif
	}

	// ========== PUBLIC METHODS ==========
    
	public bool IsGameEnded()
	{
		return gameEnded;
	}
    
	public void ForceWin()
	{
		WinGame("Developer forced win!");
	}
    
	public void ForceLose()
	{
		LoseGame();
	}
    
	public void SetNotesRequired(int notes)
	{
		notesRequiredToWin = notes;
		UpdateProgressText();
	}

	// ========== DEBUG METHODS ==========
    
	[ContextMenu("Test Win")]
	public void TestWin()
	{
		if (!gameEnded)
			WinGame("Test win triggered!");
	}
    
	[ContextMenu("Test Lose")]
	public void TestLose()
	{
		if (!gameEnded)
			LoseGame();
	}

	void OnDrawGizmosSelected()
	{
		// Draw lose radius around villain
		if (villainAI != null && enableDebugGizmos)
		{
			Gizmos.color = Color.red;
			Gizmos.DrawWireSphere(villainAI.transform.position, loseDistance);
		}
		
		// Draw exit position if maze generator exists
		if (mazeGenerator != null && enableDebugGizmos)
		{
			Gizmos.color = Color.yellow;
			Vector3 exitPos = mazeGenerator.GetExitPosition();
			Gizmos.DrawWireSphere(exitPos, mazeGenerator.cellSize * 0.5f);
			Gizmos.DrawIcon(exitPos + Vector3.up * 2f, "ExitIcon.png", true);
		}
	}
    
	[Header("Debug")]
	public bool enableDebugGizmos = true;
}