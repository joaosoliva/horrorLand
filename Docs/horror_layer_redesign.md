# Horror Layer Redesign Plan

This document redesigns the current horror layer so the maze experience becomes **frightening, surprising, and barely survivable** without replacing the existing movement/controller foundation. It is written against the current prototype architecture in `Assets/Scripts/Maze`, so the recommendations can be implemented as an MVP in the current Unity project.

---

## Core design changes (high-level)

### 1. Shift the game from constant tension to directed fear
The current project already has a strong tension scaffold via `HorrorDirector`, `ScareScheduler`, `ChaseSystem`, `JumpscareSystem`, `ThreatFeedbackSystem`, and `VillainAudio`. The main problem is that too much of the experience is currently **legible as game pressure** instead of **unpredictable dread**.

**Redesign principle:**
- Tension should be the baseline.
- Fear should come from **uncertainty, misdirection, sudden absence/presence changes, and temporary loss of confidence**.
- The villain should feel like it is **deciding when to show itself**, not simply failing to find the player.

### 2. Reframe villain visibility as a resource
Right now the villain can become too mechanically readable because the systems are heavily driven by distance, visibility, and timing windows. Instead of the monster being a mostly persistent pressure source, treat its visibility as a **budgeted dramatic resource**.

**New rule set:**
- The villain is usually **heard, implied, or almost seen**.
- Full visual confirmation should be **rare and meaningful**.
- Every reveal should answer one question while creating another:
  - “Was that it?”
  - “Why did it stop chasing?”
  - “Did it go ahead of me?”

### 3. Introduce controlled loss of control
Fear rises when the player loses certainty, not necessarily agency. Keep controls intact, but temporarily undermine player trust in the environment.

**MVP examples:**
- Flashlight flicker during nearby presence.
- A door that closes behind the player only during a threat beat.
- Briefly wrong directional audio.
- A fake footstep or shadow crossing that causes hesitation.
- A hiding spot that is safe only for a short duration.

### 4. Make the chase survivable, but only by reading the situation correctly
The chase should stop being a pure speed check. A chase is most frightening when the player believes they **might** survive if they stay calm and choose correctly.

**Core chase fantasy:**
- Sprinting is only the first response.
- Survival comes from **breaking sight**, **using the maze**, **hiding in short windows**, or **reaching temporary safety pockets**.
- Chases should have **peaks and valleys**, not uninterrupted maximum pressure.

### 5. Build a true horror loop instead of a scare timer loop
The emotional rhythm should become:

**Calm → Suspicion → Fear → Release → Corrupted Calm → Repeat**

The crucial change is that “calm” is never empty. Calm should contain evidence that the player is not alone.

---

## Revised encounter / chase system

### A. Encounter structure: the villain should feel intelligent
Replace “spawn near / become visible / chase” as the default structure with a layered encounter model.

#### Encounter type 1: Presence encounter
**Purpose:** make the player feel watched.

**Pattern:**
1. Director enters **Suspicion** state.
2. Audio or environmental cue fires first.
3. Villain is positioned in a place the player can miss if they are careless.
4. If the player notices it, the villain vanishes or retreats instead of instantly chasing.

**Examples:**
- The villain stands motionless at the end of a corridor for 0.75 seconds, then the lights drop and it is gone.
- A footstep pattern tracks the player’s movement but never appears visually.
- A shadow slides across a junction the player is about to enter.

**Design effect:** the player starts scanning space and second-guessing themselves.

#### Encounter type 2: Probe encounter
**Purpose:** test player reaction and create anticipation.

**Pattern:**
1. Villain creates a cue near the player.
2. The system measures player response:
   - did they run,
   - look around,
   - freeze,
   - use a hiding spot,
   - change route.
3. Based on the response, the villain either escalates or withholds.

**Examples:**
- Sudden heavy footsteps stop just outside line of sight.
- Breathing is heard behind the player, but turning too quickly reveals nothing.
- The villain appears ahead, forcing a route decision, but does not commit to a chase yet.

**Design effect:** makes the monster feel reactive rather than scripted.

#### Encounter type 3: Commitment encounter
**Purpose:** deliver the actual danger peak.

**Pattern:**
1. Buildup occurs first.
2. Reveal is sharp and readable.
3. Chase or hard scare begins.
4. The player gets one or two valid survival options.

**Rule:** commitment encounters should be **rare enough** that the player cannot assume every cue becomes a chase.

---

### B. New chase structure: winnable by skillful composure
The current chase pressure should be reworked into three phases.

