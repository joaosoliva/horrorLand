using System.Collections.Generic;
using UnityEngine;

public enum HintPriority
{
    Low,
    Medium,
    High,
    Critical
}

public class GameplayHintController : MonoBehaviour
{
    public static GameplayHintController Instance;

    [Header("Display")]
    public bool drawHints = true;
    public Vector2 anchor = new Vector2(0f, 0.8f);
    public int fontSize = 24;

    [Header("Default Durations")]
    public float lowDuration = 2f;
    public float mediumDuration = 2.5f;
    public float highDuration = 3f;
    public float criticalDuration = 3.5f;

    [Header("Anti-spam")]
    public float defaultHintCooldownSeconds = 2f;

    private string currentHint = string.Empty;
    private float hintUntilTime = -999f;
    private HintPriority currentPriority = HintPriority.Low;
    private GUIStyle style;
    private readonly Dictionary<string, float> nextHintAllowedAtById = new Dictionary<string, float>();
    private readonly HashSet<string> suppressedHintIds = new HashSet<string>();

    public static void PushGlobalHint(string text, float duration, HintPriority priority)
    {
        if (Instance == null || string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        string generatedId = "adhoc_" + text.GetHashCode();
        Instance.PushHint(generatedId, text, duration, priority, 0f);
    }

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
        HorrorEvents.OnSanityLow += HandleSanityLow;
        HorrorEvents.OnSanityCritical += HandleSanityCritical;
        HorrorEvents.OnCorruptionHigh += HandleCorruptionHigh;
        HorrorEvents.OnLightSpotEntered += HandleLightEntered;
        HorrorEvents.OnMonsterChaseStarted += HandleChaseStarted;
        HorrorEvents.OnExitInteractionFailed += HandleExitFailed;
        HorrorEvents.OnSoundboardCollected += HandleSoundboardCollected;
        HorrorEvents.OnSoundboardUsed += HandleSoundboardUsed;
        HorrorEvents.OnLightSpotUsed += HandleLightUsed;
        HorrorEvents.OnSprintStarted += HandleSprintStarted;
    }

    void OnDisable()
    {
        HorrorEvents.OnSanityLow -= HandleSanityLow;
        HorrorEvents.OnSanityCritical -= HandleSanityCritical;
        HorrorEvents.OnCorruptionHigh -= HandleCorruptionHigh;
        HorrorEvents.OnLightSpotEntered -= HandleLightEntered;
        HorrorEvents.OnMonsterChaseStarted -= HandleChaseStarted;
        HorrorEvents.OnExitInteractionFailed -= HandleExitFailed;
        HorrorEvents.OnSoundboardCollected -= HandleSoundboardCollected;
        HorrorEvents.OnSoundboardUsed -= HandleSoundboardUsed;
        HorrorEvents.OnLightSpotUsed -= HandleLightUsed;
        HorrorEvents.OnSprintStarted -= HandleSprintStarted;
    }

    public void SuppressHint(string hintId, bool suppressed)
    {
        if (string.IsNullOrWhiteSpace(hintId))
        {
            return;
        }

        if (suppressed)
        {
            suppressedHintIds.Add(hintId);
        }
        else
        {
            suppressedHintIds.Remove(hintId);
        }
    }

    public void PushHint(string hintId, string text, float duration, HintPriority priority, float cooldownOverride)
    {
        if (string.IsNullOrWhiteSpace(hintId) || string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        if (suppressedHintIds.Contains(hintId))
        {
            return;
        }

        if (nextHintAllowedAtById.TryGetValue(hintId, out float nextAllowedAt) && Time.time < nextAllowedAt)
        {
            return;
        }

        if (priority < currentPriority && Time.time < hintUntilTime)
        {
            return;
        }

        currentHint = text;
        hintUntilTime = Time.time + Mathf.Max(0.4f, duration);
        currentPriority = priority;

        float cooldown = cooldownOverride > 0f ? cooldownOverride : defaultHintCooldownSeconds;
        nextHintAllowedAtById[hintId] = Time.time + cooldown;

        RuntimeStatsTracker.Instance?.RegisterHintTriggered(hintId);
    }

    private void PushHint(string hintId, string text, float duration, HintPriority priority)
    {
        PushHint(hintId, text, duration, priority, defaultHintCooldownSeconds);
    }

    void HandleSanityLow() => PushHint("sanity_low", "Sanity low. Use Soundboard [Q] or find Light.", mediumDuration, HintPriority.High);
    void HandleSanityCritical() => PushHint("sanity_critical", "Critical sanity. Use Soundboard now.", criticalDuration, HintPriority.Critical);
    void HandleCorruptionHigh() => PushHint("corruption_high", "Corruption rising. Find Light.", highDuration, HintPriority.High);
    void HandleLightEntered() => PushHint("near_light", "Hold E — Hide in Light.", lowDuration, HintPriority.Medium);
    void HandleChaseStarted() => PushHint("chase_light", "Hide in Light to break the chase.", highDuration, HintPriority.Critical);
    void HandleExitFailed(string reason) => PushHint("exit_blocked", reason, highDuration, HintPriority.High);

    void HandleSoundboardCollected() => PushHint("soundboard_reminder", "The Soundboard restores sanity, but corrupts the maze.", highDuration, HintPriority.High);
    void HandleSoundboardUsed() => SuppressHint("soundboard_reminder", true);

    void HandleLightUsed()
    {
        PushHint("light_reminder", "Light restores sanity and hides you.", mediumDuration, HintPriority.Medium);
        SuppressHint("near_light", true);
    }

    void HandleSprintStarted() => PushHint("sprint_warning", "Running is loud.", highDuration, HintPriority.High);

    void OnGUI()
    {
        if (!drawHints || Time.time >= hintUntilTime || string.IsNullOrWhiteSpace(currentHint))
        {
            return;
        }

        if (style == null)
        {
            style = new GUIStyle(GUI.skin.box);
            style.fontSize = Mathf.Max(14, fontSize);
            style.alignment = TextAnchor.MiddleCenter;
            style.normal.textColor = Color.white;
            style.wordWrap = true;
        }

        float width = Mathf.Min(780f, Screen.width * 0.75f);
        float x = (Screen.width - width) * 0.5f;
        float y = Screen.height * anchor.y;
        GUI.Box(new Rect(x, y, width, 52f), currentHint, style);
    }
}
