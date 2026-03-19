using System.Reflection;
using UnityEngine;

public class HorrorDebugOverlay : MonoBehaviour
{
	[Header("References")]
	public HorrorDirector horrorDirector;
	public VillainAI villainAI;
	public MonoBehaviour sanitySource;

	[Header("Display")]
	public bool showOverlay = true;
	public KeyCode toggleKey = KeyCode.F3;
	public Vector2 position = new Vector2(16f, 16f);
	public Vector2 size = new Vector2(280f, 110f);

	private GUIStyle labelStyle;
	private PropertyInfo sanityProperty;
	private FieldInfo sanityField;

	void Start()
	{
		if (horrorDirector == null)
		{
			horrorDirector = FindObjectOfType<HorrorDirector>();
		}

		if (villainAI == null)
		{
			villainAI = FindObjectOfType<VillainAI>();
		}

		CacheSanityMembers();
	}

	void Update()
	{
		if (Input.GetKeyDown(toggleKey))
		{
			showOverlay = !showOverlay;
		}
	}

	void OnGUI()
	{
		if (!showOverlay)
		{
			return;
		}

		EnsureStyle();
		Rect rect = new Rect(position.x, position.y, size.x, size.y);
		GUI.Box(rect, "Horror Debug");

		string tension = horrorDirector != null ? horrorDirector.currentTension.ToString("0.00") : "N/A";
		string band = horrorDirector != null ? horrorDirector.CurrentBand.ToString() : "N/A";
		string aiState = villainAI != null ? villainAI.GetAIState() : "N/A";
		string chaseActive = horrorDirector != null ? (horrorDirector.IsChaseActive ? "Yes" : "No") : (villainAI != null && villainAI.IsChasing ? "Yes" : "No");
		string sanity = TryReadSanityValue(out float sanityValue) ? sanityValue.ToString("0.0") : "N/A";

		Rect contentRect = new Rect(rect.x + 12f, rect.y + 24f, rect.width - 24f, rect.height - 28f);
		GUI.Label(contentRect, $"Tension: {tension}\nBand: {band}\nAI State: {aiState}\nSanity: {sanity}\nChase Active: {chaseActive}", labelStyle);
	}

	void EnsureStyle()
	{
		if (labelStyle != null)
		{
			return;
		}

		labelStyle = new GUIStyle(GUI.skin.label);
		labelStyle.fontSize = 14;
		labelStyle.normal.textColor = Color.white;
	}

	void CacheSanityMembers()
	{
		if (sanitySource == null)
		{
			return;
		}

		var sourceType = sanitySource.GetType();
		sanityProperty = sourceType.GetProperty("CurrentSanity");
		if (sanityProperty == null)
		{
			sanityProperty = sourceType.GetProperty("currentSanity");
		}

		sanityField = sourceType.GetField("CurrentSanity");
		if (sanityField == null)
		{
			sanityField = sourceType.GetField("currentSanity");
		}
	}

	bool TryReadSanityValue(out float sanityValue)
	{
		sanityValue = 0f;
		if (sanitySource == null)
		{
			return false;
		}

		if (sanityProperty != null)
		{
			object value = sanityProperty.GetValue(sanitySource, null);
			if (value is float propertyFloat)
			{
				sanityValue = propertyFloat;
				return true;
			}
		}

		if (sanityField != null)
		{
			object value = sanityField.GetValue(sanitySource);
			if (value is float fieldFloat)
			{
				sanityValue = fieldFloat;
				return true;
			}
		}

		return false;
	}
}
