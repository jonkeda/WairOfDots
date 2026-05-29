# Simple Attack Orders And AI Rework Steps

Status: implemented.

Parent roadmap: [40-command-information-neat-roadmap.md](40-command-information-neat-roadmap.md)

Previous command slice: [40a-first-command-control-vision-steps.md](40a-first-command-control-vision-steps.md)

Related production slice: [40b-city-commands-commander-production-steps.md](40b-city-commands-commander-production-steps.md)

## Sequencing Note

This slice can be implemented before `40b`.

`40a` made the hierarchy visible. The next most useful behavior step may be to prove that simple attack intent travels down the line before adding more production commands. `40b` remains valuable, but `40c` focuses on making the current army obey the command hierarchy in a simple, deterministic way.

## Goal

Redo the first-pass AI for Generals, Commanders, and units around one clear command chain:

```text
General chooses attack objective
General commands Commander: AttackRegion
Commander commands assigned units: AdvanceToCell
Units move, capture, fight, and report upward
Commander summarizes progress to General
```

The goal is not smarter AI yet. The goal is AI that is easier to reason about, test, and later replace with NEAT outputs.

## Problem

The current behavior still has legacy decision flow mixed into each layer. Units can feel like they are pursuing tactical targets directly, while Commanders and Generals mostly expose intent after the fact.

That makes command-and-control harder to trust:

- It is not always obvious that a unit moved because its Commander ordered it.
- It is not always obvious that a Commander acted because the General gave a directive.
- Reports exist, but they do not yet clearly drive the next simple attack decision.
- The AI layers are hard to replace with NEAT because responsibilities are still blended.

## Design Principle

Keep the first attack AI almost plain:

- The General picks objectives.
- Commanders translate objectives into unit targets.
- Units obey their active command unless local survival overrides it.
- Reports are the only bottom-up information path.
- No sideways coordination.
- No multiple active executable commands.
- No real SharpNEAT yet.

## Minimal Attack Vocabulary

Use the `40a` vocabulary first:

General to Commander:

- `AttackRegion`
- `HoldRegion`

Commander to Unit:

- `AdvanceToCell`
- `DefendCell`

Unit to Commander reports:

- `EnemySighted`
- `UnderAttack`
- `LowMorale`
- `ReachedTarget`

Commander to General reports:

- `RegionStable`
- `RegionContested`
- `NeedReinforcements`
- `CommanderThreatened`

Do not add `TakeCity`, `AttackTarget`, or `ScoutArea` yet unless a tiny adapter is needed for diagnostics. The point of this slice is behavior clarity, not command catalog expansion.

## First Attack Rules

### General AI

The General should:

- Pick one simple attack objective per Commander.
- Prefer visible or reported contested regions.
- Otherwise prefer the nearest enemy or neutral city.
- Otherwise hold assigned regions.
- Issue one active `AttackRegion` or `HoldRegion` command per Commander.
- Include a target cell and optional target city in the command payload when available.
- Avoid direct hidden enemy knowledge in human-vs-AI mode.

### Commander AI

Each Commander should:

- Read only its active General command, assigned regions, assigned units, and reports.
- Ignore units assigned to another Commander.
- Ignore reserve units except when requesting reinforcements.
- Convert `AttackRegion` into `AdvanceToCell` commands for assigned units.
- Convert `HoldRegion` into `DefendCell` commands for assigned units.
- Keep units roughly aimed at the same objective instead of each unit choosing a separate strategic goal.
- Report `NeedReinforcements` when assigned strength is low or morale is poor.

### Unit AI

Each assigned unit should:

- Treat its active Commander command as the primary behavior input.
- Move toward `AdvanceToCell` targets.
- Stay near `DefendCell` targets.
- Attack adjacent enemies normally.
- Capture territory normally.
- Override the command only for rout, low morale retreat, blocked path recovery, or immediate survival.
- Report local events upward to its assigned Commander.

Reserve units should:

- Receive no Commander-to-unit commands.
- Hold position unless assigned by the General.
- Continue showing reserve visuals.

## Proposed Minor Steps

### Step 1: Define The Down-Line Attack Contract

- [x] Document the exact payload fields needed for `AttackRegion`, `HoldRegion`, `AdvanceToCell`, and `DefendCell`.
- [x] Make command target cell/city fields reliable in snapshots.
- [x] Add a focused diagnostic helper for active command lookup per Commander and unit.
- [x] Keep command history capped and deterministic.

Testability:

- A Commander has exactly one active General command.
- An assigned unit has exactly one active Commander command.
- Reserve units have no active Commander command.
- Active command target fields are present when the command needs a target.

### Step 2: Replace General AI With A Simple Objective Selector

- [x] Add a deterministic General objective selector.
- [x] Choose attack objectives from visible/reported regions first.
- [x] Fall back to nearest neutral or enemy city.
- [x] Emit `AttackRegion` for attack objectives.
- [x] Emit `HoldRegion` when there is no useful attack objective.
- [x] Keep existing genome id and telemetry identity stable.

Testability:

