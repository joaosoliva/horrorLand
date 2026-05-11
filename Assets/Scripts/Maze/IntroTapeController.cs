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
    public float runtimeDependencyTimeoutSeconds = 5f;

    [Header("Gate References")]
    public GameObject soundboardDoorGate;
    public GameObject soundboardUseDoor;
    public GameObject corruptionDoor;
    public GameObject lightDoorGate;
    public GameObject chaseGate;
    public GameObject sprintDoor;
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

    private enum BootstrapStage { Generation, Registration, Validation, RuntimeBinding, TutorialStart }
    private BootstrapStage bootstrapStage = BootstrapStage.Generation;
    private bool hasCompletedRuntimeBinding = false;

    void Awake()
    {
        Debug.Log($"[IntroTapeController] Awake objectiveDefinitions.count={(objectiveDefinitions!=null?objectiveDefinitions.Count:-1)}");
    }

    void Start()
    {
        Debug.Log($"[IntroTapeController] Start objectiveDefinitions.count={(objectiveDefinitions!=null?objectiveDefinitions.Count:-1)}");
        StartCoroutine(BootstrapTutorialRuntime());
    }

    System.Collections.IEnumerator BootstrapTutorialRuntime()
    {
        Debug.Log($"[IntroTapeController] BootstrapTutorialRuntime begin frame={Time.frameCount}");
        bootstrapStage = BootstrapStage.Generation;
        ResolveReferences();

        Debug.Log($"[IntroTapeController] layoutGenerator assigned={(layoutGenerator!=null)}");
        if (layoutGenerator != null)
        {
            TutorialLayoutContext generated = layoutGenerator.GenerateLayout();
            ApplyGeneratedLayout(generated);
        }
        else
        {
            MazeGenerator mazeGenerator = FindObjectOfType<MazeGenerator>();
            if (mazeGenerator != null)
            {
                mazeGenerator.EnsureTutorialGeneratedOnce(TutorialMazeBlueprintFactory.CreateDefaultTutorialBlueprint(), "IntroTapeController.BootstrapTutorialRuntimeFallback");
            }
        }

        bootstrapStage = BootstrapStage.Registration;
        float startedAt = Time.time;
        while (Time.time - startedAt < runtimeDependencyTimeoutSeconds)
        {
            ApplyRegistryReferencesNonDestructive();
            if (AreCoreReferencesResolved())
            {
                break;
            }

            yield return null;
        }

        bootstrapStage = BootstrapStage.Validation;
        TutorialRuntimeRegistry.Instance?.LogRegistrationReport("IntroTapeController bootstrap");
        Debug.Log($"[IntroTapeController] Binding snapshot: soundboardDoorGate={(soundboardDoorGate!=null?soundboardDoorGate.GetInstanceID().ToString():"null")}, exitDoor={(exitDoor!=null?exitDoor.GetInstanceID().ToString():"null")}, lightDoorGate={(lightDoorGate!=null?lightDoorGate.GetInstanceID().ToString():"null")}");
        if (!AreCoreReferencesResolved())
        {
            Debug.LogError("[IntroTapeController] Bootstrap completed with unresolved core references.");
        }

        bootstrapStage = BootstrapStage.RuntimeBinding;
        ValidateSceneWiring();
        ValidateTutorialInteractionRules();
        BuildObjectivesIfMissing();
        CacheObjectiveLookup();

        hasCompletedRuntimeBinding = AreCoreReferencesResolved();
        bootstrapStage = BootstrapStage.TutorialStart;
        HorrorEvents.RaiseTutorialStarted();
        EnterStep(TutorialStep.CollectSoundboard, "Tutorial started");
        Debug.Log("Tutorial first active step: CollectSoundboard.");
    }

    void OnEnable()
    {
        Debug.Log($"[IntroTapeController] OnEnable objectiveDefinitions.count={(objectiveDefinitions!=null?objectiveDefinitions.Count:-1)}");
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

    bool AreCoreReferencesResolved()
    {
        return soundboardDoorGate != null &&
            soundboardUseDoor != null &&
            corruptionDoor != null &&
            lightDoorGate != null &&
            chaseGate != null &&
            sprintDoor != null &&
            tutorialExitGate != null &&
            soundboardPickup != null &&
            tutorialLightSpot != null &&
            exitDoor != null;
    }

    void ApplyRegistryReferencesNonDestructive()
    {
        TutorialRuntimeRegistry registry = TutorialRuntimeRegistry.Instance;
        if (registry == null)
        {
            Debug.LogWarning("[IntroTapeController] ApplyRegistryReferences skipped: registry null.");
            return;
        }

        TryBindRole(registry, TutorialRuntimeRole.SoundboardDoorGate, ref soundboardDoorGate);
        TryBindRole(registry, TutorialRuntimeRole.SoundboardUseDoor, ref soundboardUseDoor);
        TryBindRole(registry, TutorialRuntimeRole.CorruptionDoor, ref corruptionDoor);
        TryBindRole(registry, TutorialRuntimeRole.LightDoorGate, ref lightDoorGate);
        TryBindRole(registry, TutorialRuntimeRole.ChaseGate, ref chaseGate);
        TryBindRole(registry, TutorialRuntimeRole.SprintDoor, ref sprintDoor);
        TryBindRole(registry, TutorialRuntimeRole.TutorialExitGate, ref tutorialExitGate);
        TryBindRole(registry, TutorialRuntimeRole.SoundboardPickup, ref soundboardPickup);
        TryBindRole(registry, TutorialRuntimeRole.TutorialLightSpot, ref tutorialLightSpot);
        TryBindRole(registry, TutorialRuntimeRole.ExitDoor, ref exitDoor);
        TryBindRole(registry, TutorialRuntimeRole.VillainAI, ref villainAI);
        TryBindRole(registry, TutorialRuntimeRole.EncounterManager, ref encounterManager);
    }

    void TryBindRole<T>(TutorialRuntimeRegistry registry, TutorialRuntimeRole role, ref T field) where T : UnityEngine.Object
    {
        if (registry.TryGet(role, out T resolved) && resolved != null)
        {
            field = resolved;
        }
    }

    void ResolveReferences()
    {
        TutorialRuntimeRegistry registry = TutorialRuntimeRegistry.Instance;
        if (registry != null)
        {
            registry.Register(TutorialRuntimeRole.VillainAI, villainAI, "IntroTapeController.ResolveReferences");
            registry.Register(TutorialRuntimeRole.EncounterManager, encounterManager, "IntroTapeController.ResolveReferences");
        }
    }

    void ValidateTutorialInteractionRules()
    {
        GameObject[] doors = { soundboardDoorGate, soundboardUseDoor, corruptionDoor, lightDoorGate, chaseGate, sprintDoor, tutorialExitGate };
        for (int i = 0; i < doors.Length; i++)
        {
            if (doors[i] == null) continue;
            DoorTrigger trigger = doors[i].GetComponent<DoorTrigger>();
            if (trigger != null && trigger.showLockedPrompt)
            {
                Debug.LogWarning("[IntroTapeController] Tutorial interaction validation: locked prompt is enabled on " + doors[i].name + ".");
            }
        }
        Debug.Log("[IntroTapeController] Tutorial interaction validation: locked-door prompt rules inspected.");
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
        Debug.Log($"[IntroTapeController] BuildObjectivesIfMissing enter count={(objectiveDefinitions!=null?objectiveDefinitions.Count:-1)}");
        if (objectiveDefinitions != null && objectiveDefinitions.Count > 0)
        {
            Debug.Log("[IntroTapeController] BuildObjectivesIfMissing preserved serialized objectiveDefinitions.");
            return;
        }

        Debug.LogError("[IntroTapeController] objectiveDefinitions is empty. Serialized objective configuration must be authored and preserved; runtime fallback creation is disabled.");
    }

    void CacheObjectiveLookup()
    {
        Debug.Log($"[IntroTapeController] CacheObjectiveLookup objectiveDefinitions.count={(objectiveDefinitions!=null?objectiveDefinitions.Count:-1)}");
        objectiveByStep.Clear();
        if (objectiveDefinitions == null)
        {
            return;
        }

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

        if (nextStep == TutorialStep.CollectSoundboard)
        {
            Debug.Log("SoundboardPickupArea marked as initial tutorial safe zone.");
        }

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
        SetDoorLocked(soundboardDoorGate, step == TutorialStep.StartDarkRoom || step == TutorialStep.CollectSoundboard);
        SetDoorLocked(soundboardUseDoor, step == TutorialStep.UseSoundboard);
        SetDoorLocked(corruptionDoor, step == TutorialStep.ShowCorruption);
        SetDoorLocked(lightDoorGate, step == TutorialStep.UseLightSpot);
        SetDoorLocked(chaseGate, step == TutorialStep.HideFromMonster);
        SetDoorLocked(sprintDoor, step == TutorialStep.TeachSprintRisk);
        SetDoorLocked(tutorialExitGate, step != TutorialStep.ExitToMainMaze && step != TutorialStep.Completed);
    }

    void SetDoorLocked(GameObject doorObj, bool locked)
    {
        if (doorObj == null)
        {
            return;
        }

        TutorialStageDoor stageDoor = doorObj.GetComponent<TutorialStageDoor>();
        if (stageDoor != null)
        {
            if (!locked)
            {
                string unlockReason = currentStep.ToString();
                Debug.Log("Tutorial door unlocked by event: " + unlockReason + ".");
                stageDoor.Unlock(unlockReason);
                Debug.Log("Door now interactable: " + doorObj.name + ".");
            }
            else if (stageDoor.IsUnlocked)
            {
                stageDoor.SetLocked(true);
            }
            return;
        }

        DoorTrigger trigger = doorObj.GetComponent<DoorTrigger>();
        if (trigger != null)
        {
            trigger.SetLockedState(locked);
            return;
        }

        doorObj.SetActive(locked);
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

        if (context.soundboardUseDoor != null)
        {
            soundboardUseDoor = context.soundboardUseDoor;
        }

        if (context.corruptionDoor != null)
        {
            corruptionDoor = context.corruptionDoor;
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


        if (context.sprintDoor != null)
        {
            sprintDoor = context.sprintDoor;
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
