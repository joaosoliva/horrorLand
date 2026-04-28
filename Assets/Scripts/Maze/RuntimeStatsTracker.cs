using System.Collections.Generic;
using UnityEngine;

public class RuntimeStatsTracker : MonoBehaviour
{
    public static RuntimeStatsTracker Instance;

    public int soundboardUseCount;
    public int lightSpotUseCount;
    public int monsterChaseCount;
    public int successfulLightHides;
    public int exitInteractionFailures;
    public int soundboardCollectedCount;
    public float tutorialStartedAt = -1f;
    public float tutorialCompletedAt = -1f;
    public float firstSoundboardUseAt = -1f;
    public float firstLightUseAt = -1f;
    public float sprintDurationTotal;
    public float longestSprintDuration;
    public float lowSanityTime;
    public float criticalSanityTime;
    public float highCorruptionTime;
    public string deathCause = "Unknown";

    public float LongestSprintDuration => longestSprintDuration;
    public IReadOnlyCollection<string> TriggeredHintIds => triggeredHintIds;

    private bool isSprinting;
    private float sprintStartedAt = -999f;
    private bool lowSanity;
    private bool criticalSanity;
    private bool highCorruption;
    private readonly HashSet<string> triggeredHintIds = new HashSet<string>();

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    void OnEnable()
    {
        HorrorEvents.OnTutorialStarted += HandleTutorialStarted;
        HorrorEvents.OnTutorialCompleted += HandleTutorialCompleted;
        HorrorEvents.OnSoundboardCollected += HandleSoundboardCollected;
        HorrorEvents.OnSoundboardUsed += HandleSoundboardUsed;
        HorrorEvents.OnLightSpotUsed += HandleLightSpotUsed;
        HorrorEvents.OnMonsterChaseStarted += HandleMonsterChaseStarted;
        HorrorEvents.OnMonsterLostPlayer += HandleMonsterLostPlayer;
        HorrorEvents.OnSprintStarted += HandleSprintStarted;
        HorrorEvents.OnSprintStopped += HandleSprintStopped;
        HorrorEvents.OnSanityLow += HandleSanityLow;
        HorrorEvents.OnSanityCritical += HandleSanityCritical;
        HorrorEvents.OnSanityChanged += HandleSanityChanged;
        HorrorEvents.OnCorruptionHigh += HandleCorruptionHigh;
        HorrorEvents.OnCorruptionChanged += HandleCorruptionChanged;
        HorrorEvents.OnPlayerDeath += HandlePlayerDeath;
        HorrorEvents.OnExitInteractionFailed += HandleExitFailed;
    }

    void OnDisable()
    {
        HorrorEvents.OnTutorialStarted -= HandleTutorialStarted;
        HorrorEvents.OnTutorialCompleted -= HandleTutorialCompleted;
        HorrorEvents.OnSoundboardCollected -= HandleSoundboardCollected;
        HorrorEvents.OnSoundboardUsed -= HandleSoundboardUsed;
        HorrorEvents.OnLightSpotUsed -= HandleLightSpotUsed;
        HorrorEvents.OnMonsterChaseStarted -= HandleMonsterChaseStarted;
        HorrorEvents.OnMonsterLostPlayer -= HandleMonsterLostPlayer;
        HorrorEvents.OnSprintStarted -= HandleSprintStarted;
        HorrorEvents.OnSprintStopped -= HandleSprintStopped;
        HorrorEvents.OnSanityLow -= HandleSanityLow;
        HorrorEvents.OnSanityCritical -= HandleSanityCritical;
        HorrorEvents.OnSanityChanged -= HandleSanityChanged;
        HorrorEvents.OnCorruptionHigh -= HandleCorruptionHigh;
        HorrorEvents.OnCorruptionChanged -= HandleCorruptionChanged;
        HorrorEvents.OnPlayerDeath -= HandlePlayerDeath;
        HorrorEvents.OnExitInteractionFailed -= HandleExitFailed;
    }

    void Update()
    {
        if (lowSanity)
        {
            lowSanityTime += Time.deltaTime;
        }
        if (criticalSanity)
        {
            criticalSanityTime += Time.deltaTime;
        }
        if (highCorruption)
        {
            highCorruptionTime += Time.deltaTime;
        }
    }