#### Phase 1: Snap reveal
Duration: **0.5–2.0 seconds**

**What happens:**
- Villain reveals suddenly with a strong audio sting and a direct visual.
- Player gets a short shock window before full pursuit speed begins.

**Why:**
This creates fear without making the chase feel unfair. The player understands the threat before maximum pressure starts.

#### Phase 2: Hot pursuit
Duration: **4–10 seconds** depending on pattern

**What happens:**
- Villain speed is high but not constant.
- It gets a short burst advantage when line of sight is maintained.
- If the player turns corners well or cuts sight, the villain briefly drops into a search state.

**Key rule:**
The villain should be strongest when it has visual confirmation, not universally faster at all times.

#### Phase 3: Search / disengage / re-acquire
Duration: **2–8 seconds**

**What happens:**
- If the player breaks sight, the villain does not instantly know the answer.
- It searches the last known area, checks common hiding points, and may reappear from another route.
- If the player uses the environment well, the chase ends.
- If they panic and run noisily, the chase can restart.

**Why:**
This turns survival into a tense puzzle instead of an unwinnable sprint.

---

### C. Escape mechanics to add

#### 1. Line-of-sight break bonus
When the player breaks sight for a short threshold, reduce villain confidence.

**MVP behavior:**
- After `1.25–2.0s` without visual contact, villain speed drops.
- After `2.5–4.0s`, AI switches from `Chasing` to `Searching`.
- Director enters a short **post-peak release** state.

#### 2. Short hiding windows
Add a small number of contextual hiding points or dark pockets.

**Important rule:** hiding is not permanent safety.
- Safe for a few seconds.
- Overused hiding spots get checked more aggressively.
- Hiding is strongest after line-of-sight is already broken.

#### 3. Temporary safety zones
Create rare “breathing rooms” where the villain cannot fully commit for a few seconds.

**Examples:**
- A lit shrine corner.
- A room with a slammed lock mechanic.
- A sound-emitting machine that masks the player for a moment.

These should be brief, unreliable, and scarce.

#### 4. Stamina-less survival tools
Avoid adding complex stamina unless absolutely necessary. Instead, reward spatial decisions.

Better options for MVP:
- cornering well,
- turning off flashlight,
- crouching in darkness,
- choosing split routes,
- using noise sources to misdirect.

---

### D. Variable villain behavior
The villain should not feel like a single-speed NavMesh missile.

#### Behavior modes to rotate between
1. **Watcher**
   - observes from distance,
   - rarely chases,
   - prioritizes silhouette reveals.
2. **Predator**
   - aggressively closes distance,
   - strong chase potential,
   - fewer fakeouts.
3. **Trickster**
   - creates false cues,
   - repositions ahead of player,
   - uses route pressure and fake retreats.

**MVP implementation idea:**
The director picks a behavior mode for the next 30–60 seconds, influencing spawn rules, cue selection, and chase chance.

---

## Jumpscare redesign

### 1. Hard jumpscares: rare, high-value, well-earned
Hard jumpscares should only occur when one of these is true:
- the player ignored multiple warnings,
- the player made a bad route decision during a peak,
- the player thought they were safe after a release,
- the finale is compressing the loop.

**Hard scare formula:**
1. foreshadowing,
2. short silence or narrowing of information,
3. sudden reveal at close range,
4. immediate consequence or chase transition.

**Do not** let the player see hard scares on a predictable timer.

### 2. Soft scares: the primary fear engine
Soft scares should be far more common than hard jumpscares.

**Good soft scare examples for this project:**
- movement in peripheral vision,
- a silhouette behind a wall opening,
- footsteps that stop when the player stops,
- a shadow crossing the flashlight beam,
- breathing from the wrong direction,
- route lighting briefly dimming where the player is headed,
- a false “safe” audio bed interrupted by a single close sound.

### 3. False threat beats
Not every buildup should pay off with danger.

**Why it matters:**
If every cue becomes a chase, the player learns the rule and fear drops. Some cues should resolve into nothing, so the player cannot confidently decode the system.

### 4. Repetition guardrails
Create category-level cooldowns, not just scare cooldowns.

**Suggested categories:**
- silhouette scare,
- audio close-pass,
- route pressure,
- fake chase,
- hard jumpscare,
- environmental manipulation.

This prevents the horror from feeling procedural in a bad way.

---

## Feedback and readability redesign

### Audio-first readability
The current project already has a strong base for diegetic feedback. Lean further into it.

