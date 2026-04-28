using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SphereCollider))]
public class SafeSpaceZone : MonoBehaviour
{
	[Header("References")]
	public Transform player;
	public SanitySystem sanitySystem;
	public CorruptionSystem corruptionSystem;
	public SpatialMapController mapController;
	public Light safeAreaLight;

	[Header("Safe Area")]
	public float activeRadius = 3.5f;
	public KeyCode interactKey = KeyCode.E;
	public float holdDuration = 1f;
	public float activeDuration = 12f;
	public float sanityRestorePerSecond = 5f;
	public float corruptionStabilizePerSecond = 2.5f;
	public bool canOnlyActivateOnce = true;

	[Header("Prompts (Code-driven)")]
	public bool drawPromptsOnScreen = true;
	public string promptTemplate = "(Hold E to wait in safe space).";
	public Vector2 promptScreenOffset = new Vector2(0f, 140f);
	public int promptFontSize = 24;

	[Header("Light Flicker")]
	public float flickerDuration = 1.3f;
	public float flickerInterval = 0.08f;

	private bool isConsumed;
	private bool isPlayerInside;
	private bool isActive;
	private float holdProgress;
	private float activeUntilTime = -999f;
	private SphereCollider triggerSphere;
	private GUIStyle promptStyle;
	private float nextPlayerSearchTime = -999f;
	private bool previousPlayerInside;

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

	void Awake()
	{
		triggerSphere = GetComponent<SphereCollider>();
		triggerSphere.isTrigger = true;
		SyncRadius();
	}

	void Start()
	{
		TryResolveReferences(true);
	}

	void Update()
	{
		TryResolveReferences(false);
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
			if (corruptionSystem != null && isPlayerInside && corruptionStabilizePerSecond > 0f)
			{
				corruptionSystem.ReduceCorruption(corruptionStabilizePerSecond * Time.deltaTime, "LightStabilize");
			}

			if (Time.time >= activeUntilTime)
			{
				DeactivateSafeSpace();
			}
		}
	}

	void OnValidate()
	{
		if (activeRadius < 0.1f)
		{
			activeRadius = 0.1f;
		}
		if (triggerSphere == null)
		{
			triggerSphere = GetComponent<SphereCollider>();
		}
		if (triggerSphere != null)
		{
			triggerSphere.isTrigger = true;
			triggerSphere.radius = activeRadius;
		}
	}

	void SyncRadius()
	{
		if (triggerSphere != null)
		{
			triggerSphere.radius = Mathf.Max(0.1f, activeRadius);
		}
	}

	void TryResolveReferences(bool force)
	{
		if (force || player == null)
		{
			if (Time.time >= nextPlayerSearchTime)
			{
				GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
				if (playerObj != null)
				{
					player = playerObj.transform;
				}
				nextPlayerSearchTime = Time.time + 1.5f;
			}
		}

		if (force || sanitySystem == null)
		{
			sanitySystem = FindObjectOfType<SanitySystem>();
		}

		if (force || corruptionSystem == null)
		{
			corruptionSystem = FindObjectOfType<CorruptionSystem>();
		}

		if (force || mapController == null)
		{
			mapController = FindObjectOfType<SpatialMapController>();
		}

		if ((force || safeAreaLight == null) && safeAreaLight == null)
		{
			safeAreaLight = GetComponent<Light>();
		}
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
		HorrorEvents.RaiseLightSpotUsed();
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
		HorrorEvents.RaiseLightSpotExpired();
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

		Vector3 zoneCenter = transform.position;
		Vector3 playerPosition = player.position;
		float sqrDistance = (new Vector2(playerPosition.x, playerPosition.z) - new Vector2(zoneCenter.x, zoneCenter.z)).sqrMagnitude;
		float radius = Mathf.Max(0.1f, activeRadius);
		isPlayerInside = sqrDistance <= radius * radius;

		if (isPlayerInside && !previousPlayerInside)
		{
			HorrorEvents.RaiseLightSpotEntered();
		}

		if (!isPlayerInside && !isActive)
		{
			holdProgress = 0f;
		}

		previousPlayerInside = isPlayerInside;
	}

	void OnGUI()
	{
		if (!drawPromptsOnScreen || isConsumed)
		{
			return;
		}

		if (!isPlayerInside && !isActive)
		{
			return;
		}

		EnsurePromptStyle();
		string text;
		if (isActive)
		{
			float remaining = Mathf.Max(0f, activeUntilTime - Time.time);
			text = "Safe space active - " + remaining.ToString("F1") + "s";
		}
		else if (holdProgress > 0f)
		{
			float pct = Mathf.Clamp01(holdProgress / Mathf.Max(0.01f, holdDuration));
			text = promptTemplate + " (" + Mathf.RoundToInt(pct * 100f) + "%)";
		}
		else
		{
			text = promptTemplate;
		}

		Vector2 size = promptStyle.CalcSize(new GUIContent(text));
		float x = (Screen.width - size.x) * 0.5f + promptScreenOffset.x;
		float y = Screen.height - promptScreenOffset.y;
		GUI.Label(new Rect(x, y, size.x + 12f, size.y + 8f), text, promptStyle);
	}

	void EnsurePromptStyle()
	{
		if (promptStyle != null)
		{
			return;
		}

		promptStyle = new GUIStyle(GUI.skin.label);
		promptStyle.alignment = TextAnchor.MiddleCenter;
		promptStyle.fontSize = Mathf.Max(14, promptFontSize);
		promptStyle.fontStyle = FontStyle.Bold;
		promptStyle.normal.textColor = Color.white;
	}
}
