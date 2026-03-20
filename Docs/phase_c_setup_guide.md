# Phase C Setup Guide

This guide explains how to wire up the recent **Phase C chase orchestration** additions in the Unity editor, plus the optional sanity visual cues that were added as support scaffolding.

## What was added

### New scripts
- `Assets/Scripts/Maze/ChaseSystem.cs`
- `Assets/Scripts/Maze/ScareScheduler.cs`
- `Assets/Scripts/Maze/ThreatFeedbackSystem.cs`
- `Assets/Scripts/Maze/SanityVisualCueController.cs`

### Updated scripts
- `Assets/Scripts/Maze/VillainAI.cs`
- `Assets/Scripts/Maze/JumpscareSystem.cs`
- `Assets/Scripts/Maze/HorrorDirector.cs`
- `Assets/Scripts/Maze/VillainAudio.cs`
- `Assets/Scripts/Maze/HorrorEvents.cs`
- `Assets/Scripts/PlaySound.cs`

## Goal of the setup

After this setup:
- `VillainAI` can ask `ChaseSystem` whether a chase should begin.
- `ChaseSystem` can alternate or choose between **Short Burst** and **Prolonged Pressure** chase patterns.
- `JumpscareSystem` can respect chase pacing and avoid firing at bad times.
- A future sanity script can drive either or both of these optional visuals:
  - a **HUD sanity bar**
  - a **screen vignette overlay**

---

## 1. Add the ChaseSystem to the scene

1. Open the main maze gameplay scene.
2. Create an empty GameObject named **ChaseSystem**.
3. Add the `ChaseSystem` component to it.
4. In the Inspector, assign:
   - **Villain AI** -> your active villain GameObject that has `VillainAI`
   - **Horror Director** -> the scene object that has `HorrorDirector`

### Recommended first-pass values
Use these as a safe baseline before playtesting:

- **Short Burst Duration Range**: `4` to `7`
- **Prolonged Pressure Duration Range**: `9` to `15`
- **Short Burst Cooldown**: `8`
- **Prolonged Pressure Cooldown**: `14`
- **Prolonged Pressure Tension Threshold**: `0.55`
- **Chase Detection Grace Period**: `0.75`
- **Search Fallback Duration**: `2.5`
- **Alternate Patterns**: `On`

### Jumpscare budget defaults
- **Drive Jumpscare Budget**: `On`
- **Jumpscare Warmup**: `1.5`
- **Jumpscare Cooldown During Chase**: `7`
- **Jumpscare Cooldown Outside Chase**: `14`
- **Post Chase Jumpscare Lockout**: `4`

### Debug option
- Turn **Enable Debug Logs** on only while tuning.
- Turn it off for regular playtests if console noise gets in the way.

---


## 1A. Add the ScareScheduler to the scene

1. Create an empty GameObject named **ScareScheduler**.
2. Add the `ScareScheduler` component.
3. Assign:
   - **Horror Director** -> the scene `HorrorDirector`
   - **Jumpscare System** -> the scene `JumpscareSystem`
   - **Chase System** -> the scene `ChaseSystem`
   - **Villain AI** -> the active `VillainAI`

### What it does
This component is now the heartbeat of the 5-minute loop. It schedules:
- minor scares,
- presence cues,
- fakeouts,
- chase triggers,
- major jumpscares,
- relief beats.

### Recommended first-pass values
- **Calm Beat Interval**: `12-18`
- **Build Beat Interval**: `12-16`
- **Threat Beat Interval**: `10-14`
- **Relief Beat Interval**: `8-12`
- **Finale Beat Interval**: `6-10`
- **Major Scare Cooldown**: `35`
- **Presence Cue Guarantee**: `25`

---

## 2. Update the VillainAI references

The new chase flow expects `VillainAI` to know about `ChaseSystem`.

1. Select the villain GameObject.
2. In `VillainAI`, find the new **Chase System** field.
3. Drag the scene's **ChaseSystem** object into that field.

### What happens if you do not assign it?
The script tries `FindObjectOfType<ChaseSystem>()` at runtime, so the scene can still work if the object exists.