#### What the player should learn from audio
- **Distance**: heartbeat, breathing, low-end rumble.
- **Direction**: localized footsteps, wall knocks, metallic scrapes.
- **Intent**: a sound difference between “watching,” “closing,” and “searching.”
- **Safety**: room tone or temporary relief bed when danger truly drops.

#### Key change
Avoid generic “threat is high” feedback. Instead, communicate **what kind of threat is happening**.

For example:
- slow breath = near but passive,
- quick heavy footfalls = approach,
- scraping or static flares = teleport/reposition risk,
- silence after pressure = probable setup for reveal.

### Visual readability without UI bloat
The current visual feedback system appears to rely heavily on vignette/intensity layering. Keep that, but reduce overt “gamey” signalling.

#### Better visual signals
- light flicker near villain pathing,
- shadow passes at intersections,
- brief occlusion in peripheral vision,
- flashlight instability only during certain encounter states,
- environmental props slightly displaced after the villain has moved through.

#### Avoid overusing
- giant text warnings,
- constant red flashes,
- always-on threat overlay.

The player should feel like they are reading the world, not a HUD.

---

## Pacing and structure

### Target runtime loop
Each 4–6 minute run should contain:
- **2–3 presence encounters**,
- **2 probe encounters**,
- **2 real chase events**,
- **1 hard jumpscare at most**,
- **multiple soft scares with spacing and variation**.

### Emotional loop target

#### Calm
- No direct threat.
- World still feels wrong.
- One suspicious cue every 10–20 seconds.

#### Suspicion
- Something is definitely nearby.
- Player begins checking corners and stopping movement.

#### Fear
- Villain reveals, path is compromised, or a chase begins.
- Audio becomes sharply directional.

#### Release
- Player survives or the threat withdraws.
- Some pressure drops, but certainty does not fully return.

#### Corrupted calm
- The player is functional again, but trust is lower than before.
- The next cycle escalates in a different way.

### Anti-predictability rule
No two consecutive peaks should have the same structure.

Examples:
- silhouette → chase,
- false chase → no reveal,
- route block → hard scare,
- pursuit → escape → delayed reappearance ahead.

---

## Example gameplay scenarios

### Scenario 1: First real fear beat
**Goal:** teach the player the villain chooses its moments.

1. Player explores in relative calm.
2. A distant metallic scrape is heard behind two walls.
3. Nothing happens for 6 seconds.
4. The flashlight flickers once when turning a corner.
5. At the far end of the corridor, the villain is visible for less than a second.
6. Lights dim slightly.
7. When the player advances to confirm, the space is empty.
8. Ten seconds later, footsteps begin behind the player.

**Why it works:**
No direct chase occurs, but the player learns the maze is occupied and unstable.

### Scenario 2: Winnable short-burst chase
**Goal:** create fear, not frustration.

1. Player hears close breathing while entering a junction.
2. Silence.
3. Villain lunges into view from the right branch with a sharp audio sting.
4. It sprints aggressively while in line of sight.
5. Player cuts left twice and breaks sight.
6. Villain enters search behavior and slows.
7. Player slips into a shadow pocket and holds still.
8. Footsteps pass by, then fade.
9. Relief bed fades in for 3 seconds.

**Why it works:**
The player survives because they understood line-of-sight and hiding, not because the AI arbitrarily gave up.

### Scenario 3: Fake relief into hard scare
**Goal:** make rare hard scares land harder.

1. After escaping, the player enters a softly lit safe-feeling area.
2. Heartbeat and distortion fade almost completely.
3. A note or objective interaction begins.
4. Environmental sound fully drops out for half a second.
5. Villain appears directly behind the interaction point with a full-frame hard scare.
6. Instead of killing instantly, it transitions into a short chase.

**Why it works:**
The scare is earned by the relief beforehand and remains interactive instead of purely punitive.

### Scenario 4: Trickster route pressure
**Goal:** make the villain feel intelligent.

1. Player hears running on the left route.
2. They choose the right route to avoid it.
3. A shadow crosses the right corridor ahead.
4. The left route goes silent.
5. The villain reveals ahead on the right, forcing a retreat.
6. Returning left now triggers the real pursuit.

**Why it works:**
The monster appears to have manipulated the player’s decision.

---

## Suggested Unity systems and scripts to modify or create

## Modify: `Assets/Scripts/Maze/HorrorDirector.cs`
**Current role:** tension and pacing authority.

