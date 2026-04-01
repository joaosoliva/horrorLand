using System.Collections;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(SphereCollider))]
public class SafeSpaceZone : MonoBehaviour
{
	[Header("References")]
	public Transform player;
	public SanitySystem sanitySystem;
	public SpatialMapController mapController;
	public Light safeAreaLight;
	public TextMeshProUGUI interactionPromptText;
	public TextMeshProUGUI safeTimerText;

	[Header("Safe Area")]
	public float activeRadius = 3.5f;
	public KeyCode interactKey = KeyCode.E;
	public float holdDuration = 1f;
	public float activeDuration = 12f;
	public float sanityRestorePerSecond = 5f;
	public bool canOnlyActivateOnce = true;
	public string promptTemplate = "(Hold E to wait in safe space).";

	[Header("Light Flicker")]
	public float flickerDuration = 1.3f;
	public float flickerInterval = 0.08f;

	private bool isConsumed;
	private bool isPlayerInside;
	private bool isActive;
	private float holdProgress;
	private float activeUntilTime = -999f;

	public bool IsActive => isActive;
	public bool IsPlayerProtected => isActive && isPlayerInside;

	public static bool IsPlayerProtectedGlobal(Transform targetPlayer)
	{
		if (targetPlayer == null)
		{
			return false;
		}

		SafeSpaceZone[] safeSpaces = FindObjectsOfType<SafeSpaceZone>();
		for (int i = 0; i < safeSpaces.Length; i++)
		{
			SafeSpaceZone zone = safeSpaces[i];
			if (zone != null && zone.player == targetPlayer && zone.IsPlayerProtected)
			{
				return true;
			}
		}

		return false;
	}

	void Start()
	{
		if (player == null)
		{
			GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
			if (playerObj != null)
			{
				player = playerObj.transform;
			}
		}

		if (sanitySystem == null)
		{
			sanitySystem = FindObjectOfType<SanitySystem>();
		}

		if (mapController == null)
		{
			mapController = FindObjectOfType<SpatialMapController>();
		}

		SphereCollider sphere = GetComponent<SphereCollider>();
		sphere.isTrigger = true;
		sphere.radius = activeRadius;

		if (safeAreaLight == null)
		{
			safeAreaLight = GetComponent<Light>();
		}


		RefreshPrompt();
		RefreshTimer();
	}

	void Update()
	{
		UpdatePlayerInsideState();

		if (isConsumed)
		{
			return;
		}

		if (!isActive)
		{
			HandleActivationInput();
		}
		else
		{
			if (sanitySystem != null && isPlayerInside)
			{
				sanitySystem.AddExternalSanity(sanityRestorePerSecond * Time.deltaTime, "SafeSpace");
			}

			if (Time.time >= activeUntilTime)
			{
				DeactivateSafeSpace();
			}
		}

		RefreshPrompt();
		RefreshTimer();
	}

	void HandleActivationInput()
	{
		if (!isPlayerInside)
		{
			holdProgress = 0f;
			return;
		}

		if (Input.GetKey(interactKey))
		{
			holdProgress += Time.deltaTime;
			if (holdProgress >= holdDuration)
			{
				ActivateSafeSpace();
			}
		}
		else
		{
			holdProgress = 0f;
		}
	}

	void ActivateSafeSpace()
	{
		isActive = true;
		activeUntilTime = Time.time + activeDuration;
		holdProgress = 0f;

		if (safeAreaLight != null)
		{
			safeAreaLight.enabled = true;
		}

		if (mapController != null)
		{
			mapController.SetMapPromptEmphasis(true);
		}
	}

	void DeactivateSafeSpace()
	{
		isActive = false;
		if (mapController != null)
		{
			mapController.SetMapPromptEmphasis(false);
		}

		if (canOnlyActivateOnce)
		{
			isConsumed = true;
		}
		StartCoroutine(FlickerAndDisableLight());
	}

	IEnumerator FlickerAndDisableLight()
	{
		if (safeAreaLight == null)
		{
			yield break;
		}

		float elapsed = 0f;
		while (elapsed < flickerDuration)
		{
			safeAreaLight.enabled = !safeAreaLight.enabled;
			yield return new WaitForSeconds(flickerInterval);
			elapsed += flickerInterval;
		}

		safeAreaLight.enabled = false;
	}

	void UpdatePlayerInsideState()
	{
		if (player == null)
		{
			isPlayerInside = false;
			return;
		}

		Vector3 offset = player.position - transform.position;
		offset.y = 0f;
		isPlayerInside = offset.sqrMagnitude <= activeRadius * activeRadius;

		if (!isPlayerInside && !isActive)
		{
			holdProgress = 0f;
		}
	}

	void RefreshPrompt()
	{
		if (interactionPromptText == null)
		{
			return;
		}

		if (isConsumed)
		{
			interactionPromptText.gameObject.SetActive(false);
			return;
		}

		if (isActive)
		{
			interactionPromptText.gameObject.SetActive(true);
			interactionPromptText.text = "Safe space active";
			return;
		}

		if (!isPlayerInside)
		{
			interactionPromptText.gameObject.SetActive(false);
			return;
		}

		interactionPromptText.gameObject.SetActive(true);
		if (holdProgress <= 0f)
		{
			interactionPromptText.text = promptTemplate;
		}
		else
		{
			float pct = Mathf.Clamp01(holdProgress / Mathf.Max(0.01f, holdDuration));
			interactionPromptText.text = $"{promptTemplate} ({Mathf.RoundToInt(pct * 100f)}%)";
		}
	}

	void RefreshTimer()
	{
		if (safeTimerText == null)
		{
			return;
		}

		if (!isActive)
		{
			safeTimerText.gameObject.SetActive(false);
			return;
		}

		safeTimerText.gameObject.SetActive(true);
		float remaining = Mathf.Max(0f, activeUntilTime - Time.time);
		safeTimerText.text = $"Safe space: {remaining:F1}s";
	}
}