However, **manual assignment is recommended** because:
- it is clearer in the scene setup,
- it avoids hidden wiring,
- it makes prefab/scene debugging easier.

---

## 3. Update the JumpscareSystem references

`JumpscareSystem` now supports chase-aware pacing.

1. Select the GameObject that contains `JumpscareSystem`.
2. Assign the new fields:
   - **Chase System** -> the scene `ChaseSystem`
   - **Horror Director** -> the scene `HorrorDirector`

### Existing fields that still need to be valid
Make sure these were already assigned and still work:
- **Villain AI**
- **Player**
- **Warning Canvas** / **Warning Text** if warning mode is enabled
- **Jumpscare Canvas** / **Jumpscare Image** / **Jumpscare Sprite**
- **Screen Flash**
- **Jumpscare Sound**

### Important behavior change
A jumpscare can now be **deferred** when `ChaseSystem` says the pacing budget is not ready.

That means if you test a timer and a scare does not fire immediately, this may be correct behavior rather than a bug.

When **Use Director Driven Scheduling** is enabled, major jumpscares still need a real contextual opportunity:
- villain visible,
- villain in the configured distance window,
- or both, depending on the moment.

---

## 4. Verify the HorrorDirector hookup

`ChaseSystem` reads the current tension from `HorrorDirector` when choosing patterns, especially if you later turn off alternating pattern behavior.

Check the `HorrorDirector` scene object and confirm:
- it exists in the active gameplay scene,
- it has a valid **Villain AI** reference,
- it has a valid **Player** reference,
- it is actually updating tension during play.

### Quick test
During play mode:
- move near the villain,
- trigger chase behavior,
- watch whether tension rises,
- verify the chase pacing feels less spammy than before.

If you use the debug overlay, this is a good time to watch:
- tension,
- pacing band,
- AI state,
- chase active state.

---


## 4A. Add the ThreatFeedbackSystem

This system gives the player constant feedback when the enemy is close or when the pacing spikes.

1. Create an empty GameObject named **ThreatFeedbackSystem**.
2. Add the `ThreatFeedbackSystem` component.
3. Assign:
   - **Villain AI** -> the active `VillainAI`
   - **Horror Director** -> the scene `HorrorDirector`
   - **Player** -> the player transform

### Optional visual references
- **Danger Vignette** -> a full-screen image for danger buildup
- **Danger Canvas Group** -> the wrapper that fades the vignette

### Optional audio references
- **Heartbeat Loop**
- **Proximity Drone**
- **Breathing Loop**

### Recommended first-pass distances
- **Near Distance**: `18`
- **Danger Distance**: `10`
- **Immediate Distance**: `5`

---

## 5. Optional setup: sanity HUD bar

The `SanityVisualCueController` can drive a UI slider as a sanity bar.

### Create the HUD bar
1. Open your gameplay HUD canvas.
2. Add a `Slider` UI element.
3. Rename it to **SanityBar**.
4. Set its min/max visually however you prefer.
5. Optional: assign a custom fill image for color feedback.

### Add the controller
1. Create an empty GameObject named **SanityVisuals** under the HUD canvas or another UI manager object.
2. Add the `SanityVisualCueController` component.
3. Configure:
   - **Show Hud Bar** -> `On`
   - **Sanity Slider** -> your `SanityBar` slider
   - **Sanity Fill Image** -> the slider fill image (optional but recommended)

### Source wiring
Assign **Sanity Source** to whichever future or temporary script exposes sanity.

The controller looks for one of these members by default:
- property: `CurrentSanity`
- property: `currentSanity`
- field: `CurrentSanity`
- field: `currentSanity`

### Range setup
By default the controller assumes:
- **Min Sanity** = `0`
- **Max Sanity** = `100`

If your sanity script uses another scale, change these values.

### Recommended visual rule
- Full sanity = bar appears healthy/full.
- Low sanity = bar shifts toward warning colors.

Use the `Gradient` field to control this.

---

## 6. Optional setup: canvas vignette overlay

The same `SanityVisualCueController` can drive a vignette overlay.

### Create the vignette
1. On your HUD canvas, add a full-screen `Image`.
2. Stretch it to all four anchors so it covers the full screen.
3. Use a vignette sprite/material if you have one.
4. Put it above gameplay HUD elements only if that is the intended look.
5. Add a `CanvasGroup` to the same object, or to a parent wrapper object.

