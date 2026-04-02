# Hybrid Note + Multi-Ending Setup Guide

This guide explains how to configure the new modular systems introduced in the recent refactor:

- `NoteData` + unlock conditions
- Dynamic run state (`RunGameState`)
- Tiered note spawning in `NoteSystem`
- Data-driven endings (`EndingData` + `EndingSystem`)

---

## 1) Scene Setup (Required Components)

In your gameplay scene (example: `SampleScene` / `level2`), ensure these components exist:

1. **RunGameState**
   - Add `RunGameState` to a persistent manager object (or `GameManager` object).
2. **NoteSystem**
   - Existing component is still used.
   - Assign references:
     - `Maze Generator`
     - `Player`
     - `Game State` (the `RunGameState` from step 1)
     - Note UI references (canvas, TMP text, button, counter text)
3. **EndingSystem**
   - Add to a manager object.
   - Fill `Endings` list with your 3 ending assets.
4. **GameManager**
   - Assign new references:
     - `Run Game State`
     - `Ending System`

> If references are left empty, systems attempt `FindObjectOfType`, but explicit assignment is recommended for reliability.

---

## 2) Create Note Data Assets

Create note assets from:

- `Create -> HorrorLand -> Notes -> Note Data`

For each note, configure:

- `id`: unique string (never reuse)
- `content`: note text shown to player
- `tier`: integer progression level (`1` near start, higher near exit)
- `isBaseNote`: checked means available from run start
- `unlockConditions`: list of condition assets (all must pass)
- `tags`: narrative tags used by endings

### Authoring Rules

- Keep note text order-independent (no hard dependency on exact pickup order).
- Use tags for narrative state, not specific note IDs, when evaluating endings.
- Keep contradictory notes out of the same campaign set.

---

## 3) Create Unlock Conditions (Reusable)

Create condition assets from:

- `Create -> HorrorLand -> Notes -> Conditions -> ...`

Available examples:

- `VisitedAreaCondition`
- `TimeSurvivedCondition`
- `TriggeredEventCondition`
- `ChaseOccurredCondition`

Attach one or more of these assets to each `NoteData.unlockConditions` list.

### Logic Behavior

- Base note (`isBaseNote=true`) appears at start.
- Non-base notes become available only when all assigned conditions are met.
- Unlocking adds to the pool; it never removes already-available notes.

---

## 4) Configure NoteSystem

On `NoteSystem`:

1. Fill **All Notes** with your `NoteData` assets.
2. Set **Notes Required To Finish** to `6` (default and expected behavior).
3. Set **Simultaneous World Notes** (recommended `2`–`3`).
4. Keep/ignore legacy `notes` list:
   - If `All Notes` is empty, legacy inline notes still work as fallback.
   - For the new architecture, prefer `All Notes`.

### Tier + Zone Behavior

- Tier is not randomized.
- Spawn position is selected by zone tier based on proximity progression from start to exit.
- Randomization occurs only between eligible notes inside a tier.

---

## 5) Feed Runtime State During Gameplay

`RunGameState` already tracks:

- Time survived
- Chase occurrence (via `HorrorEvents.OnChaseStarted`)
- Sanity snapshot (via `HorrorEvents.OnSanityChanged`)
- Collected note IDs/tags (automatically from `NoteSystem`)

For custom unlocks/endings, call these when relevant:

- `RunGameState.MarkVisitedArea("your_area_id")`
- `RunGameState.MarkTriggeredEvent("your_event_id")`

Example use cases:

- Secret room trigger calls `MarkVisitedArea("secret_room")`
- Ritual interaction calls `MarkTriggeredEvent("ritual_started")`

---

## 6) Create Ending Assets

Create ending data assets from:

- `Create -> HorrorLand -> Endings -> Ending Data`

For each ending:

- `id`: unique ending identifier
- `resultMessage`: text shown on win
- `priority`: higher priority evaluated first
- `conditions`: list of `EndingCondition` assets

Create ending conditions from:

- `Create -> HorrorLand -> Endings -> Conditions -> ...`

Available examples:

- `EndingHasTagCondition`
- `EndingWasChasedCondition`
- `EndingEnteredSecretRoomCondition`
- `EndingSanityThresholdCondition`

### 3-Ending Recommendation

- **Ending A (Truth)**: requires key lore tags + secret room visited
- **Ending B (Escape)**: requires exit behavior and no deep lore tags
- **Ending C (Broken Mind)**: sanity threshold + chase-heavy run

Set priorities so the most specific ending wins first.

---

## 7) Connect Endings to Game Flow

`GameManager` now resolves ending message on win:

- If an ending matches, it uses `EndingData.resultMessage`
- If none match, fallback message is used

No additional coding is required if references are assigned.

---

## 8) Migration Checklist (From Legacy Notes)

1. Create `NoteData` assets mirroring old inline note text.
2. Copy old IDs/text into new assets.
3. Assign proper `tier` values.
4. Mark initial notes as `isBaseNote=true`.
5. Move legacy narrative gates into `unlockConditions`.
6. Add semantic tags used by endings.
7. Populate `NoteSystem.allNotes` and leave legacy list as backup until validated.
8. Playtest, then optionally clear legacy list.

---

## 9) Validation Checklist (Playtest)

- [ ] Run starts with only base notes in the available pool.
- [ ] Unlock actions expand pool during run (no removals).
- [ ] Notes spawn by zone tier and do not duplicate.
- [ ] Exactly 6 notes can complete the run.
- [ ] Small pools still spawn gracefully (fallback path).
- [ ] Endings resolve via tags/actions (not specific note IDs).
- [ ] All 3 endings are reachable through intended play patterns.

---

## 10) Common Pitfalls

- Duplicate `NoteData.id` values -> causes selection/tracking conflicts.
- Non-base notes with empty condition list -> remain locked by design.
- Missing `RunGameState` reference -> unlock checks cannot evaluate non-base conditions.
- No ending priorities -> ambiguous outcomes when multiple endings are true.
- Forgetting to call `MarkVisitedArea` / `MarkTriggeredEvent` in gameplay triggers.

---

If you want, I can also provide a **ready-to-use example set** (6 notes + condition assets + 3 endings matrix) so your team can drop it directly into the scene.
