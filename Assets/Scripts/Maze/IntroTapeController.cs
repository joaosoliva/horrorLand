using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using HorrorLand.MenuSystem;

public class IntroTapeController : MonoBehaviour
{
    public enum TutorialStep
    {
        StartDarkRoom,
        CollectSoundboard,
        UseSoundboard,
        ShowCorruption,
        UseLightSpot,
        IntroduceMonster,
        HideFromMonster,
        TeachSprintRisk,
        ExitToMainMaze,
        Completed
    }

    [Serializable]
    public class TutorialObjective
    {
        public TutorialStep step;
        public string objectiveText;
        public float timeoutSeconds = 12f;
        public float retryHintIntervalSeconds = 4f;
        public string[] completionEventKeys = Array.Empty<string>();
    }

    [Header("Flow")]
    public TutorialStep currentStep = TutorialStep.StartDarkRoom;
    public string mainMazeSceneName = "SampleScene";
    public float introDelaySeconds = 1.2f;
    public float lightUseGraceSeconds = 1.25f;
    public float corruptionDemoMinSeconds = 1f;
    public bool enableDebugLogs = true;

    [Header("Gate References")]
    public GameObject soundboardDoorGate;
    public GameObject lightDoorGate;
    public GameObject chaseGate;
    public GameObject tutorialExitGate;
    public SoundboardPickup soundboardPickup;
    public SafeSpaceZone tutorialLightSpot;
    public VillainAI villainAI;
    public MazeExitDoor exitDoor;
    public GuidedIntroMazeGenerator layoutGenerator;

    [Header("Encounter Suppression")]
    public EncounterManager encounterManager;
    public bool disablePreChaseEncountersDuringTutorial = true;

    [Header("Objectives")]
    public List<TutorialObjective> objectiveDefinitions = new List<TutorialObjective>();

    private readonly Dictionary<TutorialStep, TutorialObjective> objectiveByStep = new Dictionary<TutorialStep, TutorialObjective>();
    private readonly HashSet<TutorialStep> completedSteps = new HashSet<TutorialStep>();
    private readonly Dictionary<string, int> eventCounts = new Dictionary<string, int>();
    private float stepStartTime;
    private float nextRetryHintAt;
    private float latestLightUseAt = -999f;
    private float corruptionAtStepEnter;

    public TutorialStep CurrentStep => currentStep;

    void Start()
    {
        ResolveReferences();

        if (layoutGenerator != null)
        {
            TutorialLayoutContext generated = layoutGenerator.GenerateLayout();
            ApplyGeneratedLayout(generated);
        }

        ValidateSceneWiring();
        BuildObjectivesIfMissing();
        CacheObjectiveLookup();

        HorrorEvents.RaiseTutorialStarted();
        EnterStep(TutorialStep.StartDarkRoom, "Tutorial started");
    }

    void OnEnable()
    {
        HorrorEvents.OnSoundboardCollected += OnSoundboardCollected;
        HorrorEvents.OnSoundboardUsed += OnSoundboardUsed;
        HorrorEvents.OnCorruptionChanged += OnCorruptionChanged;
        HorrorEvents.OnLightSpotUsed += OnLightSpotUsed;
        HorrorEvents.OnMonsterChaseStarted += OnMonsterChaseStarted;
        HorrorEvents.OnMonsterLostPlayer += OnMonsterLostPlayer;
        HorrorEvents.OnSprintStarted += OnSprintStarted;
        HorrorEvents.OnNoiseCreated += OnNoiseCreated;
        HorrorEvents.OnExitUnlocked += OnExitUnlocked;
    }

    void OnDisable()
    {
        HorrorEvents.OnSoundboardCollected -= OnSoundboardCollected;
        HorrorEvents.OnSoundboardUsed -= OnSoundboardUsed;
        HorrorEvents.OnCorruptionChanged -= OnCorruptionChanged;
        HorrorEvents.OnLightSpotUsed -= OnLightSpotUsed;
        HorrorEvents.OnMonsterChaseStarted -= OnMonsterChaseStarted;
        HorrorEvents.OnMonsterLostPlayer -= OnMonsterLostPlayer;
        HorrorEvents.OnSprintStarted -= OnSprintStarted;
        HorrorEvents.OnNoiseCreated -= OnNoiseCreated;
        HorrorEvents.OnExitUnlocked -= OnExitUnlocked;
    }

