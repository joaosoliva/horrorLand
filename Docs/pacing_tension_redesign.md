# 5-Minute Pacing and Tension Redesign

This document redesigns the game's pacing from both a **game design** and **systems** perspective so a single run feels like a compact horror roller coaster instead of a flat walk through the maze.

## Core design goals

### Problems to solve
- The loop is too calm for too long.
- Jumpscares are too rare and feel disconnected.
- Enemy encounters do not give enough feedback or pressure.
- Emotional pacing is flat instead of rising and falling.
- The enemy sometimes feels passive rather than threatening.

### Target player feeling
The player should feel:
1. **unsafe within 20-30 seconds**,
2. **watched within the first minute**,
3. **pressured regularly every 15-25 seconds**,
4. **briefly relieved after peaks**,
5. **overwhelmed by a final peak before the run ends**.

### Pacing principle
The experience should follow a repeating pattern:

**build -> warning -> peak -> relief -> corrupted calm -> repeat**

The important difference from the current version is that **calm is no longer empty**. Even calm periods should contain suspicious audio, map uncertainty, distant movement, or small false cues.

---

# Revised 5-minute gameplay loop

## Loop targets
- Total run time: **~5 minutes**
- Number of major tension cycles: **4**
- Number of minor scare beats: **10-16**
- Maximum quiet gap without a notable beat: **20 seconds**
- Final minute should feel like a compression of the earlier loop: shorter relief, faster peaks, more enemy pressure.

## Timeline overview

### 0:00-0:30 — Unsafe introduction
**Goal:** establish danger immediately; no slow warmup.

**Player experience**
- Player gains control and hears unstable ambient audio right away.
- Within 10-15 seconds, they get the first sign the maze is inhabited.
- Within 20-30 seconds, they get a proximity hint or near miss.

**Event plan**
- Start in low light with audible environmental tension bed.
- Trigger one **minor foreshadowing event** at `8-15s`:
  - distant footstep,
  - quick silhouette crossing,
  - wall thump,
  - radio/static burst.
- Trigger one **enemy-presence beat** by `20-30s`:
  - audio sting,
  - map flicker,
  - flashlight instability,
  - fake line-of-sight flash.

**Design note**
Do **not** start with a full chase, but also do **not** allow 30 seconds of nothing.

---

### 0:30-1:15 — First build and first peak
**Goal:** confirm that the enemy matters.

**Player experience**
- Enemy becomes legible as a threat.
- First real danger should happen here.
- Player learns: proximity matters, sound matters, movement matters.

**Event plan**
- `0:35-0:50`: proximity cues begin if the enemy enters the same maze region.
- `0:45-1:00`: one **minor scare** fires if player remains unpressured.
- `0:55-1:15`: first **short burst chase** or near-chase sequence.

**Desired peak**
- Not a guaranteed kill scenario.
- Short, readable, intense.
- Followed by relief once line-of-sight is broken or the chase budget ends.

---

### 1:15-2:00 — Relief with contamination
**Goal:** give relief, but keep the player suspicious.

**Player experience**
- They are no longer in immediate danger.
- Audio drops or simplifies.
- The world still feels wrong.

**Event plan**
- Reduce enemy pressure for `10-15s`.
- Trigger one **psychological scare** during relief:
  - fake footstep behind the player,
  - distant laugh/static,
  - shadow in peripheral view,
  - note pickup produces whisper layer.
- Introduce one **soundboard relief beat** if applicable:
  - player-triggered sound gives comic release,
  - director uses that release to set up rebound tension.

**Design note**
Relief should never mean silence plus no content. It should mean: danger drops, uncertainty remains.

---

### 2:00-3:00 — Second cycle, stronger escalation
**Goal:** convert uncertainty into repeated pressure.

**Player experience**
- Enemy feels more active and more invasive.
- Jumpscares occur more often, but not in an obvious pattern.
- The player should feel hunted rather than simply sharing space with an AI.

**Event plan**
- Fire a **minor scare** every `20-30s` if no major event has happened.
- Increase chance of:
  - enemy reappearance ahead of player,
  - blocked route reveals,
  - false cue followed by real cue,
  - chase fakeout that becomes real after 3-6 seconds.
- Run one **prolonged pressure chase** between `2:20-2:50`.

**Desired peak**
- This is the first chase where the player feels the enemy is persistent.
- It should include stronger audio layers and clearer danger UI/audio feedback.

---

### 3:00-4:00 — Tight cycle repetition
**Goal:** increase frequency and shorten recovery.

**Player experience**
- Calm windows are now brief.
- The player rarely feels fully safe.
- Enemy presence should be obvious through sound, space, and pacing.

