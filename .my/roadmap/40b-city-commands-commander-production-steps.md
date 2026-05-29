# City Commands And Commander Production Steps

Status: implemented.

Parent roadmap: [40-command-information-neat-roadmap.md](40-command-information-neat-roadmap.md)

Previous slice: [40a-first-command-control-vision-steps.md](40a-first-command-control-vision-steps.md)

Behavior-first alternative: [40c-simple-attack-orders-ai-rework-steps.md](40c-simple-attack-orders-ai-rework-steps.md)

Unit reference: [.my/UnitProperties.md](../UnitProperties.md)

## Goal

Make production part of the same explicit command hierarchy created in `40a`.

The next smallest useful slice is:

- The General issues structured city production commands.
- Cities execute those commands instead of production being implicit.
- Infantry and tanks still start in reserve.
- Commanders can be produced beside infantry and tanks.
- New Commanders receive the next stable player-local number.
- New Commanders start with zero regions and zero assigned subordinate units.

This keeps command-and-control deterministic before expanding into richer tactical commands or NEAT action outputs.

If the next implementation priority is to make armies obey simple down-line attack orders, implement `40c` before this production slice.

## Baseline From 40a

Keep these rules unchanged:

- Communication stays vertical only.
- The General owns reserve assignment and recall.
- Commanders cannot recall units to reserve.
- Units have one active executable command.
- Commanders have one active executable directive.
- Units without an assigned Commander are reserve.
- Vision gates enemy information.
- Normal map mode stays readable; no default command lines.

## 40b Scope

Add the smallest production command set:

- `ProduceInfantry`
- `ProduceTank`
- `ProduceCommander`
- `HoldProduction`

Use `HoldProduction` when the city cannot or should not produce this tick because of treasury, spawn blocking, or a deliberate reserve-treasury decision.

Do not add the full city command catalog yet:

- `ReserveTreasury`
- `ReinforceCommander`
- `DefendCity`
- `RecoverPayroll`

Those belong after basic city command execution is stable.

## First-Slice Production Rules

- There is at most one active production command per city.
- Production commands are General-to-city commands.
- Cities do not choose their own production.
- Infantry cost and tank cost stay as currently implemented.
- Commander cost is `9`.
- Commander upkeep is `0.9`.
- Produced infantry and tanks start in reserve.
- Produced Commanders are not reserve units, but they start without regions and without subordinate units.
- A produced Commander must not steal units or regions automatically on the spawn tick.
- The current primary Commander remains unchanged unless the player has no living Commander.
- New Commanders receive the next unused player-local Commander number. Living Commanders are never renumbered.

## Commander Production Trigger

Use a simple deterministic rule first.

Proposed first rule:

- Desired Commander count is `clamp(owned city count, 2, 4)`.
- This is a production-policy target, not a hard design limit.
- If living Commander count is below the desired count and the player can afford a Commander, issue `ProduceCommander`.
- Otherwise use the existing infantry/tank production preference.
- If no valid spawn cell exists, issue `HoldProduction` with reason `SpawnBlocked`.
- If treasury cannot afford the selected unit, issue `HoldProduction` with reason `InsufficientTreasury`.

This gives Commander production a visible path without needing a full strategic planner.

## Region Assignment Cleanup

`40a` creates deterministic rectangular region records for the current Commanders. `40b` should make assignment explicit enough that new Commanders can have zero regions.

Rules:

- Region records stay rectangular.
- Region assignment should be stored or derived from explicit General-to-Commander command state, not purely from every living Commander.
- Starting Commanders may receive the current deterministic rectangles at match start.
- Newly produced Commanders start with zero assigned regions.
- A later General planning tick may assign a region through the existing `AttackRegion` or `HoldRegion` command payload.
- A Commander with zero regions can still exist, report, and receive future assignments.

Do not add sideways region handoff between Commanders.

## Proposed Minor Steps

### Step 1: Make Region Assignment State Explicit

- [ ] Introduce explicit per-player region assignment state.
- [ ] Preserve the current deterministic starting rectangles for the initial Commanders.
- [ ] Allow a living Commander to have zero regions.
- [ ] Keep region snapshots deterministic and sorted.

Testability:

- Starting Commanders still have the expected rectangular regions.
- A Commander can exist with zero `AssignedRegionIds`.
- Region snapshots remain stable for the same seed.

### Step 2: Emit General-To-City Command Records

- [ ] Add `GeneralToCity` command records.
- [ ] Emit `ProduceInfantry`, `ProduceTank`, or `HoldProduction` from current production behavior.
- [ ] Include `CityId`, `UnitKind`, `Budget`, `Priority`, `ExpiresOnTick`, and `ReasonCode` where the existing record model allows it.
- [ ] Keep current production behavior unchanged in this step.

Testability:

- Production ticks create deterministic city command records.
- City command records are capped and sorted with the other command histories.
- `HoldProduction` appears when a city is blocked or unaffordable.

