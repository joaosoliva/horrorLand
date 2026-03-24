using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Light))]
public class ProceduralLamp : MonoBehaviour, ILightInteractable
{
	[Header("Lamp Identity")]
	public string lightId;
	public string zoneId;

	[Header("Lamp Tuning")]
	public float defaultFlickerDuration = 0.7f;
	public float flickerInterval = 0.06f;
	public float minIntensityMultiplier = 0.25f;
	public float maxIntensityMultiplier = 1f;

	public string LightId => lightId;
	public bool IsOn => lamp != null && lamp.enabled;

	private Light lamp;
	private float baseIntensity = 1f;
	private Coroutine activeRoutine;

	void Awake()
	{
		lamp = GetComponent<Light>();
		if (lamp != null)
		{
			baseIntensity = lamp.intensity;
		}

		if (string.IsNullOrEmpty(lightId))
		{
			lightId = gameObject.name;
		}
	}

	public void TriggerFlicker(float duration)
	{
		if (lamp == null)
		{
			return;
		}

		if (activeRoutine != null)
		{
			StopCoroutine(activeRoutine);
		}
		activeRoutine = StartCoroutine(FlickerRoutine(duration <= 0f ? defaultFlickerDuration : duration));
	}

	public void TurnOff(float duration = 0f)
	{
		if (lamp == null)
		{
			return;
		}

		if (activeRoutine != null)
		{
			StopCoroutine(activeRoutine);
		}

		lamp.enabled = false;
		if (duration > 0f)
		{
			activeRoutine = StartCoroutine(ReEnableAfterDelay(duration));
		}
	}

	public void DelayedActivate(float delaySeconds)
	{
		if (lamp == null)
		{
			return;
		}

		if (activeRoutine != null)
		{
			StopCoroutine(activeRoutine);
		}
		activeRoutine = StartCoroutine(DelayedEnableRoutine(Mathf.Max(0f, delaySeconds)));
	}

	IEnumerator FlickerRoutine(float duration)
	{
		float elapsed = 0f;
		while (elapsed < duration)
		{
			float intensityScale = Random.Range(minIntensityMultiplier, maxIntensityMultiplier);
			lamp.enabled = Random.value > 0.2f;
			lamp.intensity = baseIntensity * intensityScale;
			yield return new WaitForSeconds(Mathf.Max(0.01f, flickerInterval));
			elapsed += flickerInterval;
		}

		lamp.enabled = true;
		lamp.intensity = baseIntensity;
		activeRoutine = null;
	}

	IEnumerator ReEnableAfterDelay(float delaySeconds)
	{
		yield return new WaitForSeconds(delaySeconds);
		lamp.enabled = true;
		lamp.intensity = baseIntensity;
		activeRoutine = null;
	}

	IEnumerator DelayedEnableRoutine(float delaySeconds)
	{
		lamp.enabled = false;
		yield return new WaitForSeconds(delaySeconds);
		lamp.enabled = true;
		lamp.intensity = baseIntensity;
		activeRoutine = null;
	}
}
