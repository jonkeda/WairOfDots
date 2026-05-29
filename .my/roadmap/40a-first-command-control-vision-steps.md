# First Command And Control And Vision Steps

Status: implemented.

Parent roadmap: [40-command-information-neat-roadmap.md](40-command-information-neat-roadmap.md)

Unit reference: [.my/UnitProperties.md](../UnitProperties.md)

## Goal

Build the smallest useful command-and-control system with vision.

This is not the full command catalog. It is the first minor, testable path that proves:

- Commands travel top down.
- Reports travel bottom up.
- Units belong to one Commander or reserve.
- Commanders have visible numbers.
- Vision gates enemy information.
- The system is deterministic before NEAT integration expands it.

## First-Slice Rules

- Use strict hierarchy only.
- Use one active executable command per Commander and per unit.
- Keep the full command catalog in roadmap `40`, but implement only the minimum subset here.
- Do not add command queues yet.
- Do not draw command lines by default.
- Do not add real SharpNEAT yet.
- Do not require long training runs.

## Minimal Command Set

### General To Commander

Use only:

- `AttackRegion`
- `HoldRegion`

Why this is enough:

- `AttackRegion` proves downward intent and movement toward contested ground.
- `HoldRegion` proves the General can stop or stabilize a Commander without adding more tactics.

### General To Reserve

Use only:

- `AssignReserveUnit`
- `RecallCommanderUnitToReserve`

Why this is enough:

- It proves unassigned units are reserve.
- It proves the General owns assignment and recall.
- It keeps reserve authority out of the Commander layer.

### Commander To Unit

Use only:

- `AdvanceToCell`
- `DefendCell`

Why this is enough:

- `AdvanceToCell` moves assigned units toward the Commander objective.
- `DefendCell` keeps assigned units near a region, city, or Commander.
- Reserve recall is intentionally not a Commander-to-unit command.

### Unit To Commander Reports

Use only:

- `EnemySighted`
- `UnderAttack`
- `LowMorale`
- `ReachedTarget`

Why this is enough:

- The Commander can know local threat, stress, and command progress.
- The report set stays tiny and easy to test.

### Commander To General Reports

Use only:

- `RegionStable`
- `RegionContested`
- `NeedReinforcements`
- `CommanderThreatened`
- `ReserveRecallRequested`

Why this is enough:

- The General can decide between holding, attacking, assigning reserve units, and recalling assigned units.
- It proves summarized bottom-up information without exposing every unit globally.

### City Commands

Keep city production behavior as it is for the first command-control slice unless the code change is tiny.

Add explicit city commands immediately after the first slice:

- `ProduceInfantry`
- `ProduceTank`
- `ProduceCommander`

Commander production should use cost `9` and upkeep `0.9`, and new Commanders should start unassigned.

## Minimal Information Set

### Unit Snapshot Fields

Add or expose:

- `UnitId`
- `PlayerId`
- `Kind`
- `Cell`
- `HealthRatio`
- `Morale`
- `AssignedCommanderUnitId`
- `AssignedCommanderNumber`
- `IsReserve`
- `ActiveCommandType`
- `VisibleEnemyUnitIds`

### Commander Snapshot Fields

Add or expose:

- `CommanderUnitId`
- `CommanderNumber`
- `Cell`
- `HealthRatio`
- `AssignedRegionIds`
- `AssignedUnitCount`
- `ReserveUnitsRequested`
- `ActiveCommandType`
- `LatestReportType`

### Region Snapshot Fields

Keep regions rectangular:

- `RegionId`
- `BoundsX`
- `BoundsY`
- `Width`
- `Height`
- `AssignedCommanderUnitId`
- `AssignedCommanderNumber`
- `FriendlyUnitCount`
- `VisibleEnemyUnitCount`
- `OwnedCellCount`
- `Contested`

### Vision Snapshot Fields

Add or expose:

- `PlayerId`
- `VisibleCellCount`
- `VisibleEnemyUnitIds`
- `EnemyGeneralVisible`

Do not expose hidden enemy details in human mode.

## Vision First

Use the current simple model as the first official vision rule:

- Infantry vision: `5` cells.
- Tank vision: `6` cells.
- Commander vision: `8` cells.
- General vision: `10` cells.
- Scout bonus: `+2` cells.
- Distance metric: Chebyshev.
- Terrain does not block vision yet.
- Player vision is the union of all living friendly unit vision.

First implementation work:

- Centralize or document the vision radius calculation.
- Add focused tests for each radius.
- Add a test that enemy units outside vision are absent from player-facing observations.
- Add a test that enemy units inside vision appear in `VisibleEnemyUnitIds`.
- Keep AI-only diagnostics allowed to show full debug data.