    void Update()
    {
        if (currentStep == TutorialStep.Completed)
        {
            return;
        }

        TutorialObjective objective = GetCurrentObjective();
        if (objective == null)
        {
            return;
        }

        if (Time.time >= nextRetryHintAt)
        {
            LogStep($"Step {currentStep} waiting for event [{string.Join(",", objective.completionEventKeys)}]");
            GameplayHintController.Instance?.PushHint($"tutorial_{currentStep}_retry", objective.objectiveText, 2.8f, HintPriority.High, objective.retryHintIntervalSeconds);
            nextRetryHintAt = Time.time + Mathf.Max(0.5f, objective.retryHintIntervalSeconds);
        }

        if (objective.timeoutSeconds > 0f && Time.time - stepStartTime >= objective.timeoutSeconds)
        {
            LogStep($"Step {currentStep} timeout fallback triggered");
            ApplyTimeoutFallback(currentStep);
            stepStartTime = Time.time;
        }

        if (currentStep == TutorialStep.StartDarkRoom && Time.time - stepStartTime >= introDelaySeconds)
        {
            CompleteCurrentStep("Intro delay elapsed");
        }

        if (currentStep == TutorialStep.ShowCorruption)
        {
            bool sawIncrease = GetEventCount("corruption_increase") > 0;
            bool heldLongEnough = Time.time - stepStartTime >= corruptionDemoMinSeconds;
            if (sawIncrease && heldLongEnough)
            {
                CompleteCurrentStep("Corruption increase observed");
            }
        }

        if (currentStep == TutorialStep.UseLightSpot)
        {
            bool usedLightLongEnough = GetEventCount("light_used") > 0 && Time.time - latestLightUseAt >= lightUseGraceSeconds;
            if (usedLightLongEnough)
            {
                CompleteCurrentStep("Light used for required duration");
            }
        }
    }

    void ResolveReferences()
    {
        if (villainAI == null)
        {
            villainAI = FindObjectOfType<VillainAI>();
        }

        if (exitDoor == null)
        {
            exitDoor = FindObjectOfType<MazeExitDoor>();
        }

        if (tutorialLightSpot == null)
        {
            tutorialLightSpot = FindObjectOfType<SafeSpaceZone>();
        }

        if (soundboardPickup == null)
        {
            soundboardPickup = FindObjectOfType<SoundboardPickup>();
        }

        if (layoutGenerator == null)
        {
            layoutGenerator = FindObjectOfType<GuidedIntroMazeGenerator>();
        }

        if (encounterManager == null)
        {
            encounterManager = FindObjectOfType<EncounterManager>();
        }
    }

    void ValidateSceneWiring()
    {
        ValidateRef(soundboardPickup, "Soundboard pickup reference");
        ValidateRef(tutorialLightSpot, "Tutorial light spot reference");
        ValidateRef(villainAI, "Monster/Villain reference");
        ValidateRef(GameplayHintController.Instance, "UI hint controller reference");
        ValidateRef(exitDoor, "Exit door reference");
    }

    void ValidateRef(UnityEngine.Object reference, string label)
    {
        if (reference != null)
        {
            return;
        }

        Debug.LogError($"[IntroTapeController] Missing required reference: {label}. Attempting safe fallback.");
    }