    public void RegisterHintTriggered(string hintId)
    {
        if (!string.IsNullOrWhiteSpace(hintId))
        {
            triggeredHintIds.Add(hintId);
        }
    }

    public string BuildDeathTip(float normalizedSanity, float normalizedCorruption)
    {
        if (normalizedSanity <= 0.2f)
        {
            return "You lost control. Use the soundboard sooner.";
        }
        if (normalizedCorruption >= 0.7f)
        {
            return "The maze became unstable. Light can stabilize it.";
        }
        if (longestSprintDuration >= 2.5f)
        {
            return "The monster heard you running.";
        }
        if (lightSpotUseCount == 0)
        {
            return "Light spots can break a chase.";
        }
        if (soundboardUseCount == 0)
        {
            return "The Soundboard can restore sanity in emergencies.";
        }

        return "Balance sound, light, and movement to survive the maze.";
    }

    public void PrintGameplayTrainingReport()
    {
        bool usedSoundboard = soundboardUseCount > 0;
        bool usedLight = lightSpotUseCount > 0;
        bool oversprinted = longestSprintDuration >= 2.5f;
        bool exitBlocked = exitInteractionFailures > 0;
        string hintList = triggeredHintIds.Count == 0 ? "None" : string.Join(", ", triggeredHintIds);

        Debug.Log(
            "[RuntimeStatsTracker] Training Report\n" +
            $"Used Soundboard: {usedSoundboard} (count={soundboardUseCount})\n" +
            $"Used Light: {usedLight} (count={lightSpotUseCount})\n" +
            $"Over-sprinted: {oversprinted} (longest={longestSprintDuration:F2}s)\n" +
            $"Monster Chases: {monsterChaseCount}\n" +
            $"Exit Blocked Correctly: {exitBlocked} (failures={exitInteractionFailures})\n" +
            $"Hints Triggered: {hintList}\n" +
            $"Death Cause: {deathCause}");
    }

    private void HandleTutorialStarted() => tutorialStartedAt = Time.time;
    private void HandleTutorialCompleted() => tutorialCompletedAt = Time.time;
    private void HandleSoundboardCollected() => soundboardCollectedCount++;

    private void HandleSoundboardUsed()
    {
        soundboardUseCount++;
        if (firstSoundboardUseAt < 0f)
        {
            firstSoundboardUseAt = Time.time;
        }
    }

    private void HandleLightSpotUsed()
    {
        lightSpotUseCount++;
        if (firstLightUseAt < 0f)
        {
            firstLightUseAt = Time.time;
        }
    }

    private void HandleMonsterChaseStarted() => monsterChaseCount++;

    private void HandleMonsterLostPlayer()
    {
        if (lightSpotUseCount > 0)
        {
            successfulLightHides++;
        }
    }

    private void HandleSprintStarted()
    {
        isSprinting = true;
        sprintStartedAt = Time.time;
    }

    private void HandleSprintStopped()
    {
        if (!isSprinting)
        {
            return;
        }

        isSprinting = false;
        float duration = Mathf.Max(0f, Time.time - sprintStartedAt);
        sprintDurationTotal += duration;
        longestSprintDuration = Mathf.Max(longestSprintDuration, duration);
    }

    private void HandleSanityLow() => lowSanity = true;
    private void HandleSanityCritical() => criticalSanity = true;

    private void HandleSanityChanged(float sanity, float normalized, float stress)
    {
        lowSanity = normalized <= 0.3f;
        criticalSanity = normalized <= 0.15f;
    }

    private void HandleCorruptionHigh() => highCorruption = true;
    private void HandleCorruptionChanged(float current, float normalized) => highCorruption = normalized >= 0.65f;

    private void HandlePlayerDeath(string cause)
    {
        deathCause = cause;
        Debug.Log($"Telemetry death cause={cause} uses(soundboard={soundboardUseCount}, light={lightSpotUseCount}) sprintLongest={longestSprintDuration:F2}s");
    }

    private void HandleExitFailed(string reason) => exitInteractionFailures++;
}