### Controller wiring
On `SanityVisualCueController`, assign:
- **Show Vignette Overlay** -> `On`
- **Vignette Image** -> the full-screen image
- **Vignette Canvas Group** -> the object that should fade in/out

### Tuning tips
- Start with a subtle dark red or desaturated edge tint.
- Use the `Vignette Strength By Stress` curve so the effect stays light at medium stress and ramps harder near low sanity.
- Keep the effect readable but not so strong that it blocks gameplay.

### Good first-pass settings
- **Vignette Color** alpha around `0.5` to `0.8`
- keep the curve shallow at first, then steep near the end

---

## 7. Temporary sanity source for testing

If a full `SanitySystem` is not implemented yet, you can still test the visuals with a placeholder MonoBehaviour.

A simple test script only needs one readable member such as:
- `public float CurrentSanity = 100f;`

Then:
1. put that script on any active scene object,
2. assign it to `Sanity Source`,
3. lower the value during play mode,
4. confirm the HUD bar and vignette react.

---

## 8. Expected runtime flow after setup

Once everything is assigned correctly, the flow should be:

1. `VillainAI` detects or reacquires the player.
2. `VillainAI` asks `ChaseSystem` whether a chase should start.
3. `ChaseSystem` picks a chase pattern and starts the chase.
4. During the chase, `ChaseSystem` manages pacing and jumpscare budget windows.
5. `JumpscareSystem` checks the budget before actually firing a scare.
6. If a sanity-driving script is present, `SanityVisualCueController` updates the HUD bar and/or vignette.

---

## 9. Playtest checklist

Use this checklist after wiring the scene:

### Chase setup checklist
- [ ] `ChaseSystem` exists in the scene.
- [ ] `ChaseSystem.VillainAI` is assigned.
- [ ] `ChaseSystem.HorrorDirector` is assigned.
- [ ] `VillainAI.ChaseSystem` is assigned.
- [ ] Chases begin normally when the player is detected.
- [ ] Chases do not restart instantly back-to-back.
- [ ] Short burst chases end faster than prolonged pressure chases.

### Jumpscare checklist
- [ ] `JumpscareSystem.ChaseSystem` is assigned.
- [ ] `JumpscareSystem.HorrorDirector` is assigned.
- [ ] Jumpscares still work when the villain is in range.
- [ ] Jumpscares are sometimes delayed by pacing instead of firing every time the timer hits.

### Sanity visual checklist
- [ ] `SanityVisualCueController` exists in the scene.
- [ ] `Sanity Source` is assigned.
- [ ] HUD sanity bar updates when sanity changes.
- [ ] Vignette opacity changes when sanity drops.
- [ ] Effects can be disabled independently with **Show Hud Bar** and **Show Vignette Overlay**.

---

## 10. Common setup mistakes

### Chase never starts
Check:
- `VillainAI` can still see/detect the player,
- `ChaseSystem` exists in the scene,
- the `VillainAI -> ChaseSystem` reference is assigned,
- `ChaseSystem` cooldown values are not overly strict,
- the villain is not stuck in another state.

### Jumpscares feel "broken"
Check:
- the villain is inside the configured distance window,
- `ChaseSystem` may be intentionally deferring the scare,
- `minJumpscareInterval` and `maxJumpscareInterval` are not too large,
- the warning flow is not hiding the expected timing.

### Sanity visuals do nothing
Check:
- `Sanity Source` is assigned,
- the source actually exposes `CurrentSanity` or `currentSanity`,
- your min/max sanity range matches the source scale,
- `Show Hud Bar` and/or `Show Vignette Overlay` are enabled,
- the `Slider`, `Image`, and `CanvasGroup` references are assigned.

---

## 11. Recommended next step

After the scene wiring is stable, the next useful addition is a real `SanitySystem` that exposes a readable sanity value and reacts to:
- chase start/end,
- jumpscares,
- safe recovery windows,
- false cues or audiovisual effects.

That will let the optional visuals become fully gameplay-driven instead of only structural placeholders.
