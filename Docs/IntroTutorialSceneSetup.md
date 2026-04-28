# Intro Tutorial Scene Setup

## Required objects
- `GuidedIntroMazeGenerator` object in intro scene.
- `IntroTapeController` object in intro scene.
- `GameplayHintController` object.
- `RuntimeStatsTracker` object.
- `SoundboardPickup` object (with trigger collider).
- At least one `SafeSpaceZone` for tutorial light hide/use.
- `VillainAI` object.
- `MazeExitDoor` object.

## Required tags
- Player object tagged `Player`.
- Any required AI trigger volumes/tags from existing systems must remain unchanged.

## Required references on IntroTapeController
- `soundboardDoorGate` (blocks progression before soundboard use)
- `lightDoorGate` (locks/unlocks light tutorial segment)
- `chaseGate` (controls monster reveal progression)
- `tutorialExitGate` (prevents early exit)
- `soundboardPickup`
- `tutorialLightSpot`
- `villainAI`
- `exitDoor`
- `mainMazeSceneName`

## Menu setup checklist
- `MenuSceneLoader.gameplaySceneName` set to main maze scene.
- `MenuSceneLoader.introSceneName` set to intro/tutorial scene.
- `TitleScreenController.recoveredTapeButton` wired (optional but recommended).
- Ensure `MenuPrefsKeys.TutorialCompleted` is not pre-seeded unless skipping intro intentionally.

## Validation checklist
- Start a new profile: New Game routes to intro scene.
- Can’t progress without collecting soundboard.
- Can’t progress without using soundboard.
- Corruption feedback appears after first use.
- Light step requires actual light usage.
- Monster introduction triggers chase and hide objective.
- Sprint warning appears when sprint/noise occurs.
- Exit remains blocked until final step.
- Completing intro sets TutorialCompleted and routes to main maze.
- Recovered Tape replay works and does not wipe existing progress.


## Procedural generation notes
- If no prefabs are assigned, `GuidedIntroMazeGenerator` creates primitive fallback geometry and triggers.
- Use `useFixedTutorialSeed = true` and `fixedTutorialSeed = 1998` for deterministic first-run layout.
- Keep `allowReplayRandomization = false` by default to avoid breaking tutorial beat reliability.
