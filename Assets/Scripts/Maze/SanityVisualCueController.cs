using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SanityVisualCueController : MonoBehaviour
{
	[Header("Sanity Source")]
	public MonoBehaviour sanitySource;
	public string sanityPropertyName = "CurrentSanity";
	public string sanityFieldName = "currentSanity";
	public float minSanity = 0f;
	public float maxSanity = 100f;

	[Header("HUD")]
	public bool showHudBar = true;
	public bool autoCreateHudIfMissing = true;
	public RectTransform hudParent;
	public Slider sanitySlider;
	public Image sanityFillImage;
	public Gradient sanityBarGradient;
	public TextMeshProUGUI sanityLabelText;
	public TextMeshProUGUI sanityValueText;

	[Header("HUD Style (Optional Sprites)")]
	public Sprite sanityBarBackgroundSprite;
	public Sprite sanityBarFillSprite;
	public Vector2 hudAnchoredPosition = new Vector2(28f, -28f);

	[Header("Optional Vignette Overlay (ThreatFeedbackSystem owns danger vignette)")]
	public bool showVignetteOverlay = false;
	public CanvasGroup vignetteCanvasGroup;
	public Image vignetteImage;
	public Color vignetteColor = new Color(0.35f, 0f, 0f, 0.8f);
	public AnimationCurve vignetteStrengthByStress = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

	private PropertyInfo sanityProperty;
	private FieldInfo sanityField;
	private RectTransform runtimeHudRoot;

	void Start()
	{
		CacheSanityMembers();
		TryCreateHudIfNeeded();
		if (TryReadSanity(out float sanityValue, out float normalizedSanity))
		{
			RefreshVisuals(sanityValue, normalizedSanity);
		}
		else
		{
			RefreshVisuals(maxSanity, 1f);
		}
	}

	void Update()
	{
		if (!TryReadSanity(out float sanityValue, out float normalizedSanity))
		{
			return;
		}

		RefreshVisuals(sanityValue, normalizedSanity);
	}

	void CacheSanityMembers()
	{
		if (sanitySource == null)
		{
			return;
		}

		var sourceType = sanitySource.GetType();
		sanityProperty = sourceType.GetProperty(sanityPropertyName);
		if (sanityProperty == null)
		{
			sanityProperty = sourceType.GetProperty("currentSanity");
		}

		sanityField = sourceType.GetField(sanityFieldName);
		if (sanityField == null)
		{
			sanityField = sourceType.GetField("CurrentSanity");
		}
	}

	bool TryReadSanity(out float sanityValue, out float normalizedSanity)
	{
		sanityValue = maxSanity;
		normalizedSanity = 1f;
		if (sanitySource == null)
		{
			return false;
		}

		if (sanityProperty != null)
		{
			object value = sanityProperty.GetValue(sanitySource, null);
			if (TryConvertToFloat(value, out float propertySanity))
			{
				sanityValue = propertySanity;
				normalizedSanity = Mathf.InverseLerp(minSanity, maxSanity, propertySanity);
				return true;
			}
		}

		if (sanityField != null)
		{
			object value = sanityField.GetValue(sanitySource);
			if (TryConvertToFloat(value, out float fieldSanity))
			{
				sanityValue = fieldSanity;
				normalizedSanity = Mathf.InverseLerp(minSanity, maxSanity, fieldSanity);
				return true;
			}
		}

		return false;
	}

	bool TryConvertToFloat(object value, out float result)
	{
		if (value is float floatValue)
		{
			result = floatValue;
			return true;
		}

		if (value is int intValue)
		{
			result = intValue;
			return true;
		}

		result = 0f;
		return false;
	}

	void TryCreateHudIfNeeded()
	{
		if (!autoCreateHudIfMissing || sanitySlider != null)
		{
			return;
		}

		RectTransform parent = hudParent != null ? hudParent : GetOrCreateHudParent();
		runtimeHudRoot = CreateHudRoot(parent);

		sanityLabelText = CreateLabel(runtimeHudRoot, "Sanity", new Vector2(0f, -8f), TextAlignmentOptions.Left);
		sanityValueText = CreateLabel(runtimeHudRoot, "100", new Vector2(-8f, -8f), TextAlignmentOptions.Right);

		sanitySlider = CreateSlider(runtimeHudRoot);
		if (sanitySlider.fillRect != null)
		{
			sanityFillImage = sanitySlider.fillRect.GetComponent<Image>();
		}
	}

	RectTransform GetOrCreateHudParent()
	{
		Canvas canvas = FindObjectOfType<Canvas>();
		if (canvas == null)
		{
			GameObject canvasObject = new GameObject("Runtime HUD Canvas");
			canvas = canvasObject.AddComponent<Canvas>();
			canvas.renderMode = RenderMode.ScreenSpaceOverlay;
			canvasObject.AddComponent<CanvasScaler>();
			canvasObject.AddComponent<GraphicRaycaster>();
		}

		return canvas.GetComponent<RectTransform>();
	}

	RectTransform CreateHudRoot(RectTransform parent)
	{
		GameObject hudRootObject = new GameObject("Sanity HUD", typeof(RectTransform));
		hudRootObject.transform.SetParent(parent, false);
		RectTransform rectTransform = hudRootObject.GetComponent<RectTransform>();
		rectTransform.anchorMin = new Vector2(0f, 1f);
		rectTransform.anchorMax = new Vector2(0f, 1f);
		rectTransform.pivot = new Vector2(0f, 1f);
		rectTransform.anchoredPosition = hudAnchoredPosition;
		rectTransform.sizeDelta = new Vector2(300f, 70f);
		return rectTransform;
	}

	TextMeshProUGUI CreateLabel(RectTransform parent, string text, Vector2 anchoredPosition, TextAlignmentOptions alignment)
	{
		GameObject textObject = new GameObject(text + " Text", typeof(RectTransform), typeof(TextMeshProUGUI));
		textObject.transform.SetParent(parent, false);

		RectTransform textRect = textObject.GetComponent<RectTransform>();
		textRect.anchorMin = new Vector2(0f, 1f);
		textRect.anchorMax = new Vector2(1f, 1f);
		textRect.pivot = new Vector2(0.5f, 1f);
		textRect.anchoredPosition = anchoredPosition;
		textRect.sizeDelta = new Vector2(-8f, 24f);

		TextMeshProUGUI label = textObject.GetComponent<TextMeshProUGUI>();
		label.text = text;
		label.fontSize = 26f;
		label.alignment = alignment;
		label.color = Color.white;
		label.outlineWidth = 0.2f;
		label.outlineColor = Color.black;
		return label;
	}

	Slider CreateSlider(RectTransform parent)
	{
		GameObject sliderObject = new GameObject("Sanity Slider", typeof(RectTransform), typeof(Slider));
		sliderObject.transform.SetParent(parent, false);

		RectTransform sliderRect = sliderObject.GetComponent<RectTransform>();
		sliderRect.anchorMin = new Vector2(0f, 1f);
		sliderRect.anchorMax = new Vector2(0f, 1f);
		sliderRect.pivot = new Vector2(0f, 1f);
		sliderRect.anchoredPosition = new Vector2(0f, -36f);
		sliderRect.sizeDelta = new Vector2(280f, 18f);

		GameObject backgroundObject = new GameObject("Background", typeof(RectTransform), typeof(Image));
		backgroundObject.transform.SetParent(sliderObject.transform, false);
		RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
		backgroundRect.anchorMin = Vector2.zero;
		backgroundRect.anchorMax = Vector2.one;
		backgroundRect.offsetMin = Vector2.zero;
		backgroundRect.offsetMax = Vector2.zero;

		Image backgroundImage = backgroundObject.GetComponent<Image>();
		backgroundImage.sprite = sanityBarBackgroundSprite;
		backgroundImage.type = sanityBarBackgroundSprite != null ? Image.Type.Sliced : Image.Type.Simple;
		backgroundImage.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);

		GameObject fillAreaObject = new GameObject("Fill Area", typeof(RectTransform));
		fillAreaObject.transform.SetParent(sliderObject.transform, false);
		RectTransform fillAreaRect = fillAreaObject.GetComponent<RectTransform>();
		fillAreaRect.anchorMin = Vector2.zero;
		fillAreaRect.anchorMax = Vector2.one;
		fillAreaRect.offsetMin = new Vector2(2f, 2f);
		fillAreaRect.offsetMax = new Vector2(-2f, -2f);

		GameObject fillObject = new GameObject("Fill", typeof(RectTransform), typeof(Image));
		fillObject.transform.SetParent(fillAreaObject.transform, false);
		RectTransform fillRect = fillObject.GetComponent<RectTransform>();
		fillRect.anchorMin = new Vector2(0f, 0f);
		fillRect.anchorMax = new Vector2(1f, 1f);
		fillRect.offsetMin = Vector2.zero;
		fillRect.offsetMax = Vector2.zero;

		Image fillImage = fillObject.GetComponent<Image>();
		fillImage.sprite = sanityBarFillSprite;
		fillImage.type = sanityBarFillSprite != null ? Image.Type.Sliced : Image.Type.Simple;
		fillImage.color = Color.green;

		Slider slider = sliderObject.GetComponent<Slider>();
		slider.direction = Slider.Direction.LeftToRight;
		slider.minValue = minSanity;
		slider.maxValue = maxSanity;
		slider.value = maxSanity;
		slider.targetGraphic = fillImage;
		slider.fillRect = fillRect;

		return slider;
	}

	void RefreshVisuals(float sanityValue, float normalizedSanity)
	{
		float clampedSanity = Mathf.Clamp01(normalizedSanity);
		float clampedSanityValue = Mathf.Clamp(sanityValue, minSanity, maxSanity);

		if (sanitySlider != null)
		{
			sanitySlider.gameObject.SetActive(showHudBar);
			sanitySlider.value = clampedSanityValue;
		}

		if (sanityFillImage != null)
		{
			sanityFillImage.gameObject.SetActive(showHudBar);
			sanityFillImage.color = sanityBarGradient.Evaluate(clampedSanity);
		}

		if (sanityLabelText != null)
		{
			sanityLabelText.gameObject.SetActive(showHudBar);
			sanityLabelText.text = "Sanity";
		}

		if (sanityValueText != null)
		{
			sanityValueText.gameObject.SetActive(showHudBar);
			sanityValueText.text = Mathf.RoundToInt(clampedSanityValue) + "/" + Mathf.RoundToInt(maxSanity);
		}

		if (runtimeHudRoot != null)
		{
			runtimeHudRoot.gameObject.SetActive(showHudBar);
		}

		if (vignetteImage != null)
		{
			vignetteImage.gameObject.SetActive(false);
			vignetteImage.color = vignetteColor;
		}

		if (vignetteCanvasGroup != null)
		{
			vignetteCanvasGroup.gameObject.SetActive(false);
			vignetteCanvasGroup.alpha = 0f;
		}
	}
}
