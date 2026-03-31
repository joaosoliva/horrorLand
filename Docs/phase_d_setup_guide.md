# Phase D Setup Guide

This guide covers the **Phase D sanity and psychological layer MVP** added for the maze loop.

## What was added

### New scripts
- `Assets/Scripts/Maze/SanitySystem.cs`
- `Assets/Scripts/Maze/SanityPsychologicalEffects.cs`

### Updated scripts
- `Assets/Scripts/Maze/HorrorEvents.cs` (adds sanity change event broadcasting)

## 1) Add the SanitySystem

1. Create an empty GameObject named **SanitySystem**.
2. Add the `SanitySystem` component.
3. Recommended first-pass values:
   - **Starting Sanity**: `100`
   - **Passive Recovery Per Second**: `2.4`
   - **Chase Drain Per Second**: `9`
   - **Major Jumpscare Sanity Loss**: `16`
   - **Near Miss Sanity Loss**: `7`

### Behavior summary
- Sanity drains from chase pressure and threat bands.
- Sanity is hit by major jumpscares and other scare events.
- Sanity partially recovers during safer windows.
- Near-miss moments (danger -> near/far) can cause additional stress spikes.

## 2) Add psychological effects

1. Create an empty GameObject named **SanityPsychologicalEffects**.
2. Add `SanityPsychologicalEffects`.
3. Wire references:
   - **Sanity System** -> the scene `SanitySystem`
   - **Listener Low Pass** -> `AudioLowPassFilter` on player/listener camera (optional but recommended)
   - **False Cue Audio Source** -> a general-purpose AudioSource for fake cue playback
   - **Glitch Canvas Group** -> a full-screen overlay canvas group for quick flickers

### Optional references
- **False Cue Clips**: assign 2-6 short clips (step, breath, static, scrape).
- **Glitch Image**: optional image to colorize the glitch overlay.
- **Player**: optional transform for 3D positional false cues.

## 3) Threshold defaults (MVP)

- **Distortion Stress Threshold**: `0.30`
- **False Cue Stress Threshold**: `0.50`
- **Visual Glitch Stress Threshold**: `0.65`

As sanity drops, effects activate in this order:
1. subtle audio distortion,
2. occasional false audio cues,
3. short visual glitches/flickers.

## 4) Accessibility / testing toggles

`SanityPsychologicalEffects` has independent toggles:
- **Enable Audio Distortion**
- **Enable False Audio Cues**
- **Enable Visual Glitches**

Use these to isolate each layer while playtesting readability.

## 5) Integrate existing UI/debug tools

- `SanityVisualCueController` can use `SanitySystem` as `sanitySource` (`CurrentSanity` is exposed).
- `HorrorDebugOverlay` can point to `SanitySystem` to show the runtime value.

## 6) Quick validation checklist

- Start run at full sanity (`100`).
- Enter chase -> sanity drops immediately and over time.
- Trigger major jumpscare -> sanity takes a large hit.
- Break away from threat and stay safe -> sanity recovers.
- Lower sanity enough to trigger distortion -> verify thresholds in sequence.