    void BuildObjectivesIfMissing()
    {
        if (objectiveDefinitions != null && objectiveDefinitions.Count > 0)
        {
            return;
        }

        objectiveDefinitions = new List<TutorialObjective>
        {
            new TutorialObjective { step = TutorialStep.StartDarkRoom, objectiveText = "Your thoughts are slipping.", timeoutSeconds = 8f, retryHintIntervalSeconds = 4f, completionEventKeys = new []{"auto_intro_done"} },
            new TutorialObjective { step = TutorialStep.CollectSoundboard, objectiveText = "Take the Soundboard.", timeoutSeconds = 24f, retryHintIntervalSeconds = 4f, completionEventKeys = new []{"soundboard_collected"} },
            new TutorialObjective { step = TutorialStep.UseSoundboard, objectiveText = "Use Soundboard [Q] to recover sanity.", timeoutSeconds = 18f, retryHintIntervalSeconds = 4f, completionEventKeys = new []{"soundboard_used"} },
            new TutorialObjective { step = TutorialStep.ShowCorruption, objectiveText = "Corruption makes the maze unstable.", timeoutSeconds = 12f, retryHintIntervalSeconds = 4f, completionEventKeys = new []{"corruption_increase"} },
            new TutorialObjective { step = TutorialStep.UseLightSpot, objectiveText = "Hold E — Hide in Light.", timeoutSeconds = 20f, retryHintIntervalSeconds = 4f, completionEventKeys = new []{"light_used"} },
            new TutorialObjective { step = TutorialStep.IntroduceMonster, objectiveText = "Do not let it see you.", timeoutSeconds = 10f, retryHintIntervalSeconds = 4f, completionEventKeys = new []{"monster_chase_started"} },
            new TutorialObjective { step = TutorialStep.HideFromMonster, objectiveText = "Hide in Light to break the chase.", timeoutSeconds = 20f, retryHintIntervalSeconds = 4f, completionEventKeys = new []{"monster_lost_player", "light_used"} },
            new TutorialObjective { step = TutorialStep.TeachSprintRisk, objectiveText = "Running is loud.", timeoutSeconds = 12f, retryHintIntervalSeconds = 3f, completionEventKeys = new []{"sprint_started", "noise_created"} },
            new TutorialObjective { step = TutorialStep.ExitToMainMaze, objectiveText = "Reach the exit.", timeoutSeconds = 30f, retryHintIntervalSeconds = 4f, completionEventKeys = new []{"exit_unlocked"} },
        };
    }

    void CacheObjectiveLookup()
    {
        objectiveByStep.Clear();
        foreach (TutorialObjective objective in objectiveDefinitions)
        {
            objectiveByStep[objective.step] = objective;
        }
    }

    TutorialObjective GetCurrentObjective()
    {
        objectiveByStep.TryGetValue(currentStep, out TutorialObjective objective);
        return objective;
    }

    void EnterStep(TutorialStep nextStep, string reason)
    {
        currentStep = nextStep;
        stepStartTime = Time.time;
        nextRetryHintAt = Time.time + 0.25f;

        CorruptionSystem corruption = FindObjectOfType<CorruptionSystem>();
        corruptionAtStepEnter = corruption != null ? corruption.CurrentCorruption : 0f;

        LogStep($"Enter {nextStep}. Reason: {reason}");

        TutorialObjective objective = GetCurrentObjective();
        if (objective != null)
        {
            GameplayHintController.Instance?.PushHint($"tutorial_{nextStep}", objective.objectiveText, 3f, HintPriority.High, 0.8f);
        }

        ConfigureStepGates(nextStep);

        if (disablePreChaseEncountersDuringTutorial && encounterManager != null)
        {
            bool allow = nextStep == TutorialStep.IntroduceMonster || nextStep == TutorialStep.HideFromMonster || nextStep == TutorialStep.TeachSprintRisk;
            encounterManager.SetPreChaseEnabled(allow);
        }

        if (nextStep == TutorialStep.IntroduceMonster && villainAI != null)
        {
            villainAI.ForceChase();
        }

        if (nextStep == TutorialStep.Completed)
        {
            PlayerPrefs.SetInt(MenuPrefsKeys.TutorialCompleted, 1);
            PlayerPrefs.SetInt(MenuPrefsKeys.ForceTutorialReplay, 0);
            PlayerPrefs.Save();
            HorrorEvents.RaiseTutorialCompleted();
            RuntimeStatsTracker.Instance?.PrintGameplayTrainingReport();
            if (disablePreChaseEncountersDuringTutorial && encounterManager != null)
            {
                encounterManager.SetPreChaseEnabled(true);
            }
            if (!string.IsNullOrWhiteSpace(mainMazeSceneName))
            {
                SceneManager.LoadScene(mainMazeSceneName);
            }
        }
    }