**Event plan**
- Hard cap quiet gaps to `15s`.
- Alternate:
  - one **minor scare**,
  - one **enemy presence cue**,
  - one **major pressure beat**.
- Jumpscares may happen here, but only if they are preceded by foreshadowing or contextual setup.

**Recommended beat sequence example**
- `3:05`: subtle danger cue
- `3:20`: false scare
- `3:32`: actual enemy reveal
- `3:45`: short chase burst
- `3:55`: silence/release for 5-8 seconds

---

### 4:00-5:00 — Final compression and end spike
**Goal:** deliver the strongest last minute.

**Player experience**
- The game feels like it is collapsing into panic.
- Relief is brief and unreliable.
- Enemy feels close, unavoidable, and personal.

**Event plan**
- Increase tension floor so the run never returns to true calm.
- Reduce chase cooldowns.
- Increase frequency of proximity audio and visual corruption.
- Allow one **major jumpscare** and several **minor scares**.
- End with one final strong peak in the last `20-30s`:
  - chase into exit,
  - forced corridor pass,
  - false relief into final reveal,
  - scripted final audio sting + visual confirmation.

**Ending rule**
The final 30 seconds must feel louder, faster, and less safe than anything before it.

---

# New and modified systems

## 1. HorrorDirector (modified/expanded)
**Purpose:** become the real pacing brain.

### Responsibilities
- Track current tension from `0-100` instead of a soft passive float only.
- Track pacing phase:
  - `Calm`
  - `Build`
  - `Threat`
  - `Peak`
  - `Relief`
  - `Finale`
- Enforce a max quiet interval.
- Request scares/events when the game has gone too long without a beat.
- Lower tension after peaks, but not all the way to zero.

### New tunable fields
- `float maxQuietSeconds = 20f`
- `float desiredBeatInterval = 12f`
- `float tensionFloorByMinute[]`
- `float reliefDurationMin/Max`
- `float peakCooldown`
- `float finaleStartTime = 240f`

### MVP logic
If no meaningful event has happened in `maxQuietSeconds`, the director should force one of:
- minor scare,
- enemy reveal cue,
- route pressure cue,
- soundboard rebound cue.

---

## 2. ScareScheduler (new)
**Purpose:** manage beat frequency and avoid repetition.

### Responsibilities
- Maintain cooldowns for scare categories.
- Pick the next event based on current phase and recent history.
- Prevent the same scare type from repeating too often.

### Scare categories
- `MinorPsychological`
- `PresenceCue`
- `Fakeout`
- `MajorJumpscare`
- `ChaseTrigger`
- `ReliefBeat`

### Required data
- last trigger time per category
- recent scare history queue
- current phase from `HorrorDirector`
- current enemy distance band

### MVP rules
- No identical scare category twice in a row.
- At least one minor/presence event every `15-25s`.
- Major jumpscare only if a warning or threat cue happened in the prior `3-8s`.

---

## 3. JumpscareSystem (modified)
**Purpose:** split jumpscares into major and minor scares.

### Major jumpscares
High impact. Use sparingly, but more often than now.
Examples:
- full-screen flash + enemy face + hit sting,
- sudden close reveal when turning corner,
- enemy hand/body snaps into frame.

### Minor scares
Low cost, frequent, psychological.
Examples:
- screen edge flicker,
- whisper in one ear,
- brief shadow crossing,
- warning text/static pulse,
- fake footstep behind player.

### New tunable fields
- `int maxMajorScaresPerRun = 3`
- `Vector2 minorScareIntervalRange = new Vector2(12f, 22f)`
- `Vector2 majorScareIntervalRange = new Vector2(35f, 65f)`
- `float foreshadowWindow = 6f`
- `float antiRepeatWindow = 20f`

### Design rule
A major jumpscare should almost always be preceded by a readable warning signal, even if subtle.

---

## 4. ThreatFeedbackSystem (new)
**Purpose:** give clear feedback when danger is near.

### Responsibilities
- Translate enemy distance/state into audiovisual feedback.
- Feed diegetic warnings instead of only UI warnings.

### Output examples
- heartbeat layer,
- breathing intensity,
- radio static,
- flashlight flicker,
- camera sway increase,
- environment creaks near enemy.

### Distance bands
- `Far`: no direct feedback, just ambient corruption.
- `Near`: faint directional threat cue.
- `Danger`: strong audio pulse, breathing, visual pressure.
- `Immediate`: chase alarm/sting and aggressive feedback.

### MVP implementation idea
A single component subscribes to `VillainAI` + `HorrorDirector` and drives:
- screen vignette amount,
- heartbeat loop volume,
- low-pass filter amount,
- flashlight instability,
- optional HUD icon pulse.

---

## 5. EnemyPresenceController (new or split from AI)
**Purpose:** make the enemy feel active even when not chasing.

