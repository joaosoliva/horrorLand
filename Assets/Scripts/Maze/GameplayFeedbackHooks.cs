using UnityEngine;

public class GameplayFeedbackHooks : MonoBehaviour
{
    [Header("Optional References")]
    public CorruptionFeedbackSystem corruptionFeedbackSystem;
    public AudioSource feedbackAudioSource;
    public AudioClip soundboardRecoveryClip;
    public AudioClip lightCalmClip;
    public AudioClip chaseRevealClip;
    public AudioClip sprintNoiseClip;

    void Start()
    {
        if (corruptionFeedbackSystem == null)
        {
            corruptionFeedbackSystem = FindObjectOfType<CorruptionFeedbackSystem>();
        }

        if (feedbackAudioSource == null)
        {
            feedbackAudioSource = GetComponent<AudioSource>();
        }
    }

    void OnEnable()
    {
        HorrorEvents.OnSoundboardUsed += HandleSoundboardUsed;
        HorrorEvents.OnCorruptionChanged += HandleCorruptionChanged;
        HorrorEvents.OnLightSpotUsed += HandleLightSpotUsed;
        HorrorEvents.OnMonsterChaseStarted += HandleMonsterChaseStarted;
        HorrorEvents.OnMonsterLostPlayer += HandleMonsterLostPlayer;
        HorrorEvents.OnNoiseCreated += HandleNoiseCreated;
    }

    void OnDisable()
    {
        HorrorEvents.OnSoundboardUsed -= HandleSoundboardUsed;
        HorrorEvents.OnCorruptionChanged -= HandleCorruptionChanged;
        HorrorEvents.OnLightSpotUsed -= HandleLightSpotUsed;
        HorrorEvents.OnMonsterChaseStarted -= HandleMonsterChaseStarted;
        HorrorEvents.OnMonsterLostPlayer -= HandleMonsterLostPlayer;
        HorrorEvents.OnNoiseCreated -= HandleNoiseCreated;
    }

    void HandleSoundboardUsed()
    {
        Debug.Log("[GameplayFeedbackHooks] Soundboard used: trigger sanity relief feedback and corruption pulse.");
        PlayOneShot(soundboardRecoveryClip);
        corruptionFeedbackSystem?.TriggerTutorialCorruptionBurst(0.7f);
    }

    void HandleCorruptionChanged(float current, float normalized)
    {
        if (normalized >= 0.35f)
        {
            Debug.Log("[GameplayFeedbackHooks] Corruption rising: glitch/audio distortion feedback active.");
        }
    }

    void HandleLightSpotUsed()
    {
        Debug.Log("[GameplayFeedbackHooks] Light used: calm/stability feedback triggered.");
        PlayOneShot(lightCalmClip);
    }

    void HandleMonsterChaseStarted()
    {
        Debug.Log("[GameplayFeedbackHooks] Monster reveal/chase feedback triggered.");
        PlayOneShot(chaseRevealClip);
    }

    void HandleMonsterLostPlayer()
    {
        Debug.Log("[GameplayFeedbackHooks] Monster lost player: relief feedback triggered.");
    }

    void HandleNoiseCreated(float loudness, string sourceTag)
    {
        if (sourceTag == "Sprint")
        {
            Debug.Log("[GameplayFeedbackHooks] Sprint noise feedback triggered.");
            PlayOneShot(sprintNoiseClip);
        }
    }

    void PlayOneShot(AudioClip clip)
    {
        if (feedbackAudioSource != null && clip != null)
        {
            feedbackAudioSource.PlayOneShot(clip);
        }
    }
}