    void CompleteCurrentStep(string reason)
    {
        if (!completedSteps.Contains(currentStep))
        {
            completedSteps.Add(currentStep);
        }

        TutorialStep next = GetNextStep(currentStep);
        LogStep($"Exit {currentStep}. Reason: {reason}. Next={next}");
        EnterStep(next, reason);
    }

    TutorialStep GetNextStep(TutorialStep step)
    {
        if (step == TutorialStep.StartDarkRoom) return TutorialStep.CollectSoundboard;
        if (step == TutorialStep.CollectSoundboard) return TutorialStep.UseSoundboard;
        if (step == TutorialStep.UseSoundboard) return TutorialStep.ShowCorruption;
        if (step == TutorialStep.ShowCorruption) return TutorialStep.UseLightSpot;
        if (step == TutorialStep.UseLightSpot) return TutorialStep.IntroduceMonster;
        if (step == TutorialStep.IntroduceMonster) return TutorialStep.HideFromMonster;
        if (step == TutorialStep.HideFromMonster) return TutorialStep.TeachSprintRisk;
        if (step == TutorialStep.TeachSprintRisk) return TutorialStep.ExitToMainMaze;
        if (step == TutorialStep.ExitToMainMaze) return TutorialStep.Completed;
        return TutorialStep.Completed;
    }

    void ConfigureStepGates(TutorialStep step)
    {
        SetGateActive(soundboardDoorGate, step == TutorialStep.StartDarkRoom || step == TutorialStep.CollectSoundboard || step == TutorialStep.UseSoundboard);
        SetGateActive(lightDoorGate, step == TutorialStep.StartDarkRoom || step == TutorialStep.CollectSoundboard || step == TutorialStep.UseSoundboard || step == TutorialStep.ShowCorruption);
        SetGateActive(chaseGate, step != TutorialStep.IntroduceMonster && step != TutorialStep.HideFromMonster && step != TutorialStep.TeachSprintRisk && step != TutorialStep.ExitToMainMaze && step != TutorialStep.Completed);

        bool blockExit = step != TutorialStep.ExitToMainMaze && step != TutorialStep.Completed;
        SetGateActive(tutorialExitGate, blockExit);
    }

    void SetGateActive(GameObject gate, bool active)
    {
        if (gate != null)
        {
            gate.SetActive(active);
        }
    }

    void ApplyTimeoutFallback(TutorialStep step)
    {
        if (step == TutorialStep.CollectSoundboard)
        {
            GameplayHintController.Instance?.PushHint("tutorial_collect_soundboard_timeout", "The Soundboard is your emergency focus tool.", 3f, HintPriority.Critical, 2f);
            SetGateActive(soundboardDoorGate, true);
        }
        else if (step == TutorialStep.UseSoundboard)
        {
            GameplayHintController.Instance?.PushHint("tutorial_use_soundboard_timeout", "Use Soundboard [Q] now.", 3f, HintPriority.Critical, 2f);
        }
        else if (step == TutorialStep.ShowCorruption)
        {
            HorrorEvents.RaiseCorruptionEventTriggered("IntenseVisualGlitch", 0.75f);
        }
        else if (step == TutorialStep.UseLightSpot)
        {
            GameplayHintController.Instance?.PushHint("tutorial_use_light_timeout", "Light restores sanity and hides you.", 3f, HintPriority.Critical, 2f);
            SetGateActive(lightDoorGate, false);
        }
        else if (step == TutorialStep.HideFromMonster)
        {
            GameplayHintController.Instance?.PushHint("tutorial_hide_timeout", "Hide in Light to break the chase.", 3f, HintPriority.Critical, 2f);
        }
        else if (step == TutorialStep.TeachSprintRisk)
        {
            GameplayHintController.Instance?.PushHint("tutorial_sprint_timeout", "Running is loud.", 3f, HintPriority.High, 2f);
            RegisterEvent("noise_created");
            TryCompleteByEvent("noise_created");
        }
        else if (step == TutorialStep.ExitToMainMaze)
        {
            bool allPriorCompleted = completedSteps.Contains(TutorialStep.TeachSprintRisk);
            if (!allPriorCompleted)
            {
                SetGateActive(tutorialExitGate, true);
            }
        }
    }

