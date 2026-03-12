# Gameplay Systems Audit (Prototype)

## Scope and method
- Reviewed gameplay scripts under `Assets/Scripts/Maze` and related legacy scripts in `Assets/Scripts`.
- Focused on implemented systems, system interactions, technical debt, and fit with target loop: **tension → panic → absurd humor → tension**.
- Objective: improve architecture and replayability **without unnecessary rewrites**.

---

## 1) Current major gameplay systems

## Maze / world generation
### `MazeGenerator`
**What it does**
- Generates the maze grid and geometry at runtime.
- Computes start/entry and exit, creates a start room and door setup.
- Exposes maze data (`GetMazeCell`, start/exit world positions) used by other systems.

**How it interacts**
- `VillainAI` queries maze cells and dimensions for movement/path decisions.
- `NoteSystem` uses maze cells to place collectable notes.
- `GameManager` checks exit position for win condition.
- `SpatialMapController` reads cells and exit position to render explored map.

**Potential issues / debt**
- Very large "god script" (generation + geometry + start room + door + player placement).
- Debug checks are hardcoded (e.g., specific wall names), creating brittle behavior.
- Runtime generation, material creation, and scene object orchestration are tightly coupled.

**Classification: REFACTOR**
- Core behavior aligns with design goals; needs decomposition into smaller services.

---

## Monster AI and presence
### `VillainAI`
**What it does**
- Controls patrol/search/chase states.
- Uses maze-based pathing (named A*, implemented as BFS queue).
- Handles spawn validation, LOS checks, detection/FOV, and progressive difficulty ramp.
- Includes a "dread" teleport/reappear mechanic.

**How it interacts**
- Depends heavily on `MazeGenerator` maze data.
- Feeds/coordinates with `ProceduralVillain` visual behavior during chase.
- Used by `GameManager` (lose check) and `JumpscareSystem` (distance gating).

**Potential issues / debt**
- Too many responsibilities in one class (FSM, pathfinding, spawn logic, difficulty tuning, LOS, debugging).
- Public flags (`isPatrolling`, etc.) can drift out of sync and are harder to maintain than enum-driven state.
- "AStar" naming mismatch can confuse maintenance and balancing.
- Extensive polling/coroutines and `FindObjectOfType` coupling.

**Classification: REFACTOR**
- Strong prototype base and useful behaviors; should be split into focused components.

### `ProceduralVillain`
**What it does**
- Builds a procedural monster body and applies soft-bone horror deformation/audio feel.
- Drives visual style and body animation toward uncanny look.

**How it interacts**
- Receives player context from `VillainAI`.
- Supports overall monster presentation and encounter intensity.

**Potential issues / debt**
- Mixes runtime mesh/body generation, animation, and audio setup in one component.
- Asset/material creation at runtime can increase GC and complicate tuning.

**Classification: KEEP (light refactor later)**
- Distinctive streamable identity; keep concept and behavior.

### `VillainAudio`
**What it does**
- Localized villain audio behavior (ambient/chase cues).

**How it interacts**
- Pairs with AI states and encounter proximity.

**Potential issues / debt**
- Likely state coupling to `VillainAI` without explicit event contract.

**Classification: REFACTOR**
- Keep feature, move to event-driven audio hooks.

---

## Narrative collection
### `NoteSystem`
**What it does**
- Spawns notes in valid maze cells, tracks collection/read status, shows note UI and counter.
- Queues note display sequence and pauses game while reading.

**How it interacts**
- Depends on `MazeGenerator` for valid spawn cells.
- Progress is consumed by `GameManager` for win condition.

**Potential issues / debt**
- Collection order and display order can diverge (`nextDisplayIndex` uses configured list order, not necessarily collected note).
- Uses both trigger-based collection and per-frame fallback scanning.
- Contains spawn logic + save state + UI + input handling in one class.

