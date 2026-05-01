using UnityEngine;
using System.Collections;
using TMPro;
using System;

public class DoorTrigger : MonoBehaviour
{
	[Header("Door Settings")]
	public float doorWidth = 2f;
	public float doorHeight = 2.2f;
	public Vector3 facingDirection = Vector3.forward;
    
	[Header("Animation Settings")]
	public float openAngle = 90f;
	public float animationSpeed = 2f;
    
	[Header("UI Settings")]
	public Vector3 textOffset = new Vector3(0, 2f, -0.5f);
	public string openText = "Press E to open door";
	public string lockedText = "Door locked";
	public bool showLockedPrompt = false;
    
	private bool isOpen = false;
	private bool isLocked = false;
	private bool playerInsideTrigger = false;
	private bool playerHasExitedRoom = false;

	private Transform leftDoor;
	private Transform rightDoor;
	private Quaternion leftDoorClosedRot;
	private Quaternion rightDoorClosedRot;
	private GameObject interactionTextObject;
	private TextMeshPro interactionText;
	public event Action OnDoorOpened;
	public event Action OnDoorClosedLocked;

	public bool IsOpen => isOpen;
	public bool IsLocked => isLocked;
	public bool HasPlayerExitedRoom => playerHasExitedRoom;

	void Start()
	{
		// Find the door hinge objects (not the visuals)
		leftDoor = transform.Find("Door_Left");
		rightDoor = transform.Find("Door_Right");
        
		if (leftDoor != null) 
		{
			leftDoorClosedRot = leftDoor.localRotation;
		}
        
		if (rightDoor != null) 
		{
			rightDoorClosedRot = rightDoor.localRotation;
		}

		// Create interaction text programmatically
		CreateInteractionText();

		Debug.Log("Door trigger initialized. Press E when near the door from inside the room.");
	}

	void CreateInteractionText()
	{
		// Create the text object as a child of the door parent
		interactionTextObject = new GameObject("DoorInteractionText");
		interactionTextObject.transform.SetParent(transform); // Parent to the door trigger object
		interactionTextObject.transform.localPosition = textOffset;
		interactionTextObject.transform.localRotation = Quaternion.identity;
		
		// Add TextMeshPro component (3D version, not UI)
		interactionText = interactionTextObject.AddComponent<TextMeshPro>();
		
		// Set up text properties
		interactionText.text = "";
		interactionText.fontSize = 16;
		interactionText.color = Color.white;
		interactionText.alignment = TextAlignmentOptions.Center;
		interactionText.enableWordWrapping = false;
		
		// Make sure the text is always readable
		interactionTextObject.transform.localScale = Vector3.one * 0.1f;
		
		// Add a billboard component to make text always face camera
		//interactionTextObject.AddComponent<Billboard>();
		
		interactionTextObject.SetActive(false);
	}

	void Update()
	{
		if (isLocked)
		{
			if (Input.GetKeyDown(KeyCode.E) && playerInsideTrigger)
			{
				Debug.Log("Door interaction blocked: door locked.");
			}
			return;
		}

		// Player can open door when inside trigger zone (inside room) and pressing E
		if (Input.GetKeyDown(KeyCode.E) && !isOpen && playerInsideTrigger)
		{
			StartCoroutine(OpenDoors());
		}
	}

	void UpdateInteractionText()
	{
		if (interactionText == null) return;

		if (isLocked)
		{
			if (showLockedPrompt)
			{
				interactionText.text = lockedText;
			}
			else
			{
				interactionText.text = "";
				if (interactionTextObject != null && interactionTextObject.activeSelf)
				{
					Debug.Log("Door prompt suppressed: door locked.");
				}
			}
		}
		else if (!isOpen && playerInsideTrigger)
		{
			interactionText.text = openText;
		}
		else
		{
			interactionText.text = "";
		}
	}

	void ShowInteractionText()
	{
		if (interactionTextObject != null)
		{
			interactionTextObject.SetActive(true);
			UpdateInteractionText();
		}
	}

	void HideInteractionText()
	{
		if (interactionTextObject != null)
		{
			interactionTextObject.SetActive(false);
		}
	}

	IEnumerator OpenDoors()
	{
		isOpen = true;
		Debug.Log("Opening doors...");

		// Hide interaction text immediately when opening
		HideInteractionText();

		float t = 0;
		while (t < 1 && leftDoor != null && rightDoor != null)
		{
			// Left door swings out to the left (negative Y rotation around its hinge)
			leftDoor.localRotation = Quaternion.Lerp(leftDoorClosedRot, 
				Quaternion.Euler(0, -openAngle, 0), t);
            
			// Right door swings out to the right (positive Y rotation around its hinge)
			rightDoor.localRotation = Quaternion.Lerp(rightDoorClosedRot, 
				Quaternion.Euler(0, openAngle, 0), t);
            
			t += Time.deltaTime * animationSpeed;
			yield return null;
		}

		// Ensure final rotation
		if (leftDoor != null) leftDoor.localRotation = Quaternion.Euler(0, -openAngle, 0);
		if (rightDoor != null) rightDoor.localRotation = Quaternion.Euler(0, openAngle, 0);
		OnDoorOpened?.Invoke();
	}