    void RegisterEvent(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        if (!eventCounts.ContainsKey(key))
        {
            eventCounts[key] = 0;
        }

        eventCounts[key]++;
        TryCompleteByEvent(key);
    }

    int GetEventCount(string key)
    {
        return eventCounts.TryGetValue(key, out int count) ? count : 0;
    }

    void TryCompleteByEvent(string eventKey)
    {
        TutorialObjective objective = GetCurrentObjective();
        if (objective == null || objective.completionEventKeys == null)
        {
            return;
        }

        for (int i = 0; i < objective.completionEventKeys.Length; i++)
        {
            string completionKey = objective.completionEventKeys[i];
            if (string.Equals(completionKey, eventKey, StringComparison.OrdinalIgnoreCase))
            {
                if (currentStep == TutorialStep.UseLightSpot && Time.time - latestLightUseAt < lightUseGraceSeconds)
                {
                    return;
                }

                if (currentStep == TutorialStep.ShowCorruption)
                {
                    CorruptionSystem corruption = FindObjectOfType<CorruptionSystem>();
                    float currentCorruption = corruption != null ? corruption.CurrentCorruption : corruptionAtStepEnter;
                    if (currentCorruption <= corruptionAtStepEnter)
                    {
                        return;
                    }
                }

                CompleteCurrentStep("Event " + eventKey + " satisfied objective");
                return;
            }
        }
    }


    public void ApplyGeneratedLayout(TutorialLayoutContext context)
    {
        if (context == null)
        {
            return;
        }

        if (context.soundboardPickup != null)
        {
            soundboardPickup = context.soundboardPickup;
        }

        if (context.soundboardGate != null)
        {
            soundboardDoorGate = context.soundboardGate;
        }

        if (context.lightGate != null)
        {
            lightDoorGate = context.lightGate;
        }

        if (context.chaseGate != null)
        {
            chaseGate = context.chaseGate;
        }

        if (context.firstLightSpot != null)
        {
            tutorialLightSpot = context.firstLightSpot;
        }


        if (context.tutorialExitGate != null)
        {
            tutorialExitGate = context.tutorialExitGate;
        }

        if (context.mainMazeConnector != null)
        {
            mainMazeSceneName = string.IsNullOrWhiteSpace(mainMazeSceneName) ? "SampleScene" : mainMazeSceneName;
        }

        if (context.monsterSpawnPoint != null && villainAI != null)
        {
            villainAI.transform.position = context.monsterSpawnPoint.position;
        }

        LogStep("Applied generated tutorial layout context.");
    }

    void LogStep(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log("[IntroTapeController] " + message);
        }
    }

    void OnSoundboardCollected() => RegisterEvent("soundboard_collected");
    void OnSoundboardUsed() => RegisterEvent("soundboard_used");
    void OnCorruptionChanged(float current, float normalized)
    {
        if (current > corruptionAtStepEnter)
        {
            RegisterEvent("corruption_increase");
        }
    }

    void OnLightSpotUsed()
    {
        latestLightUseAt = Time.time;
        RegisterEvent("light_used");
    }

    void OnMonsterChaseStarted() => RegisterEvent("monster_chase_started");
    void OnMonsterLostPlayer() => RegisterEvent("monster_lost_player");
    void OnSprintStarted() => RegisterEvent("sprint_started");
    void OnNoiseCreated(float loudness, string sourceTag) => RegisterEvent("noise_created");
    void OnExitUnlocked() => RegisterEvent("exit_unlocked");
}