**Add / change:**
- Replace pure tension scheduling with **encounter state orchestration**.
- Track a new `EncounterMode`:
  - `None`,
  - `Presence`,
  - `Probe`,
  - `Commitment`,
  - `Release`.
- Track a temporary **behavior profile**:
  - `Watcher`, `Predator`, `Trickster`.
- Add anti-repetition memory per encounter category.
- Add “uncertainty budget” so the director can intentionally choose a non-payoff buildup.
- Control reveal rarity instead of only beat frequency.

**MVP outcome:** the director stops behaving like a tension metronome and starts behaving like a horror dramaturgy system.

## Modify: `Assets/Scripts/Maze/ScareScheduler.cs`
**Current role:** schedules scare beats by phase.

**Add / change:**
- Schedule by **encounter intent** rather than raw `ScareType` only.
- Add category cooldowns:
  - `audioFakeout`,
  - `silhouetteReveal`,
  - `routePressure`,
  - `environmentShift`,
  - `hardScare`.
- Allow some beats to intentionally **resolve into nothing**.
- Bias against major scares unless the buildup state has been satisfied.

**MVP outcome:** scares stop feeling like randomized event cards.

## Modify: `Assets/Scripts/Maze/ChaseSystem.cs`
**Current role:** manages chase patterns and cooldowns.

**Add / change:**
- Split chase into explicit sub-states:
  - `Reveal`, `Pursuit`, `Search`, `Disengage`.
- Add line-of-sight decay and confidence loss.
- Add a “barely survivable” balance model:
  - faster while visible,
  - slower when uncertain,
  - temporary burst on reacquire.
- Add short safety windows after successful breakaway.
- Add escape success event for the director to trigger release pacing.

**MVP outcome:** chase becomes scary but readable and winnable.

## Modify: `Assets/Scripts/Maze/VillainAI.cs`
**Current role:** movement, detection, spawn logic, dread teleporting, state transitions.

**Add / change:**
- Separate **spawn intent** from patrol/chase behavior.
- Add reveal-specific spawn validation:
  - behind junctions,
  - edge of flashlight range,
  - corridor termini,
  - same region but outside current view cone.
- Add search heuristics for hiding and last known path prediction.
- Add temporary “don’t commit” behavior for presence/probe encounters.
- Add telegraphable reposition logic instead of silent random reappearance.

**MVP outcome:** the villain feels deliberate and intelligent.

## Modify: `Assets/Scripts/Maze/JumpscareSystem.cs`
**Current role:** handles major/minor scares with distance gating.

**Add / change:**
- Separate **soft scare library** from **hard scare library**.
- Require buildup tags before hard scare eligibility.
- Convert on-screen text warnings into optional debug/development tools, not primary player-facing horror delivery.
- Add environmental scare triggers:
  - shadow pass,
  - flashlight dip,
  - sound burst behind player,
  - silhouette pop.

**MVP outcome:** fewer cheap scares, more effective ones.

## Modify: `Assets/Scripts/Maze/VillainAudio.cs`
**Current role:** audio layers based on distance and phase.

**Add / change:**
- Drive audio from villain intent, not just distance.
- Add distinct cue families:
  - stalking cues,
  - approach cues,
  - search cues,
  - fakeout cues,
  - post-escape release cues.
- Add occasional silence windows before hard reveals.
- Use spatial one-shots for misleading directionality during probe encounters.

**MVP outcome:** the player can “read” danger from audio alone.

## Modify: `Assets/Scripts/Maze/ThreatFeedbackSystem.cs`
**Current role:** vignette + loops for threat intensity.

**Add / change:**
- Reduce heavy UI-style flashing.
- Shift toward subtle, world-adjacent effects:
  - flashlight flicker,
  - camera micro-lurch,
  - edge shadowing,
  - brief peripheral dimming.
- Add separate values for **certainty** and **danger** so feedback can express “something is wrong” without fully exposing enemy distance.

**MVP outcome:** better fear readability with less HUD feel.

## Create: `Assets/Scripts/Maze/EncounterDirector.cs`
**Purpose:** coordinate multi-step encounter sequences.

**Responsibilities:**
- Pick encounter type.
- Reserve the next reveal position.
- Trigger audio/environmental telegraphs.
- Tell `VillainAI` whether the encounter should observe, probe, or commit.
- Report outcome back to `HorrorDirector`.

**Why:**
This avoids stuffing sequence logic directly into `HorrorDirector`.

## Create: `Assets/Scripts/Maze/HidingSpot.cs`
**Purpose:** define short-term escape nodes.

