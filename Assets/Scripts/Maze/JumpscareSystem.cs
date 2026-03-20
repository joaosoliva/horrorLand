using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class JumpscareSystem : MonoBehaviour
{
	[Header("Jumpscare Settings")]
	public VillainAI villainAI;
	public Transform player;
	public ChaseSystem chaseSystem;
	public HorrorDirector horrorDirector;
	public float minJumpscareInterval = 30f;
	public float maxJumpscareInterval = 60f;
	public float triggerDistance = 10f;
	public float maxDistanceForJumpscare = 20f;
    
	[Header("Warning System")]
	public bool enableWarning = true;
	public float warningDuration = 3f;
	public Canvas warningCanvas;
	public TextMeshProUGUI warningText;
	public string warningMessage = "HE IS NEAR!";
	public Color warningColor = Color.red;
    
	[Header("Jumpscare Visual")]
	public Canvas jumpscareCanvas; // SEPARATE canvas for jumpscare
	public Image jumpscareImage;
	public Sprite jumpscareSprite;
	public float jumpscareDuration = 0.5f;
	public int flashCount = 3;
	public AudioClip jumpscareSound;
    
	[Header("Screen Effects")]
	public Image screenFlash;
	public Color flashColor = Color.red;
	public float flashIntensity = 0.8f;
    
	private float nextJumpscareTime;
	private bool isJumpscareActive = false;
	private bool isWarningActive = false;
	private AudioSource audioSource;
	private Coroutine currentJumpscareCoroutine;
	private Coroutine currentWarningCoroutine;

	[Header("Debug")]
	public bool enableDebugLogs = true;
	public float lastDistance = 0f;

	void Start()
	{
		// Set up references
		if (chaseSystem == null)
		{
			chaseSystem = FindObjectOfType<ChaseSystem>();
		}

		if (horrorDirector == null)
		{
			horrorDirector = FindObjectOfType<HorrorDirector>();
		}

		if (villainAI == null)
		{
			villainAI = FindObjectOfType<VillainAI>();
			if (enableDebugLogs) Debug.Log("VillainAI found: " + (villainAI != null));
		}
        
		if (player == null)
		{
			GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
			if (playerObj != null)
			{
				player = playerObj.transform;
				if (enableDebugLogs) Debug.Log("Player found: " + player.name);
			}
		}
        
		// Set up audio
		audioSource = GetComponent<AudioSource>();
		if (audioSource == null)
			audioSource = gameObject.AddComponent<AudioSource>();
        
		// Initialize UI elements
		InitializeUI();
        
		// Set first jumpscare time
		ResetJumpscareTimer();
        
		if (enableDebugLogs) 
		{
			Debug.Log("Jumpscare system initialized");
			Debug.Log($"Next jumpscare in: {nextJumpscareTime - Time.time:F1} seconds");
		}
	}

	void InitializeUI()
	{
		// Initialize warning canvas
		if (warningCanvas != null)
		{
			warningCanvas.gameObject.SetActive(false);
            
			if (warningText != null)
			{
				warningText.color = warningColor;
				warningText.text = warningMessage;
			}
			if (enableDebugLogs) Debug.Log("Warning canvas initialized");
		}
		else
		{
			if (enableDebugLogs) Debug.LogWarning("Warning canvas not assigned!");
		}
        
		// Initialize jumpscare canvas and image - SEPARATE from warning
		if (jumpscareCanvas != null)
		{
			jumpscareCanvas.gameObject.SetActive(false);
			if (enableDebugLogs) Debug.Log("Jumpscare canvas initialized");
		}
		else
		{
			// Fallback: use the existing image directly
			if (jumpscareImage != null)
			{
				jumpscareImage.gameObject.SetActive(false);
				if (jumpscareSprite != null)
				{
					jumpscareImage.sprite = jumpscareSprite;
				}
				if (enableDebugLogs) Debug.Log("Jumpscare image initialized (no canvas)");
			}
			else
			{
				if (enableDebugLogs) Debug.LogWarning("Jumpscare image not assigned!");
			}
		}
        
		// Initialize screen flash
		if (screenFlash != null)
		{
			screenFlash.gameObject.SetActive(false);
			screenFlash.color = Color.clear;
			if (enableDebugLogs) Debug.Log("Screen flash initialized");
		}
		else
		{
			if (enableDebugLogs) Debug.LogWarning("Screen flash not assigned!");
		}
	}

	void Update()
	{
		if (villainAI == null || player == null) 
		{
			if (enableDebugLogs && Time.frameCount % 60 == 0) 
				Debug.LogWarning("Missing references: VillainAI=" + (villainAI != null) + " Player=" + (player != null));
			return;
		}
        
		if (isJumpscareActive) return;

		// Calculate distance to villain
		float distanceToVillain = Vector3.Distance(player.position, villainAI.transform.position);
		lastDistance = distanceToVillain; // For debugging
        
		// Check if it's time for a jumpscare
		if (Time.time >= nextJumpscareTime)
		{
			if (enableDebugLogs) 
			{
				Debug.Log($"Jumpscare timer reached! Distance: {distanceToVillain:F1}, " +
					$"Trigger Range: {triggerDistance}-{maxDistanceForJumpscare}");
			}
            
			// Only trigger jumpscare if villain is within range
			if (distanceToVillain <= maxDistanceForJumpscare && distanceToVillain >= triggerDistance)
			{
				if (chaseSystem != null && !chaseSystem.CanTriggerContextualJumpscare(distanceToVillain))
				{
					if (enableDebugLogs) Debug.Log("Jumpscare deferred by chase budget.");
					ResetJumpscareTimer();
					return;
				}

				if (enableDebugLogs) Debug.Log("Distance condition met! Triggering jumpscare...");
                
				if (enableWarning)
				{
					StartWarning();
				}
				else
				{
					TriggerJumpscare();
				}
			}
			else
			{
				// Villain is too far or too close, reset timer
				if (enableDebugLogs) Debug.Log($"Distance condition NOT met. Resetting timer. Distance: {distanceToVillain:F1}");
				ResetJumpscareTimer();
			}
		}
		else if (enableDebugLogs && Time.frameCount % 120 == 0) // Log every 2 seconds
		{
			Debug.Log($"Jumpscare in: {nextJumpscareTime - Time.time:F1}s, Distance: {distanceToVillain:F1}");
		}
	}

	void StartWarning()
	{
		if (isWarningActive || isJumpscareActive) 
		{
			if (enableDebugLogs) Debug.Log("Warning blocked - already active");
			return;
		}
        
		if (enableDebugLogs) Debug.Log("Starting warning sequence...");
		currentWarningCoroutine = StartCoroutine(WarningRoutine());
	}

	IEnumerator WarningRoutine()
	{
		isWarningActive = true;
        
		if (enableDebugLogs) Debug.Log("Warning routine started");

		// Show warning
		if (warningCanvas != null)
		{
			warningCanvas.gameObject.SetActive(true);
			if (enableDebugLogs) Debug.Log("Warning canvas activated");
            
			// Flash warning text
			float elapsed = 0f;
			while (elapsed < warningDuration)
			{
				float alpha = Mathf.PingPong(elapsed * 4f, 1f);
				if (warningText != null)
				{
					Color color = warningColor;
					color.a = alpha;
					warningText.color = color;
				}
                
				elapsed += Time.deltaTime;
				yield return null;
			}
            
			warningCanvas.gameObject.SetActive(false);
			if (enableDebugLogs) Debug.Log("Warning canvas deactivated");
		}
		else
		{
			if (enableDebugLogs) Debug.LogWarning("No warning canvas - waiting duration");
			// Fallback: wait for warning duration
			yield return new WaitForSeconds(warningDuration);
		}
        
		isWarningActive = false;
        
		if (enableDebugLogs) Debug.Log("Warning complete - triggering jumpscare");
        
		// Trigger jumpscare after warning
		TriggerJumpscare();
	}

	void TriggerJumpscare()
	{
		if (isJumpscareActive) 
		{
			if (enableDebugLogs) Debug.Log("Jumpscare blocked - already active");
			return;
		}
        
		if (chaseSystem != null)
		{
			chaseSystem.ConsumeJumpscareBudget();
		}

		HorrorEvents.RaiseJumpscareTriggered();
		if (enableDebugLogs) Debug.Log("=== TRIGGERING JUMPSCARE ===");
		currentJumpscareCoroutine = StartCoroutine(JumpscareRoutine());
		ResetJumpscareTimer();
	}

	IEnumerator JumpscareRoutine()
	{
		isJumpscareActive = true;
        
		if (enableDebugLogs) Debug.Log("Jumpscare routine started");

		// Play sound
		if (jumpscareSound != null && audioSource != null)
		{
			audioSource.PlayOneShot(jumpscareSound);
			if (enableDebugLogs) Debug.Log("Playing jumpscare sound");
		}
		else
		{
			if (enableDebugLogs) Debug.LogWarning("No jumpscare sound or audio source");
		}
        
		// Flash jumpscare image - USE SEPARATE CANVAS
		if (jumpscareImage != null && jumpscareSprite != null)
		{
			if (enableDebugLogs) Debug.Log("Starting image flash");
            
			// Enable the jumpscare canvas if it exists
			if (jumpscareCanvas != null)
			{
				jumpscareCanvas.gameObject.SetActive(true);
				if (enableDebugLogs) Debug.Log("Jumpscare canvas activated");
			}
            
			for (int i = 0; i < flashCount; i++)
			{
				jumpscareImage.gameObject.SetActive(true);
				if (enableDebugLogs) Debug.Log($"Flash {i+1}/{flashCount} - ON");
				yield return new WaitForSeconds(jumpscareDuration / (flashCount * 2));
                
				jumpscareImage.gameObject.SetActive(false);
				if (enableDebugLogs) Debug.Log($"Flash {i+1}/{flashCount} - OFF");
				yield return new WaitForSeconds(jumpscareDuration / (flashCount * 2));
			}
            
			// Disable the jumpscare canvas after flashing
			if (jumpscareCanvas != null)
			{
				jumpscareCanvas.gameObject.SetActive(false);
				if (enableDebugLogs) Debug.Log("Jumpscare canvas deactivated");
			}
		}
		else
		{
			if (enableDebugLogs) Debug.LogWarning("No jumpscare image or sprite - skipping image flash");
		}
        
		// Screen flash effect
		if (screenFlash != null)
		{
			if (enableDebugLogs) Debug.Log("Starting screen flash");
			screenFlash.gameObject.SetActive(true);
            
			// Flash screen
			float flashTime = jumpscareDuration * 0.5f;
			float elapsed = 0f;
            
			while (elapsed < flashTime)
			{
				float t = elapsed / flashTime;
				Color color = flashColor;
				color.a = Mathf.Lerp(flashIntensity, 0f, t);
				screenFlash.color = color;
                
				elapsed += Time.deltaTime;
				yield return null;
			}
            
			screenFlash.gameObject.SetActive(false);
			if (enableDebugLogs) Debug.Log("Screen flash complete");
		}
		else
		{
			if (enableDebugLogs) Debug.LogWarning("No screen flash - skipping");
		}
        
		isJumpscareActive = false;
		if (enableDebugLogs) Debug.Log("=== JUMPSCARE COMPLETED ===");
	}

	void ResetJumpscareTimer()
	{
		float interval = Random.Range(minJumpscareInterval, maxJumpscareInterval);
		nextJumpscareTime = Time.time + interval;
		if (enableDebugLogs) Debug.Log($"Timer reset - next jumpscare in {interval:F1} seconds");
	}

	// ========== TEST METHODS ==========
    
	[ContextMenu("Test Jumpscare")]
	public void TestJumpscare()
	{
		if (enableDebugLogs) Debug.Log("=== MANUAL TEST JUMPSCARE ===");
		TriggerJumpscare();
	}
    
	[ContextMenu("Test Warning")]
	public void TestWarning()
	{
		if (enableDebugLogs) Debug.Log("=== MANUAL TEST WARNING ===");
		StartWarning();
	}
    
	public void ForceJumpscare()
	{
		if (!isJumpscareActive)
		{
			if (enableDebugLogs) Debug.Log("=== FORCE JUMPSCARE ===");
			TriggerJumpscare();
		}
	}
    
	public void ForceJumpscareWithWarning()
	{
		if (!isJumpscareActive && !isWarningActive)
		{
			if (enableDebugLogs) Debug.Log("=== FORCE JUMPSCARE WITH WARNING ===");
			StartWarning();
		}
	}
    
	public void StopJumpscare()
	{
		if (currentJumpscareCoroutine != null)
		{
			StopCoroutine(currentJumpscareCoroutine);
		}
        
		if (currentWarningCoroutine != null)
		{
			StopCoroutine(currentWarningCoroutine);
		}
        
		// Reset all visual elements
		if (warningCanvas != null)
			warningCanvas.gameObject.SetActive(false);
        
		if (jumpscareCanvas != null)
			jumpscareCanvas.gameObject.SetActive(false);
        
		if (jumpscareImage != null)
			jumpscareImage.gameObject.SetActive(false);
        
		if (screenFlash != null)
			screenFlash.gameObject.SetActive(false);
        
		isJumpscareActive = false;
		isWarningActive = false;
        
		if (enableDebugLogs) Debug.Log("Jumpscare stopped");
	}
    
	public void SetJumpscareIntervals(float minInterval, float maxInterval)
	{
		minJumpscareInterval = minInterval;
		maxJumpscareInterval = maxInterval;
		ResetJumpscareTimer();
	}
    
	public void SetWarningEnabled(bool enabled)
	{
		enableWarning = enabled;
	}
    
	public bool IsJumpscareActive()
	{
		return isJumpscareActive || isWarningActive;
	}
}