### Responsibilities
- Trigger non-chase appearances.
- Stage near misses.
- Teleport/reposition for visibility moments.
- Request route pressure events.

### Presence events
- visible at end of corridor,
- disappears when player approaches,
- audible behind wall,
- crosses an intersection,
- appears near objective then retreats.

### MVP rule
If the player has not had a real enemy-presence beat in `25s`, force a presence cue if safe to do so.

---

## 6. ChaseSystem (modified)
**Purpose:** support readable, escalating chases rather than just start/stop logic.

### Required chase types
- **Short Burst**: 4-8 seconds, sharp panic spike, fast relief.
- **Prolonged Pressure**: 10-18 seconds, stronger audio escalation, path denial, repeated re-spotting.
- **Fake Chase**: sting + rush + loss of contact, used sparingly.

### Tunable values
- `shortBurstDurationRange`
- `prolongedPressureDurationRange`
- `fakeChaseChance`
- `chaseCooldown`
- `graceReacquireTime`
- `finaleChaseBias`

### Design rule
Every chase should have a purpose:
- teach,
- peak,
- interrupt relief,
- or deliver finale pressure.

---

## 7. AudioTensionManager / SoundboardManager (modified/new)
**Purpose:** make audio the primary driver of build, release, and foreshadowing.

### Dynamic layers
1. **Base ambient** — always present, low intensity
2. **Tension layer** — fades in during build/threat
3. **Proximity layer** — tied to enemy distance
4. **Peak layer** — active during chase/jumpscare windows
5. **Relief layer** — low, eerie reset after peaks
6. **Silence cut** — intentional drop before some major scares

### Audio rules
- Never keep all layers active all the time.
- Silence is a weapon: cut audio right before important spikes.
- Relief needs a distinct audio state, not just lower volume.
- Soundboard should be dual-purpose: humor release + danger bait.

### Soundboard redesign
Each sound button should expose:
- `loudness`
- `humorReliefValue`
- `threatAttraction`
- `cooldown`
- `misfireChance` (optional)

This creates a loop where sound gives emotional release but may worsen danger.

---

## 8. RoutePressureSystem (new)
**Purpose:** keep navigation stressful without needing a constant chase.

### Responsibilities
- Identify safe-ish route segments.
- Occasionally invalidate a route with pressure cues.
- Pull the player toward a risky area or away from certainty.

### Examples
- door slam nearby,
- corridor light flicker ahead,
- distant enemy sound from the route the player planned to use,
- minimap static or temporary blackout,
- note/goal room becomes acoustically hostile.

### MVP rule
Use route pressure once per cycle to stop the run from becoming a pure walk simulator between chases.

---

## 9. FinaleController (new)
**Purpose:** own the last 45-60 seconds.

### Responsibilities
- Raise tension floor.
- Shorten recovery windows.
- Bias the scheduler toward threat and chase beats.
- Guarantee one final memorable payoff.

### Finale behavior
- Boost enemy presence frequency.
- Increase chance of a major scare.
- Reduce silent downtime to almost zero.
- Force a final corridor/exit confrontation.

---

# Specific implementation suggestions (Unity / C# level)

## Event model
Use lightweight events or ScriptableObject channels.

### Useful events
```csharp
public static event Action<float> OnTensionChanged;
public static event Action<HorrorPhase> OnPhaseChanged;
public static event Action<ScareType> OnScareTriggered;
public static event Action<EnemyDistanceBand> OnThreatBandChanged;
public static event Action OnMajorPeakStarted;
public static event Action OnMajorPeakEnded;
public static event Action OnFinaleStarted;
public static event Action<SoundTag, float> OnSoundboardPlayed;
```

## Recommended enums
```csharp
public enum HorrorPhase
{
    Calm,
    Build,
    Threat,
    Peak,
    Relief,
    Finale
}

public enum ScareType
{
    MinorPsychological,
    PresenceCue,
    Fakeout,
    MajorJumpscare,
    ChaseTrigger,
    ReliefBeat
}

public enum EnemyDistanceBand
{
    Far,
    Near,
    Danger,
    Immediate
}
```

## HorrorDirector update loop suggestion
```csharp
void Update()
{
    runtimeSeconds += Time.deltaTime;
    UpdateMinuteBias();
    UpdateEnemyPressure();
    UpdateTensionDecay();
    EvaluatePhase();

    if (Time.time - lastMeaningfulBeatTime >= maxQuietSeconds)
    {
        scareScheduler.RequestCatchUpBeat();
    }

    if (runtimeSeconds >= finaleStartTime && !finaleStarted)
    {
        StartFinale();
    }
}
```