- Same seed and same reports produce the same General commands.
- General commands differ when reports change from stable to contested.
- The General does not issue commands to eliminated or dead Commanders.
- Human-vs-AI General observations do not include hidden enemy units.

### Step 3: Replace Commander AI With A Simple Order Translator

- [x] Commander reads the General command assigned to itself.
- [x] Commander assigns `AdvanceToCell` targets to only its assigned units.
- [x] Commander assigns `DefendCell` targets when ordered to hold.
- [x] Commander requests reinforcements through `NeedReinforcements`, not by taking reserve units.
- [x] Commander with zero assigned units still reports its region state.

Testability:

- A Commander never commands units assigned to another Commander.
- A Commander never commands reserve units.
- `AttackRegion` produces `AdvanceToCell` for assigned units.
- `HoldRegion` produces `DefendCell` for assigned units.
- Low assigned strength creates `NeedReinforcements`.

### Step 4: Replace Unit AI With Command-Following Movement

- [x] Unit behavior starts from its active Commander command.
- [x] `AdvanceToCell` moves the unit toward the command target.
- [x] `DefendCell` keeps the unit near the command target.
- [x] Adjacent combat and territory capture remain unchanged.
- [x] Routed or low-morale units may override movement with retreat/rally behavior.
- [x] Units emit `ReachedTarget` when they arrive.

Testability:

- An assigned unit advances toward the Commander target.
- An assigned unit does not pick an unrelated strategic city.
- A reserve unit does not move because of Commander orders.
- A routed unit can override an attack command.
- A unit emits `ReachedTarget` after reaching its command target.

### Step 5: Make Reports Affect The Next Attack Plan

- [x] Feed Commander reports into the next General objective choice.
- [x] Prefer `RegionContested` over quiet expansion.
- [x] Treat repeated `NeedReinforcements` as a reason to hold or request reserve assignment.
- [x] Treat `CommanderThreatened` as a reason to defend or protect that Commander.
- [x] Keep this deterministic and small.

Testability:

- A contested report changes the next General command target.
- A threatened Commander receives hold/protection behavior instead of blind attack.
- Repeated reinforcement requests do not let Commanders recall units directly.

### Step 6: Put Human Commands Through The Same Chain

- [x] Human `Attack`, `Hold`, and `Defend` should become the same General-to-Commander command records as AI commands.
- [x] Human Commander mode should still use Commander-to-unit commands.
- [x] Human Dot/chaos mode should remain a later feature if it complicates the chain.
- [x] Selected details should show that human orders use the same command path.

Testability:

- Human `Attack` emits `AttackRegion`.
- Human `Hold` emits `HoldRegion`.
- Human Commander mode emits `AdvanceToCell` or `DefendCell` for assigned units.
- No hidden enemy details are exposed in human mode.

### Step 7: Add Down-Line Command Diagnostics

- [x] Add a compact command-flow diagnostic view or query.
- [x] Group by player, General command, Commander command, assigned units, and latest reports.
- [x] Keep normal map mode clean.
- [x] Use selected-object HUD details before adding any map lines.

Testability:

- Brinell can query one player's command chain.
- Selecting a General shows Commander attack orders.
- Selecting a Commander shows assigned unit orders.
- Selecting a unit shows the active command target and latest report.

## First Tests

Core tests:

- General emits one active command per living Commander.
- Commander emits commands only to assigned units.
- Reserve units do not receive Commander commands.
- Attack orders move assigned units toward the commanded target.
- Hold orders keep assigned units near the defended target.
- Unit reports flow to the assigned Commander.
- Commander summaries flow to the General.
- Reports can change the next General command deterministically.
- Same seed and same inputs produce the same command chain.

Brinell/UI tests:

- Selected General shows down-line attack orders.
- Selected Commander shows assigned unit orders.
- Selected unit shows active command target.
- Command diagnostics do not expose hidden enemy details in human-vs-AI.
- Normal map mode stays readable without default command lines.

## Done Criteria

- Attack behavior is visibly caused by command records.
- General, Commander, and unit responsibilities are separated.
- Units no longer choose strategic attack objectives independently.
- Commanders command only their assigned units.
- Reserve units stay out of Commander order flow.
- Reports can affect later General commands.
- Human and AI commands use the same hierarchy.
- The system remains deterministic.
- Tests cover the full down-line attack path.

## Not In 40c

- Commander production and city command execution from `40b`.
- Full command catalog expansion.
- Real SharpNEAT package integration.
- Multiple active command queues.
- Default command lines over the whole map.
- Terrain-blocking vision.
- Sideways coordination between Commanders or units.

## Next After 40c

- Implement [40d-reserve-commands-steps.md](40d-reserve-commands-steps.md) so Commander reinforcement and recall reports become General-owned reserve commands.
- Implement `40b` city commands and Commander production if it was deferred.
- Add selected-object command links for debug views.
- Add richer commands such as `TakeCity`, `DefendRegion`, `ProtectGeneral`, and `ScoutFront`.
- Feed the cleaned General, Commander, and Unit policies into NEAT-style observation/action adapters.
