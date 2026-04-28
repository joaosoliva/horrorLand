using UnityEngine;

public class PlayerSprintSystem : MonoBehaviour
{
    [Header("References")]
    public SC_FPSController playerController;

    [Header("Stamina")]
    public float maxStamina = 100f;
    public float staminaDrainPerSecond = 26f;
    public float staminaRecoveryPerSecond = 20f;
    public float recoveryDelay = 0.8f;

    [Header("Noise")]
    [Range(0f, 1f)] public float sprintNoiseLoudness = 0.55f;
    public float sprintNoiseInterval = 0.4f;
    public float longSprintWarningSeconds = 2.75f;

    public float CurrentStamina => currentStamina;
    public bool IsSprinting => isSprinting;
    public float LongestSprintDuration => longestSprintDuration;

    private float currentStamina;
    private bool isSprinting;
    private float sprintStartedAt = -999f;
    private float lastSprintTime = -999f;
    private float nextNoiseTime = -999f;
    private float longestSprintDuration;

    void Start()
    {
        if (playerController == null)
        {
            playerController = FindObjectOfType<SC_FPSController>();
        }

        currentStamina = Mathf.Max(1f, maxStamina);
    }

    void Update()
    {
        if (playerController == null)
        {
            return;
        }

        bool wantsSprint = playerController.IsSprinting && currentStamina > 0f;

        if (wantsSprint)
        {
            if (!isSprinting)
            {
                isSprinting = true;
                sprintStartedAt = Time.time;
                HorrorEvents.RaiseSprintStarted();
            }

            currentStamina = Mathf.Max(0f, currentStamina - staminaDrainPerSecond * Time.deltaTime);
            lastSprintTime = Time.time;

            if (Time.time >= nextNoiseTime)
            {
                nextNoiseTime = Time.time + Mathf.Max(0.05f, sprintNoiseInterval);
                HorrorEvents.RaiseNoiseCreated(sprintNoiseLoudness, "Sprint");
            }

            if (Time.time - sprintStartedAt >= longSprintWarningSeconds)
            {
                GameplayHintController.PushGlobalHint("Running is loud.", 1.3f, HintPriority.High);
            }

            if (currentStamina <= 0f)
            {
                playerController.allowSprinting = false;
            }
        }
        else
        {
            if (isSprinting)
            {
                isSprinting = false;
                float sprintDuration = Mathf.Max(0f, Time.time - sprintStartedAt);
                longestSprintDuration = Mathf.Max(longestSprintDuration, sprintDuration);
                HorrorEvents.RaiseSprintStopped();
            }

            if (Time.time - lastSprintTime >= recoveryDelay)
            {
                currentStamina = Mathf.Min(maxStamina, currentStamina + staminaRecoveryPerSecond * Time.deltaTime);
                if (currentStamina > maxStamina * 0.2f)
                {
                    playerController.allowSprinting = true;
                }
            }
        }
    }
}