**Classification: REFACTOR**
- Solid mechanic aligned with design goals; separate responsibilities.

---

## Win/lose and run state
### `GameManager`
**What it does**
- Checks win/lose conditions, shows end screens, restarts scene, updates note progress HUD.

**How it interacts**
- Reads `NoteSystem`, `MazeGenerator`, and `VillainAI` state directly.

**Potential issues / debt**
- Tight direct references; no event bus/signals for game-state transitions.
- Disables all player `MonoBehaviour` components generically (can break unrelated systems unexpectedly).

**Classification: REFACTOR**
- Keep flow, improve boundaries and control handoff.

---

## Jumpscare / event tension spikes
### `JumpscareSystem`
**What it does**
- Time-windowed jumpscares with distance gating.
- Optional warning UI before jumpscare flash/sound.

**How it interacts**
- Uses `VillainAI` transform distance to trigger.
- Contributes to panic peaks in pacing loop.

**Potential issues / debt**
- Timer + distance only; not informed by broader pacing state.
- Heavy debug logging and multiple UI pathways in one class.

**Classification: REFACTOR**
- Useful spike system; should be controlled by a pacing director.

---

## Navigation assistance
### `SpatialMapController`
**What it does**
- Builds a handheld minimap plane, tracks explored cells, renders player + exit.

**How it interacts**
- Reads `MazeGenerator` structure and player position.

**Potential issues / debt**
- Revealing exit on map can reduce horror uncertainty too early.
- Per-pixel update strategy can get expensive on larger mazes.

**Classification: REFACTOR**
- Keep for accessibility/direction, but gate clarity with tension/sanity design.

---

## Legacy / duplicate prototype scripts (non-core)
Examples: `enemyMonsterAI`, `randomJumpscare`, `scaryEventTrigger`, `obunga*`, `PlaySound`, assorted menu/interaction scripts.

**Assessment**
- These appear to be earlier prototype remnants and parallel mechanics.
- Overlapping monster/jumpscare logic risks conflicting runtime behavior and maintenance noise.

**Classification: REPLACE (as active systems), KEEP only if used in isolated test scenes**
- Consolidate into canonical systems above; retire duplicates from production scene.

---

## 2) Missing planned systems vs. current implementation

### Missing / incomplete
- **HorrorDirector** (global pacing/tension authority): not found as a dedicated system.
- **SoundboardSystem** (intentional comedic interaction loop): only generic/random `PlaySound` input behavior exists.
- **SanitySystem** (stress-driven hallucination/audio distortion): not found as standalone gameplay system.
- **ChaseSystem** (orchestrated chase phases): partially embedded inside `VillainAI`, not modular.
- **RhythmEscapeSystem**: not found.

---

## 3) Recommended architecture evolution (minimal rewrite)

## Target modular structure
- `HorrorDirector` (new): owns tension scalar [0..1], pacing states, cooldown budgets.
- `MonsterController` facade (new): high-level commands to AI (`Patrol`, `Investigate`, `StartChase`, `AbortChase`).
- `VillainAI` (refactored): movement + sensing + pathing only.
- `ChaseSystem` (new): chase start/stop rules, intensity ramp, fail/success outcomes.
- `SoundboardSystem` (new): deterministic slot-based meme triggers with cooldown/risk/reward.
- `SanitySystem` (new): stress accumulation, audiovisual effects, event hooks.
- `EventBus` or ScriptableObject channels (new): low-coupling system communication.
- `NarrativeSystem` split: `NoteSpawner`, `NoteCollector`, `NoteUI`.

## Communication rules
- Use events for cross-system effects, e.g.:
  - `OnTensionChanged(float)`
  - `OnSoundboardPlayed(SoundTag tag, float loudness)`
  - `OnPlayerSpotted()` / `OnChaseStarted()` / `OnChaseEnded()`
  - `OnSanityThresholdCrossed(SanityTier tier)`