## ScareScheduler selection sketch
```csharp
public ScareType SelectNextScare(HorrorPhase phase)
{
    List<ScareType> candidates = GetCandidatesForPhase(phase);
    candidates.RemoveAll(type => IsOnCooldown(type) || IsRecentRepeat(type));

    if (candidates.Count == 0)
        return ScareType.PresenceCue;

    return WeightedRandom(candidates);
}
```

## Threat feedback calculation
```csharp
float distance = Vector3.Distance(player.position, villain.position);
EnemyDistanceBand band = distance switch
{
    > 18f => EnemyDistanceBand.Far,
    > 10f => EnemyDistanceBand.Near,
    > 5f  => EnemyDistanceBand.Danger,
    _     => EnemyDistanceBand.Immediate,
};
```

## Minor scare trigger example
```csharp
if (phase == HorrorPhase.Build && Time.time >= nextMinorScareTime)
{
    TriggerMinorScare(MinorScareType.FootstepBehindPlayer);
    nextMinorScareTime = Time.time + Random.Range(12f, 20f);
}
```

## Presence cue trigger example
```csharp
if (Time.time - lastPresenceCueTime > 25f && !villainAI.IsChasing)
{
    enemyPresenceController.TrySpawnVisibleCueAheadOfPlayer();
}
```

## Major scare gate example
```csharp
bool canMajorScare =
    timeSinceLastForeshadow <= foreshadowWindow &&
    timeSinceLastMajorScare >= majorScareCooldown &&
    currentPhase != HorrorPhase.Relief;
```

---

# Examples of triggers, events, and timing

## Trigger matrix by phase

### Calm
**Allowed**
- ambient instability
- distant enemy sound
- brief visual anomaly
- map flicker

**Not allowed**
- repeated major scares
- long chase chains

### Build
**Allowed**
- footsteps behind player
- partial reveal at corridor end
- flashlight flicker
- near-but-not-seen audio cue

**Timing**
- at least one event every `12-18s`

### Threat
**Allowed**
- route denial cue
- clear enemy proximity sound
- fakeout sting
- pre-jumpscare foreshadow

**Timing**
- at least one event every `10-15s`

### Peak
**Allowed**
- chase
- major jumpscare
- double audio layer escalation
- flashlight instability spike

**Timing**
- 1 major event plus optional supporting cue

### Relief
**Allowed**
- low ambient recovery layer
- one contaminated calm cue after `6-10s`
- no immediate second major scare unless in finale

### Finale
**Allowed**
- all systems biased toward pressure
- faster cycles
- stronger route denial
- guaranteed final peak sequence

---

## Example 45-second micro-cycle
This is the building block for the 5-minute run.

### Seconds 0-8
- ambient build
- faint proximity cue

### Seconds 8-14
- minor psychological scare

### Seconds 14-22
- visible or audible enemy presence cue

### Seconds 22-30
- warning signal: silence cut, static swell, flashlight pulse

### Seconds 30-38
- peak event: short chase or major scare

### Seconds 38-45
- relief state with contaminated calm

Repeat with stronger intensity each cycle.

---

## Example event chains

### Chain A: Enemy nearby build
1. distant metal hit
2. left-channel footstep
3. flashlight flicker
4. silhouette at T-junction
5. enemy disappears
6. chase begins 4 seconds later

### Chain B: Fake relief into scare
1. chase ends
2. audio drops to near silence
3. breathing settles
4. player rounds corner
5. minor scare fires
6. enemy sound reappears behind wall

### Chain C: Finale pressure chain
1. objective almost complete
2. ambient cuts out
3. route ahead flickers
4. enemy appears in exit lane
5. major sting
6. prolonged chase
7. final reveal or escape trigger

---

# MVP implementation order

## Week 1 / MVP-first order
1. Expand `HorrorDirector` into real phase-based pacing control.
2. Add `ScareScheduler` with cooldowns and anti-repeat logic.
3. Split `JumpscareSystem` into minor vs major scares.
4. Add `ThreatFeedbackSystem` for proximity feedback.
5. Improve `ChaseSystem` with short burst / prolonged pressure / fake chase roles.
6. Add `FinaleController` for the last minute.

## If time is limited
Prioritize these in order:
1. `HorrorDirector` pacing enforcement
2. `ScareScheduler`
3. `ThreatFeedbackSystem`
4. `JumpscareSystem` major/minor split
5. `FinaleController`

That order gives the biggest improvement to pacing fastest.

---

# Success criteria

The redesign is working if, in a 5-minute test run:
- the player experiences a notable beat at least every `15-20s`,
- there are at least `3` memorable high-intensity moments,
- enemy proximity is readable even without direct line-of-sight,
- jumpscares feel more frequent but not spammed,
- the last minute feels meaningfully more intense than the first,
- players describe the run as stressful, unpredictable, and "active" rather than quiet or empty.