## Proposed Minor Steps

### Step 1: Number Existing Commanders

- [X] Give each player's existing Commander number `1`.
- [X] Add `CommanderNumber` to snapshots and diagnostics.
- [X] Render the number in the Commander circle.
- [X] Add tests for stable numbering and no renumbering after death.

This preserves current gameplay while creating the visual language.

### Step 2: Add Assignment And Reserve State

- [X] Add `AssignedCommanderUnitId` and `AssignedCommanderNumber` to infantry and tanks.
- [X] Treat `null` assignment as reserve.
- [X] Mark reserve units with a reserve pip.
- [X] Assign starting units to Commander `1` for compatibility.
- [X] Make newly produced units start in reserve.
- [X] Add a General reserve assignment command for moving reserve units to Commander `1`.

This creates reserve without needing multiple Commanders yet.

### Step 3: Add First Command Records

- [x] Add capped deterministic histories for the minimal command and report records.
- [x] Include active commands in snapshots.
- [x] Start with history caps small enough for diagnostics, such as `32` commands and `32` reports per player.
- [x] Sort by tick, player, source unit, target unit, then command/report type.

This makes command state inspectable before behavior changes much.

### Step 4: Emit Commands From Current Behavior

- [x] Convert the current human/AI directive into `AttackRegion` or `HoldRegion`.
- [x] Convert the current Commander movement decision into `AdvanceToCell` or `DefendCell`.
- [x] Convert reserve assignment into `AssignReserveUnit`.
- [x] Convert General recall into `RecallCommanderUnitToReserve`.
- [x] Keep existing movement/combat as the behavior backend.

This keeps the game playable while making intent explicit.

### Step 5: Gate Reports Through Vision

- [x] Units only report enemies they can see.
- [x] Commanders only summarize visible enemy pressure plus assigned-unit reports.
- [x] Generals receive Commander summaries rather than direct hidden enemy details.
- [x] Human-vs-AI observations stay filtered.

This is the first real information model.

### Step 6: Add Two-Commander Start

- [x] Add a second starting Commander per player after numbering and assignment are stable.
- [x] Commander `1` and Commander `2` start with deterministic nearby positions.
- [x] Split starting units deterministically between them.
- [x] Let the General assign rectangular regions to either Commander.

If this is too large for the first PR, keep Step 6 as the second PR. The final 40a target is still two Commanders per player.

### Step 7: Add Rectangular Region Assignment

- [x] Create deterministic rectangular region records.
- [x] Let the General assign zero or more regions to each Commander.
- [x] For the first pass, assignment can be simple and deterministic.
- [x] Commander reports summarize only assigned regions.

This proves multi-Commander hierarchy without needing rich tactics.

### Step 8: Add Minimal Selected Details

- [x] Selected unit shows assignment, reserve state, active command, and latest report.
- [x] Selected Commander shows number, assigned regions, assigned unit count, active command, and latest report.
- [x] Selected General shows Commander commands and reserve assignments.

Do not add full-map command lines in this slice.

## First Tests

Core tests:

- Commander numbers start at `1` per player.
- Commander numbers remain stable after Commander death.
- Unassigned units are reserve.
- General can assign a reserve unit to a Commander.
- General can recall an assigned unit to reserve.
- Commander cannot recall units to reserve directly.
- Vision radii match infantry `5`, tank `6`, Commander `8`, General `10`, scout bonus `+2`.
- Hidden enemies are not included in human/player-facing observations.
- Visible enemies produce `EnemySighted` reports.
- Commander summaries include `RegionContested` only from visible enemies and unit reports.

Brinell/UI tests:

- Commander circle shows number.
- Assigned unit circle shows Commander number.
- Reserve unit shows reserve pip.
- Selected unit or Commander exposes active command details.
- Human mode does not show hidden enemy details.

## Done Criteria

- A player can see which Commander owns each assigned unit.
- Reserve units are visually distinct.
- The General can assign reserve units.
- The General can recall assigned units to reserve.
- A Commander can request recall, but cannot perform recall.
- At least one top-down command and one bottom-up report are visible in snapshots.
- Vision filters enemy information in player-facing observations.
- The system remains deterministic for the same seed and command inputs.

## Next After 40a

- Add explicit city commands for infantry, tank, and Commander production in [40b-city-commands-commander-production-steps.md](40b-city-commands-commander-production-steps.md).
- Make simple attack orders flow cleanly down General to Commander to unit in [40c-simple-attack-orders-ai-rework-steps.md](40c-simple-attack-orders-ai-rework-steps.md).
- Add richer commands from the full catalog in roadmap `40`.
- Add command pips/icons for the larger command set.
- Feed command/report records into NEAT observations after deterministic behavior is stable.
