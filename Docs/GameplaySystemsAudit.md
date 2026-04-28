# Gameplay Systems Audit

_Last updated: 2026-04-28_

## System: Tutorial controller
- **Exists:** Yes (refactored state machine).
- **File path(s):** `Assets/Scripts/Maze/IntroTapeController.cs`
- **Current behavior:** Event-driven tutorial progression using `TutorialStep`, `TutorialObjective`, objective timeout/retry, fail-safe gates, and completion persistence.
- **Dependencies:** `HorrorEvents`, `GameplayHintController`, `VillainAI`, `MazeExitDoor`, `SoundboardPickup`, `SafeSpaceZone`.
- **Scene wiring required:** Gate objects, references for soundboard/light/villain/exit.
- **Missing features:** Optional cinematic camera choreography and stronger authored in-scene sequence beats.
- **Recommended follow-up:** Add authored timeline stingers per step and automated playmode tests.

## System: Event system
- **Exists:** Yes.
- **File path(s):** `Assets/Scripts/Maze/HorrorEvents.cs`
- **Current behavior:** Central gameplay event bus for soundboard/sanity/corruption/light/chase/sprint/noise/death/exit/tutorial lifecycle.
- **Dependencies:** Most gameplay systems subscribe/emit via this static bus.
- **Scene wiring required:** None.
- **Missing features:** Typed payload structs for richer data contracts.
- **Recommended follow-up:** Introduce event payload DTOs for stronger tooling.

## System: Soundboard
- **Exists:** Yes.
- **File path(s):** `Assets/Scripts/PlaySound.cs`, `Assets/Scripts/Maze/SoundboardPickup.cs`
- **Current behavior:** Slot-based mapped playback with cooldown and events; pickup emits collection event and reminder hint.
- **Dependencies:** `HorrorEvents`, UI hints, sanity/corruption systems.
- **Scene wiring required:** Pickup object and runtime soundboard object references.
- **Missing features:** Explicit dedicated UI for cooldown per slot.
- **Recommended follow-up:** Add slot HUD and tuning panel.

## System: Light spots
- **Exists:** Yes.
- **File path(s):** `Assets/Scripts/Maze/SafeSpaceZone.cs`
- **Current behavior:** Hold-to-activate safe area restoring sanity, stabilizing corruption, and emitting light lifecycle events.
- **Dependencies:** `SanitySystem`, `CorruptionSystem`, `HorrorEvents`.
- **Scene wiring required:** Safe zone colliders/lights.
- **Missing features:** Distinct VFX/SFX presets per tier.
- **Recommended follow-up:** Add profile-driven light spot presets.

## System: Monster AI
- **Exists:** Yes.
- **File path(s):** `Assets/Scripts/Maze/VillainAI.cs`, `Assets/Scripts/Maze/ChaseSystem.cs`
- **Current behavior:** Patrol/search/chase AI with soundboard and generic noise reactions; chase start/end events broadcast.
- **Dependencies:** `MazeGenerator`, `HorrorEvents`, `SafeSpaceZone`.
- **Scene wiring required:** Player and maze references.
- **Missing features:** More explicit tutorial-safe chase profile settings.
- **Recommended follow-up:** Add “tutorial chase profile” ScriptableObject.

## System: Sprint system
- **Exists:** Yes.
- **File path(s):** `Assets/Scripts/Maze/PlayerSprintSystem.cs`, `Assets/Scripts/SC_FPSController.cs`
- **Current behavior:** Stamina drain/recovery, sprint start/stop, periodic noise emission, warning hints.
- **Dependencies:** Player controller + event/hint systems.
- **Scene wiring required:** Player sprint component on player object.
- **Missing features:** HUD stamina bar.
- **Recommended follow-up:** Add optional stamina UI controller.

## System: Hint system
- **Exists:** Yes.
- **File path(s):** `Assets/Scripts/Maze/GameplayHintController.cs`
- **Current behavior:** Event-driven hints with priority, cooldown, suppression, and tracking.
- **Dependencies:** `HorrorEvents`, `RuntimeStatsTracker`.
- **Scene wiring required:** Hint controller object.
- **Missing features:** Localization/internationalization.
- **Recommended follow-up:** Externalize hint strings.

## System: UI
- **Exists:** Partial.
- **File path(s):** `Assets/Scripts/Maze/SanityVisualCueController.cs`, `Assets/Scripts/Maze/CorruptionVisualCueController.cs`, `Assets/Scripts/Maze/GameplayHintController.cs`
- **Current behavior:** Sanity/corruption bars + runtime hint overlay.
- **Dependencies:** sanity/corruption systems and event system.
- **Scene wiring required:** Optional HUD references or runtime generation.
- **Missing features:** Dedicated tutorial objective widget.
- **Recommended follow-up:** Add objective panel for tutorial readability.

## System: Scene/menu flow
- **Exists:** Yes.
- **File path(s):** `Assets/Scripts/MenuSystem/MenuSceneLoader.cs`, `Assets/Scripts/MenuSystem/TitleScreenController.cs`, `Assets/Scripts/MenuSystem/MenuPrefsKeys.cs`
- **Current behavior:** First-run tutorial gating and replay route using persistent flags.
- **Dependencies:** PlayerPrefs keys and menu button wiring.
- **Scene wiring required:** Configure intro/main scene names and replay button.
- **Missing features:** Explicit continue-from-main-save branch logic in modern menu path.
- **Recommended follow-up:** Integrate continue slot system.

## System: Save/persistence
- **Exists:** Partial.
- **File path(s):** `Assets/Scripts/MenuSystem/MenuPrefsKeys.cs`, `Assets/Scripts/MenuSystem/GameSettingsStore.cs`
- **Current behavior:** PlayerPrefs settings and tutorial completion flags.
- **Dependencies:** PlayerPrefs.
- **Scene wiring required:** None.
- **Missing features:** Dedicated profile save for run progression.
- **Recommended follow-up:** Add minimal profile object for run meta state.

## System: Debug/telemetry
- **Exists:** Yes.
- **File path(s):** `Assets/Scripts/Maze/RuntimeStatsTracker.cs`, `Assets/Scripts/Maze/HorrorDebugOverlay.cs`
- **Current behavior:** Tracks mechanic usage and exposes `PrintGameplayTrainingReport()` for tuning.
- **Dependencies:** `HorrorEvents`.
- **Scene wiring required:** Runtime tracker object.
- **Missing features:** Persisted session logs and CSV export.
- **Recommended follow-up:** Add developer console command + export.

---

## Major gaps summary
1. Scene wiring remains the top practical risk (missing references can degrade onboarding quality).
2. Tutorial flow now event-driven but still depends on authored scene gates and spatial layout.
3. Validation currently manual/log-based; no automated playmode checks yet.


## System: Procedural tutorial generator
- **Exists:** Yes.
- **File path(s):** `Assets/Scripts/Maze/GuidedIntroMazeGenerator.cs`, `Assets/Scripts/Maze/TutorialLayoutContext.cs`
- **Current behavior:** Generates deterministic guided intro segments with gates, pickups, light spots, monster reveal markers, sprint trigger, exit gate, and connector context.
- **Dependencies:** `IntroTapeController`, `SoundboardPickup`, `SafeSpaceZone`, `VillainAI`, `HorrorEvents`.
- **Scene wiring required:** Optional prefab references; can run with primitive fallbacks.
- **Missing features:** Full navmesh bake automation and authored art kit integration.
- **Recommended follow-up:** Add prefab palette/Theme profiles and runtime navmesh update if needed.
