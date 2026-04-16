using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class flashlight : MonoBehaviour
{
    public GameObject light;
    public bool toggle;
    public AudioSource toggleSound;
    [Header("Scare Flicker")]
    public bool allowScareFlicker = true;
    public int scareFlickerBursts = 4;
    public float scareFlickerDuration = 0.32f;
    public float scareFlickerOffIntensity = 0f;
    public float scareFlickerCooldown = 0.4f;

    private Light lightComponent;
    private Coroutine scareFlickerRoutine;
    private float lastScareFlickerTime = -999f;
    private float baseIntensity = 1f;
    private bool isScareFlickering = false;

    void Start()
    {
        if (light != null)
        {
            lightComponent = light.GetComponentInChildren<Light>();
            if (lightComponent != null)
            {
                baseIntensity = lightComponent.intensity;
            }
        }

        ApplyFlashlightState(toggle);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F))
        {
            if (isScareFlickering)
            {
                return;
            }

            toggle = !toggle;
            //toggleSound.Play();
            ApplyFlashlightState(toggle);
        }
    }

    public void TriggerScareFlicker(float delay = 0f)
    {
        if (!allowScareFlicker || light == null)
        {
            return;
        }

        if (Time.time - lastScareFlickerTime < scareFlickerCooldown)
        {
            return;
        }

        if (scareFlickerRoutine != null)
        {
            StopCoroutine(scareFlickerRoutine);
        }

        scareFlickerRoutine = StartCoroutine(ScareFlickerRoutine(Mathf.Max(0f, delay)));
        lastScareFlickerTime = Time.time;
    }

    IEnumerator ScareFlickerRoutine(float delay)
    {
        isScareFlickering = true;
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        bool wasToggleOn = toggle;
        float burstDuration = Mathf.Max(0.04f, scareFlickerDuration / Mathf.Max(1, scareFlickerBursts * 2));

        for (int i = 0; i < scareFlickerBursts; i++)
        {
            SetFlickerLightState(false);
            yield return new WaitForSeconds(burstDuration);
            SetFlickerLightState(true);
            yield return new WaitForSeconds(burstDuration);
        }

        toggle = wasToggleOn;
        ApplyFlashlightState(wasToggleOn);
        if (lightComponent != null)
        {
            lightComponent.intensity = baseIntensity;
        }
        isScareFlickering = false;
    }

    void SetFlickerLightState(bool enabled)
    {
        if (light == null)
        {
            return;
        }

        ApplyFlashlightState(enabled);
        if (lightComponent != null)
        {
            lightComponent.intensity = enabled ? baseIntensity : scareFlickerOffIntensity;
        }
    }

    void ApplyFlashlightState(bool enabled)
    {
        if (light == null)
        {
            return;
        }

        if (lightComponent == null)
        {
            lightComponent = light.GetComponent<Light>();
            if (lightComponent == null)
            {
                lightComponent = light.GetComponentInChildren<Light>();
            }
        }

        if (lightComponent != null)
        {
            lightComponent.enabled = enabled;
            return;
        }

        if (light == gameObject)
        {
            return;
        }

        light.SetActive(enabled);
    }
}
