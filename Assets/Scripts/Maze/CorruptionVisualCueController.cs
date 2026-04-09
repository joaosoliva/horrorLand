using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CorruptionVisualCueController : MonoBehaviour
{
	[Header("Corruption Source")]
	public CorruptionSystem corruptionSystem;
	public float minCorruption = 0f;
	public float maxCorruption = 100f;

	[Header("HUD")]
	public bool showHudBar = true;
	public bool autoCreateHudIfMissing = true;
	public RectTransform hudParent;
	public Slider corruptionSlider;
	public Image corruptionFillImage;
	public Gradient corruptionBarGradient;
	public TextMeshProUGUI corruptionLabelText;
	public TextMeshProUGUI corruptionValueText;

	[Header("HUD Style (Optional Sprites)")]
	public Sprite corruptionBarBackgroundSprite;
	public Sprite corruptionBarFillSprite;
	public Vector2 hudAnchoredPosition = new Vector2(28f, -104f);

	private RectTransform runtimeHudRoot;

	void Start()
	{
		if (corruptionSystem == null)
		{
			corruptionSystem = FindObjectOfType<CorruptionSystem>();
		}

		if (corruptionSystem != null)
		{
			maxCorruption = corruptionSystem.maxCorruption;
		}

		TryCreateHudIfNeeded();
		RefreshVisuals(corruptionSystem != null ? corruptionSystem.CurrentCorruption : minCorruption);
	}

	void OnEnable()
	{
		HorrorEvents.OnCorruptionChanged += HandleCorruptionChanged;
	}

	void OnDisable()
	{
		HorrorEvents.OnCorruptionChanged -= HandleCorruptionChanged;
	}

	void HandleCorruptionChanged(float currentCorruption, float normalizedCorruption)
	{
		RefreshVisuals(currentCorruption);
	}

	void RefreshVisuals(float corruptionValue)
	{
		float clampedCorruption = Mathf.Clamp(corruptionValue, minCorruption, maxCorruption);
		float normalized = Mathf.InverseLerp(minCorruption, maxCorruption, clampedCorruption);

		if (corruptionSlider != null)
		{
			corruptionSlider.gameObject.SetActive(showHudBar);
			corruptionSlider.minValue = minCorruption;
			corruptionSlider.maxValue = maxCorruption;
			corruptionSlider.value = clampedCorruption;
		}

		if (corruptionFillImage != null)
		{
			corruptionFillImage.gameObject.SetActive(showHudBar);
			corruptionFillImage.color = corruptionBarGradient.Evaluate(normalized);
		}

		if (corruptionLabelText != null)
		{
			corruptionLabelText.gameObject.SetActive(showHudBar);
			corruptionLabelText.text = "Corruption";
		}

		if (corruptionValueText != null)
		{
			corruptionValueText.gameObject.SetActive(showHudBar);
			corruptionValueText.text = Mathf.RoundToInt(clampedCorruption) + "/" + Mathf.RoundToInt(maxCorruption);
		}

		if (runtimeHudRoot != null)
		{
			runtimeHudRoot.gameObject.SetActive(showHudBar);
		}
	}

	void TryCreateHudIfNeeded()
	{
		if (!autoCreateHudIfMissing || corruptionSlider != null)
		{
			return;
		}

		RectTransform parent = hudParent != null ? hudParent : GetOrCreateHudParent();
		runtimeHudRoot = CreateHudRoot(parent);

		corruptionLabelText = CreateLabel(runtimeHudRoot, "Corruption", new Vector2(0f, -8f), TextAlignmentOptions.Left);
		corruptionValueText = CreateLabel(runtimeHudRoot, "0", new Vector2(-8f, -8f), TextAlignmentOptions.Right);

		corruptionSlider = CreateSlider(runtimeHudRoot);
		if (corruptionSlider.fillRect != null)
		{
			corruptionFillImage = corruptionSlider.fillRect.GetComponent<Image>();
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
		GameObject hudRootObject = new GameObject("Corruption HUD", typeof(RectTransform));
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
		GameObject sliderObject = new GameObject("Corruption Slider", typeof(RectTransform), typeof(Slider));
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
		backgroundImage.sprite = corruptionBarBackgroundSprite;
		backgroundImage.type = corruptionBarBackgroundSprite != null ? Image.Type.Sliced : Image.Type.Simple;
		backgroundImage.color = new Color(0f, 0f, 0f, 0.5f);

		GameObject fillAreaObject = new GameObject("Fill Area", typeof(RectTransform));
		fillAreaObject.transform.SetParent(sliderObject.transform, false);
		RectTransform fillAreaRect = fillAreaObject.GetComponent<RectTransform>();
		fillAreaRect.anchorMin = new Vector2(0f, 0f);
		fillAreaRect.anchorMax = new Vector2(1f, 1f);
		fillAreaRect.offsetMin = new Vector2(3f, 3f);
		fillAreaRect.offsetMax = new Vector2(-3f, -3f);

		GameObject fillObject = new GameObject("Fill", typeof(RectTransform), typeof(Image));
		fillObject.transform.SetParent(fillAreaObject.transform, false);
		RectTransform fillRect = fillObject.GetComponent<RectTransform>();
		fillRect.anchorMin = new Vector2(0f, 0f);
		fillRect.anchorMax = new Vector2(1f, 1f);
		fillRect.offsetMin = Vector2.zero;
		fillRect.offsetMax = Vector2.zero;

		Image fillImage = fillObject.GetComponent<Image>();
		fillImage.sprite = corruptionBarFillSprite;
		fillImage.type = corruptionBarFillSprite != null ? Image.Type.Sliced : Image.Type.Simple;
		fillImage.color = Color.magenta;

		Slider slider = sliderObject.GetComponent<Slider>();
		slider.direction = Slider.Direction.LeftToRight;
		slider.minValue = minCorruption;
		slider.maxValue = maxCorruption;
		slider.value = minCorruption;
		slider.targetGraphic = fillImage;
		slider.fillRect = fillRect;

		return slider;
	}
}