- Avoid polling other systems every frame when event-driven transitions are possible.

---

## 4) Implementation plan for missing systems (phased)

## Phase 1 — Stabilize and expose APIs (low risk)
1. Add `HorrorDirector` with basic tension model:
   - Inputs: time since last scare, proximity to villain, chase active, darkness, soundboard usage.
   - Outputs: normalized tension and pacing band (Calm / Uneasy / Panic / Recovery).
2. Refactor `VillainAI` state to enum + explicit transition methods.
3. Move pathfinding into `MazePathfinder` service class used by AI.
4. Split `NoteSystem` UI from collection/spawn logic.

## Phase 2 — Comedy-horror loop completion
1. Implement `SoundboardSystem`:
   - 4–8 mapped meme slots, cooldowns, limited charges or battery economy.
   - Sounds generate noise events used by AI and director.
2. Implement `SanitySystem`:
   - Stress rises with chase/near misses/jumpscares, decreases in safe windows.
   - Distortions: subtle audio warble, fake footsteps, brief visual artifacts.
3. Hook `JumpscareSystem` triggers to director budgets (no random spam).

## Phase 3 — Replayability and streamability
1. Add event seeds + run mutators (e.g., "echoing halls", "aggressive stalker", "faulty map").
2. Add `ChaseSystem` escalation patterns (short burst chase, long pursuit, fake chase).
3. Prototype `RhythmEscapeSystem` as optional mini-mechanic during chase exits.

---

## 5) Keep / Refactor / Replace summary table

| System | Classification | Why |
|---|---|---|
| MazeGenerator | REFACTOR | Strong foundation but monolithic and tightly coupled. |
| VillainAI | REFACTOR | Good behavior breadth; too many responsibilities in one script. |
| ProceduralVillain | KEEP | Unique identity and high streamability value. |
| VillainAudio | REFACTOR | Useful but should subscribe to explicit AI/director events. |
| NoteSystem | REFACTOR | Core narrative loop works; needs split and ordering fixes. |
| GameManager | REFACTOR | Works for prototype flow; over-coupled and broad control side effects. |
| JumpscareSystem | REFACTOR | Effective panic spikes; should become director-driven. |
| SpatialMapController | REFACTOR | Helpful but currently reduces uncertainty and can be optimized. |
| Legacy duplicate scripts | REPLACE | Overlapping mechanics increase conflicts and maintenance cost. |

---

## 6) Immediate next coding actions (smallest high-impact set)
1. Create `HorrorDirector` scaffold + event channel interfaces (no behavior rewrites yet).
2. Refactor `VillainAI` state representation to enum and isolate transitions.
3. Extract `NoteUIController` from `NoteSystem` to reduce coupling.
4. Replace generic `PlaySound` with first `SoundboardSystem` MVP that emits noise events.

This sequence delivers better pacing control and clearer architecture while preserving prototype momentum.

---

## 7) Solo developer execution plan (clear phased roadmap)

This plan is optimized for **one developer** shipping iterative playable builds in short loops.

## Guiding constraints
- Keep each phase shippable in 1-2 weeks.
- Prefer vertical slices over broad rewrites.
- Preserve current content and scene setup as much as possible.
- Only refactor when it directly unlocks pacing, replayability, or debugging.

## Phase A (Week 1) — Foundation and risk reduction
**Goal**: stabilize architecture without changing player-facing behavior too much.

