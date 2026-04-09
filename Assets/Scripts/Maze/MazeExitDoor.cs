using UnityEngine;
using TMPro;

public class MazeExitDoor : MonoBehaviour
{
	[Header("Interaction")]
	public KeyCode interactKey = KeyCode.E;
	public string interactionText = "Press E to escape";
	public Vector3 textOffset = new Vector3(0f, 2f, -0.8f);

	private bool playerInRange;
	private GameManager gameManager;
	private TextMeshPro interactionTextMesh;
	private GameObject interactionTextObject;

	void Start()
	{
		gameManager = FindObjectOfType<GameManager>();
		CreateInteractionText();
	}

	void Update()
	{
		if (!playerInRange || gameManager == null)
		{
			return;
		}

		if (Input.GetKeyDown(interactKey))
		{
			gameManager.ActivateExitDoor();
			HideInteractionText();
		}
	}

	void CreateInteractionText()
	{
		interactionTextObject = new GameObject("ExitDoorInteractionText");
		interactionTextObject.transform.SetParent(transform);
		interactionTextObject.transform.localPosition = textOffset;
		interactionTextObject.transform.localRotation = Quaternion.identity;
		interactionTextObject.transform.localScale = Vector3.one * 0.1f;

		interactionTextMesh = interactionTextObject.AddComponent<TextMeshPro>();
		interactionTextMesh.text = string.Empty;
		interactionTextMesh.fontSize = 16;
		interactionTextMesh.color = Color.white;
		interactionTextMesh.alignment = TextAlignmentOptions.Center;
		interactionTextMesh.enableWordWrapping = false;

		interactionTextObject.SetActive(false);
	}

	void ShowInteractionText()
	{
		if (interactionTextMesh == null || interactionTextObject == null)
		{
			return;
		}

		interactionTextMesh.text = interactionText;
		interactionTextObject.SetActive(true);
	}

	void HideInteractionText()
	{
		if (interactionTextObject != null)
		{
			interactionTextObject.SetActive(false);
		}
	}

	void OnTriggerEnter(Collider other)
	{
		if (!other.CompareTag("Player"))
		{
			return;
		}

		playerInRange = true;
		ShowInteractionText();
	}

	void OnTriggerExit(Collider other)
	{
		if (!other.CompareTag("Player"))
		{
			return;
		}

		playerInRange = false;
		HideInteractionText();
	}

	void OnDestroy()
	{
		if (interactionTextObject != null)
		{
			Destroy(interactionTextObject);
		}
	}
}