	IEnumerator CloseDoors()
	{
		Debug.Log("Closing doors...");

		float t = 0;
		Quaternion leftStartRot = leftDoor.localRotation;
		Quaternion rightStartRot = rightDoor.localRotation;
        
		while (t < 1 && leftDoor != null && rightDoor != null)
		{
			leftDoor.localRotation = Quaternion.Lerp(leftStartRot, leftDoorClosedRot, t);
			rightDoor.localRotation = Quaternion.Lerp(rightStartRot, rightDoorClosedRot, t);
            
			t += Time.deltaTime * animationSpeed;
			yield return null;
		}

		// Ensure final rotation
		if (leftDoor != null) leftDoor.localRotation = leftDoorClosedRot;
		if (rightDoor != null) rightDoor.localRotation = rightDoorClosedRot;
        
		isOpen = false;
		isLocked = true;
		Debug.Log("Doors closed and locked.");
		OnDoorClosedLocked?.Invoke();
	}


	public void SetLockedState(bool locked)
	{
		isLocked = locked;
		if (isLocked)
		{
			HideInteractionText();
		}
		else if (!isOpen && playerInsideTrigger)
		{
			ShowInteractionText();
		}
		UpdateInteractionText();
	}

	public void ForceCloseAndLock(float delaySeconds = 0f)
	{
		if (isLocked)
		{
			return;
		}

		StopAllCoroutines();
		HideInteractionText();
		StartCoroutine(ForceCloseRoutine(Mathf.Max(0f, delaySeconds)));
	}

	IEnumerator ForceCloseRoutine(float delaySeconds)
	{
		if (delaySeconds > 0f)
		{
			yield return new WaitForSeconds(delaySeconds);
		}

		if (!isOpen)
		{
			isLocked = true;
			OnDoorClosedLocked?.Invoke();
			yield break;
		}

		yield return StartCoroutine(CloseDoors());
	}

	void OnTriggerEnter(Collider other)
	{
		if (other.CompareTag("Player"))
		{
			playerInsideTrigger = true;
			Debug.Log("Player entered door trigger zone (inside room)");

			// Show interaction text if door is not open and not locked
			if (!isOpen && !isLocked)
			{
				ShowInteractionText();
			}

			// If player re-enters the room after exiting, don't auto-close
			if (playerHasExitedRoom && isOpen)
			{
				Debug.Log("Player returned to room - keeping doors open");
			}
		}
	}

	void OnTriggerExit(Collider other)
	{
		if (other.CompareTag("Player"))
		{
			playerInsideTrigger = false;
			Debug.Log("Player left door trigger zone");

			// Hide interaction text when player leaves trigger
			HideInteractionText();

			// Check if player is exiting through the doors (moving in the facing direction)
			Vector3 toPlayer = (other.transform.position - transform.position).normalized;
			float dotProduct = Vector3.Dot(toPlayer, transform.forward);

			// If player is moving in the door's facing direction (exiting room) and doors are open
			if (dotProduct > 0.1f && isOpen && !playerHasExitedRoom && !isLocked)
			{
				playerHasExitedRoom = true;
				Debug.Log("Player exited through doors - closing behind them");
				StartCoroutine(CloseDoors());
			}
		}
	}

	// Visual debug - show trigger zone in editor
	void OnDrawGizmosSelected()
	{
		if (GetComponent<BoxCollider>() != null)
		{
			Gizmos.color = Color.yellow;
			Gizmos.matrix = transform.localToWorldMatrix;
			Gizmos.DrawWireCube(GetComponent<BoxCollider>().center, GetComponent<BoxCollider>().size);
			
			// Also show text position
			Gizmos.color = Color.cyan;
			Gizmos.DrawWireSphere(transform.position + textOffset, 0.2f);
		}
	}

	void OnDestroy()
	{
		// Clean up the interaction text object when this object is destroyed
		if (interactionTextObject != null)
		{
			Destroy(interactionTextObject);
		}
	}
}

// Simple billboard component to make text always face the camera
public class Billboard : MonoBehaviour
{
	void Update()
	{
		if (Camera.main != null)
		{
			// Make the text face the camera, but keep it upright
			Vector3 lookDirection = Camera.main.transform.position - transform.position;
			lookDirection.y = 0; // Keep text upright
			transform.rotation = Quaternion.LookRotation(lookDirection);
		}
	}
}
