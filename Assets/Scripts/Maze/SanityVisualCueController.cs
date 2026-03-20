using System.Reflection;
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

	[Header("Optional HUD Bar")]
	public bool showHudBar = true;
	public Slider sanitySlider;
	public Image sanityFillImage;
	public Gradient sanityBarGradient;

	[Header("Optional Vignette Overlay")]
	public bool showVignetteOverlay = true;
	public CanvasGroup vignetteCanvasGroup;
	public Image vignetteImage;
	public Color vignetteColor = new Color(0.35f, 0f, 0f, 0.8f);
	public AnimationCurve vignetteStrengthByStress = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

	private PropertyInfo sanityProperty;
	private FieldInfo sanityField;

	void Start()
	{
		CacheSanityMembers();
		RefreshVisuals(TryReadSanityNormalized(out float normalizedSanity) ? normalizedSanity : 1f);
	}

	void Update()
	{
		if (!TryReadSanityNormalized(out float normalizedSanity))
		{
			return;
		}

		RefreshVisuals(normalizedSanity);
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

	bool TryReadSanityNormalized(out float normalizedSanity)
	{
		normalizedSanity = 1f;
		if (sanitySource == null)
		{
			return false;
		}

		if (sanityProperty != null)
		{
			object value = sanityProperty.GetValue(sanitySource, null);
			if (TryConvertToFloat(value, out float sanityValue))
			{
				normalizedSanity = Mathf.InverseLerp(minSanity, maxSanity, sanityValue);
				return true;
			}
		}

		if (sanityField != null)
		{
			object value = sanityField.GetValue(sanitySource);
			if (TryConvertToFloat(value, out float sanityValue))
			{
				normalizedSanity = Mathf.InverseLerp(minSanity, maxSanity, sanityValue);
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

	void RefreshVisuals(float normalizedSanity)
	{
		float clampedSanity = Mathf.Clamp01(normalizedSanity);
		float stress = 1f - clampedSanity;

		if (sanitySlider != null)
		{
			sanitySlider.gameObject.SetActive(showHudBar);
			sanitySlider.normalizedValue = clampedSanity;
		}

		if (sanityFillImage != null)
		{
			sanityFillImage.gameObject.SetActive(showHudBar);
			sanityFillImage.color = sanityBarGradient.Evaluate(clampedSanity);
		}

		if (vignetteImage != null)
		{
			vignetteImage.gameObject.SetActive(showVignetteOverlay);
			vignetteImage.color = vignetteColor;
		}

		if (vignetteCanvasGroup != null)
		{
			vignetteCanvasGroup.gameObject.SetActive(showVignetteOverlay);
			vignetteCanvasGroup.alpha = showVignetteOverlay ? vignetteStrengthByStress.Evaluate(stress) * vignetteColor.a : 0f;
		}
	}
}