### Step 3: Make Infantry And Tank Production Consume City Commands

- [ ] Move existing infantry/tank spawning behind the city command executor.
- [ ] Keep existing costs, spawn rules, reserve behavior, and treasury spending.
- [ ] Preserve current AI production preferences unless a city command says otherwise.
- [ ] Add a clear blocked-spawn path.

Testability:

- Existing production tests still pass.
- A `ProduceInfantry` command spends infantry cost and creates reserve infantry.
- A `ProduceTank` command spends tank cost and creates a reserve tank.
- `HoldProduction` creates no unit and spends no treasury.
- Blocked cities do not spend treasury.

### Step 4: Add ProduceCommander

- [ ] Add `ProduceCommander` as a General-to-city command.
- [ ] Charge cost `9`.
- [ ] Add Commander upkeep `0.9`.
- [ ] Spawn the Commander near the city using the same deterministic spawn search style as other units.
- [ ] Assign the next player-local Commander number.
- [ ] Start with zero regions and zero assigned subordinate units.

Testability:

- Commander production spends exactly `9`.
- Commander upkeep contributes exactly `0.9`.
- New Commander number is the next unused number for that player.
- Existing Commander numbers do not change.
- New Commander has no assigned regions.
- New Commander has no assigned units.
- Spawn blocking prevents production without spending treasury.

### Step 5: Let General Planning Assign New Commander Regions

- [ ] Keep produced Commanders unassigned on their spawn tick.
- [ ] On a later planning tick, allow the General to assign zero or more rectangular regions.
- [ ] Use the existing `AttackRegion` or `HoldRegion` command payload instead of adding a new sideways mechanism.
- [ ] Make the selected Commander details show the changed region assignment.

Testability:

- A newly produced Commander has zero regions immediately after spawn.
- After a General assignment command, the Commander snapshot includes the assigned region id.
- The assignment is deterministic for the same seed and game state.
- No Commander-to-Commander or unit-to-unit command record appears.

### Step 6: Expose Selected City Production Details

- [ ] Selected city details show active production command.
- [ ] Selected city details show planned unit kind, affordability, and spawn-blocked state.
- [ ] Map visual diagnostics expose active city command data for Brinell.
- [ ] Keep the tactical map itself free of permanent production text.

Testability:

- Brinell can select or query a city and read its active production command.
- Economy overlay remains readable.
- Human-vs-AI mode does not expose hidden enemy information through city production details.

### Step 7: Update Docs And Acceptance Tests

- [ ] Update `.my/UnitProperties.md` with Commander cost `9` and upkeep `0.9`.
- [ ] Update `.my/HowToPlay.md` if Commander production becomes player-visible.
- [ ] Update `AGENTS.md` implemented baseline after the slice is complete.
- [ ] Add focused core and Brinell tests for the new production path.

Testability:

- Docs match implemented Commander cost and upkeep.
- Core tests prove the production command chain.
- UI tests prove selected city diagnostics without relying on map UI controls.

## First Tests

Core tests:

- General-to-city production records are emitted deterministically.
- City command history is capped and sorted.
- `ProduceInfantry` creates reserve infantry.
- `ProduceTank` creates reserve tank.
- `ProduceCommander` creates a numbered Commander.
- Commander production cost is `9`.
- Commander upkeep is `0.9`.
- Produced Commanders start with zero regions.
- Produced Commanders start with zero assigned units.
- Produced Commanders do not renumber living Commanders.
- Blocked city production emits `HoldProduction` and spends nothing.
- Unaffordable production emits `HoldProduction` and spends nothing.

Brinell/UI tests:

- Selected city exposes active production command.
- Selected produced Commander shows its number and zero assigned regions.
- Reserve units produced by city commands still show reserve pips.
- Economy overlay still exposes spawn capacity and payroll stress.

## Done Criteria

- Production is visible as General-to-city command data.
- Infantry and tank production execute through explicit city commands.
- Commander production exists beside infantry and tank production.
- Commander cost is `9`.
- Commander upkeep is `0.9`.
- Produced Commanders receive stable player-local numbers.
- Produced Commanders start with zero regions and zero assigned units.
- A later General command can assign a region to a produced Commander.
- Human-vs-AI still does not reveal hidden enemy information.
- AI-only diagnostics can inspect the production command flow.
- Core and Brinell tests cover the slice.

## Not In 40b

- Real SharpNEAT package integration.
- Multiple active command queues.
- Full city command catalog.
- Advanced command icons for every command type.
- Default command lines across the map.
- Terrain-blocking vision.
- Sideways Commander coordination.
- Commander-controlled reserve recall.

## Next After 40b

- Add command pips/icons for the expanded command set.
- Add selected-object command links for General and Commander debug views.
- Add richer General-to-Commander commands such as `DefendRegion`, `TakeCity`, `ProtectGeneral`, and `ScoutFront`.
- Feed the explicit city, reserve, region, and unit command records into deterministic NEAT-style observations.
