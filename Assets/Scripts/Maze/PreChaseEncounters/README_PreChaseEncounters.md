# Pre-Chase Encounter System (Phase 8 Hand-off)

This document is the phase-8 hand-off checklist for the pre-chase system.

## Components
- `EncounterManager`
- `MazeContextQuery`
- `BehindBackEncounter`
- `CornerEncounter`
- `LongHallEncounter`

## Required scene wiring
1. Assign `VillainAI.preChaseEncounterManager`.
2. Assign `EncounterManager` references:
   - Villain AI
   - Player transform
   - ChaseSystem
   - JumpscareSystem
   - MazeContextQuery
   - GameManager (optional but recommended for `requireGameActive`)
3. Assign `MazeContextQuery` references:
   - MazeGenerator
   - Initial room volume OR initial room root
   - Start-room `DoorTrigger` (for exit-gate behavior)
4. Add encounter components to `EncounterManager.encounters` list.

## Runtime gates (expected)
- Initial grace period blocks encounters.
- Initial room blocks encounters.
- Safe zone blocks encounters.
- Start-room exit gate can block encounters.
- Game-ended state blocks encounters when `requireGameActive` is enabled.

## Back encounter behavior (expected)
- Selection creates pending state.
- Trigger requires player turn angle threshold.
- Pending state expires after max pending time.

## Debug tools
- `EncounterManager.drawDebugOverlay` for HUD status.
- `EncounterManager` gizmos for hall/corner points.
- Context menu:
  - `Phase 7/Checklist: Log Encounter Runtime Status`

## Verification pass (manual)
1. Stay in start room: encounters and capture should be blocked.
2. Leave room: encounter eligibility should begin (after grace period).
3. Stand in short hall: long-hall encounter should reject.
4. Stand near true corner: corner encounter should become valid.
5. Trigger pending back encounter and rotate: it should only fire after threshold turn.
6. Enter safe zone: encounters should be blocked/cancelled.

## Known limitations
- This phase does not add Unity automated tests yet; validation is currently runtime-debug driven.