**Tasks**
1. Add `HorrorDirector` skeleton with tension value and pacing bands (Calm/Uneasy/Panic/Recovery).
2. Add lightweight event channels (C# events or ScriptableObject channels):
   - `OnTensionChanged`
   - `OnChaseStarted` / `OnChaseEnded`
   - `OnSoundboardPlayed`
3. Convert `VillainAI` state booleans to enum state + explicit transitions.
4. Add debug overlay (optional) to display: tension, AI state, sanity, chase active.

**Definition of done**
- Game still fully playable.
- No major feature regressions.
- Tension value updates in runtime and can be inspected.

**Expected impact**
- Cleaner integration surface for every next phase.

## Phase B (Week 2) — Complete the comedy loop MVP
**Goal**: make meme interaction meaningful instead of cosmetic.

**Tasks**
1. Implement `SoundboardSystem` MVP:
   - 4 mapped sounds (keys 1-4)
   - per-sound cooldown
   - optional "loudness" metadata
2. Emit noise events from soundboard and let AI react (investigate chance or awareness bump).
3. Add short "humor relief" effect in `HorrorDirector` (small temporary tension drop), followed by rebound.

**Definition of done**
- Player can intentionally use soundboard for tactical/comedic decisions.
- Soundboard sometimes helps, sometimes increases risk (interesting tradeoff).

**Expected impact**
- Core loop starts matching target: panic -> absurd humor -> renewed tension.

## Phase C (Week 3) — Chase quality pass
**Goal**: make encounters legible and memorable.

**Tasks**
1. Extract chase orchestration into `ChaseSystem` (trigger, escalation, cooldown).
2. Keep `VillainAI` focused on sensing/path movement.
3. Add 2 chase patterns:
   - fast short burst
   - prolonged pressure chase
4. Hook `JumpscareSystem` to director/chase budgets so spikes are contextual.

**Definition of done**
- Chases feel intentional (not random spam).
- Back-to-back unfair chase loops are reduced.

**Expected impact**
- Stronger panic moments and better streamable highlights.

## Phase D (Week 4) — Sanity and psychological layer MVP
**Goal**: add psychological horror variance cheaply.

**Tasks**
1. Implement `SanitySystem` with 0-100 stress meter.
2. Add 3 low-cost effects by thresholds:
   - subtle audio distortion
   - occasional false audio cue
   - brief visual glitch/flicker
3. Connect stress changes to events (chase, near miss, jumpscare, safe time).

**Definition of done**
- Player can feel state changes without confusion.
- Effects are readable and can be toggled for accessibility/testing.

**Expected impact**
- Runs become more unpredictable and memorable.

## Phase E (Week 5) — Narrative and pacing polish
**Goal**: improve mid-run structure and clarity.

**Tasks**
1. Split `NoteSystem` into `NoteSpawner`, `NoteCollector`, `NoteUI`.
2. Fix note display ordering so collected note == displayed note (unless intentionally sequenced).
3. Add pacing hooks in director based on notes found (e.g., milestone spikes).

**Definition of done**
- Notes no longer create sequencing confusion.
- Narrative progression supports tension pacing.

**Expected impact**
- Better 10-20 minute arc with clearer player motivation.

## Phase F (Week 6) — Replayability layer
**Goal**: make each run feel distinct with low content cost.

**Tasks**
1. Add run mutators (pick 2-3):
   - "Aggressive Stalker" (higher chase frequency)
   - "Faulty Soundboard" (misfires/overheat chance)
   - "Echoing Halls" (more false cues)
2. Add seed display for reproducibility and streamer sharing.
3. Add one extra rare event per run for surprise value.

**Definition of done**
- Back-to-back runs produce meaningfully different stories.
- Players can share seeds/challenge runs.

**Expected impact**
- Higher replayability and stream retention.

---

## Priority order if time is limited (must-have -> nice-to-have)
1. **Must-have**: Phase A + B + C (architecture + soundboard gameplay + chase quality).
2. **Should-have**: Phase D (sanity effects) for psychological flavor.
3. **Nice-to-have**: Phase E + F polish/replayability extras.

---

## Weekly cadence recommendation for solo dev
- **Mon-Tue**: implementation.
- **Wed**: integration and bug fixing.
- **Thu**: one focused playtest + balancing pass.
- **Fri**: lock build, write short changelog, tag version.

Keep each week to one core objective; avoid parallel major refactors.