**Responsibilities:**
- Store concealment quality.
- Store max safe duration.
- Expose whether it has been used recently.
- Notify villain search logic if overused.

## Create: `Assets/Scripts/Maze/EnvironmentScareController.cs`
**Purpose:** handle non-villain world scares.

**Examples:**
- light flicker groups,
- distant door slam,
- prop movement,
- shadow projector burst,
- audio emitters at intersections.

This keeps soft scares modular and cheap to author.

## Create: `Assets/Scripts/Maze/PlayerFearState.cs`
**Purpose:** centralize fear-facing gameplay data.

**Track:**
- time since last visual contact,
- time since last safe zone,
- confidence level / uncertainty level,
- recent escape success,
- recent false alarm count.

This can help the director decide whether to escalate or withhold.

---

## Key variables and parameters to tune

## Director / pacing
- `maxQuietSeconds`: 8–16
- `presenceEncounterChance`: 0.35–0.6
- `probeEncounterChance`: 0.25–0.45
- `commitmentEncounterChance`: 0.15–0.3
- `nonPayoffBuildupChance`: 0.2–0.4
- `hardScareMaxPerRun`: 1–2
- `releaseWindowSeconds`: 4–10
- `corruptedCalmMinSeconds`: 6–15

## Villain reveal logic
- `rareRevealMinInterval`: 20–45s
- `maxContinuousVisibilitySeconds`: 0.75–2.5s outside chase
- `revealDistanceMin`: 8m
- `revealDistanceMax`: 18m
- `behindPlayerRevealChance`: low, use sparingly
- `aheadOfPlayerRevealChance`: medium-high
- `junctionRevealChance`: high

## Chase tuning
- `revealFreezeWindow`: 0.35–0.8s
- `basePursuitSpeed`: slightly below current unwinnable pressure
- `losBurstSpeedBonus`: +10–25%
- `searchSpeedMultiplier`: 0.65–0.8
- `lineOfSightBreakTime`: 1.25–2.0s
- `fullSearchTransitionTime`: 2.5–4.0s
- `reacquireBurstDuration`: 1.0–2.0s
- `safeZoneProtectionTime`: 3–6s

## Hiding
- `hideSafeDuration`: 2–5s
- `hideDetectionGrace`: 1–2s
- `reusedHidePenalty`: medium/high
- `villainCheckHideChance`: starts low, rises with repeated use

## Jumpscare budget
- `softScareInterval`: 10–25s depending on phase
- `hardScareCooldown`: 60–120s
- `falseThreatRate`: 30–50% of buildup beats
- `silhouetteCooldown`: 25–40s
- `environmentShiftCooldown`: 15–30s

## Audio
- `stalkCueRadius`: 14–20m
- `approachCueRadius`: 8–14m
- `searchCueRadius`: 5–10m
- `silenceBeforeReveal`: 0.2–1.0s
- `directionalAudioErrorChance`: low/moderate during probe only

## Readability / feedback
- `flashlightFlickerChanceNearPresence`: 0.15–0.35
- `shadowPassChanceAtJunction`: 0.2–0.4
- `heavyVignetteThreshold`: reserve for peak only
- `cameraLurchStrength`: subtle, not disorienting

---

## Recommended MVP implementation order

### Phase 1: Make chases winnable and reveals rarer
1. Rework `ChaseSystem` for line-of-sight break and search fallback.
2. Add reveal cooldown / visibility budgeting to `VillainAI` and `HorrorDirector`.
3. Reduce constant villain visibility outside true commitment encounters.

### Phase 2: Improve fear grammar
1. Add `EncounterDirector`.
2. Expand `ScareScheduler` to choose encounter intents and non-payoff buildups.
3. Add 4–6 soft scare environmental events through `EnvironmentScareController`.

### Phase 3: Improve readability and variety
1. Refactor audio into stalking/approach/search/release sets.
2. Add hiding spots and temporary safety pockets.
3. Replace UI-heavy warnings with diegetic cues.

### Phase 4: Final polish
1. Add 1–2 premium hard scares.
2. Tune repetition protection.
3. Tune finale compression so the end becomes fast, oppressive, and survivable.

---

## Final target experience
When this redesign is working, the player should feel:
- the maze is never fully safe,
- the villain is choosing its moments,
- every reveal matters,
- every chase is terrifying but not hopeless,
- survival depends on perception and composure,
- and the horror comes from uncertainty first, mechanics second.
