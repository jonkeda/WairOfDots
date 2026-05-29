# Wair Of Dots Agent Notes

These notes are for future coding agents working in this repo. Keep them current when project direction changes.

## Project Shape

- Wair of Dots is a single-human, multi-AI tactical game.
- The main mode is one human player against multiple AI players.
- AI players should have different NEAT-style controller identities and behavior.
- Command and information flow should be strict hierarchy: General to Commanders, Commanders to assigned units, and reports upward. Do not add sideways command flow.
- AI-only mode is important for debugging, training smoke tests, and watching emergent behavior.
- Oravey may be used only as a Stride and Brinell technical reference. Do not copy Oravey game mechanics, control overlays, or gameplay rules.

## Documentation Rules

- Put future project docs under `.my`.
- Roadmaps go in `.my/roadmap`.
- Implemented roadmaps can be moved to `.my/roadmap/archive`.
- RCA docs go in `.my/rca`.
- Player-facing docs belong in `.my`, such as `.my/HowToPlay.md`, `.my/UAT.md`, and `.my/UnitProperties.md`.
- `AGENTS.md` is the root exception because coding agents look for it here.

## Current Implemented Baseline

- C# core simulation and Stride Windows host exist.
- The game is grid-based with passable terrain, terrain costs, eight-way movement, smooth visual movement, and single-cell occupancy.
- Units, Commanders, and Generals are all physical map units.
- Combat is adjacent; unit health and morale exist.
- General death eliminates that player and neutralizes that player's cities and controlled cells.
- Cities can be owned, captured, colored by owner, and used as spawn anchors.
- Territory cells have ownership and tax values.
- Infantry captures only its current cell.
- Tanks capture their current cell and adjacent passable cells.
- Players collect tax income, pay upkeep, suffer payroll deficits, and lose morale when unpaid.
- Production is treasury-funded and spawns units near owned cities.
- Production is driven by explicit General-to-city commands: `ProduceInfantry`, `ProduceTank`, `ProduceCommander`, and `HoldProduction`.
- City production commands are emitted as command records in the `GeneralToCity` layer with city ID, unit kind, affordability, and reason codes.
- Commanders can be produced beside infantry and tanks; Commander cost is `9`, Commander upkeep is `0.9`.
- Produced Commanders receive the next stable player-local Commander number, start with zero regions and zero assigned units.
- A later General planning tick assigns rectangular regions to new Commanders.
- Desired Commander count is `clamp(owned city count, 2, 4)` as a production-policy target.
- Region assignments are explicit per-player state; starting Commanders get deterministic rectangles, new Commanders start unassigned.
- AI-only spectator mode exists.
- Basic Human General and Human Commander mode controls exist.
- General, Commander, and Unit controller interfaces exist.
- Each player starts with two Commanders numbered `1` and `2`; Commander numbers are exposed in snapshots/diagnostics and rendered in Commander circles.
- Starting infantry and tanks are split deterministically between the two starting Commanders; newly produced infantry and tanks start in reserve until the General assigns them.
- Capped command and report histories are exposed in match snapshots; current planning emits `AttackRegion`/`HoldRegion`, `AdvanceToCell`/`DefendCell`, General reserve commands emit `AssignReserveUnit`, `AssignReserveGroup`, `RecallCommanderUnitToReserve`, and `RecallCommanderGroupToReserve`, and commander pressure emits `CommanderThreatened`.
- Commanders report `NeedReinforcements` and `ReserveRecallRequested`; only the General executes reserve assignment or recall, including group operations.
- Simple attack AI is behavior-backed: Generals choose per-Commander objectives, Commanders translate active General commands into orders only for assigned units, reserve units receive no Commander orders, and low morale can override attack orders with retreat/defense.
- Rectangular Commander regions, visible enemy IDs, active command types, and latest report types are exposed in snapshots; Commander summaries are based on visible enemy pressure.
- Brinell automation exposes compact command-chain diagnostics grouped by player, Commander, assigned units, active commands, reserve commands, and recent reports.
- Current AI uses deterministic NEAT-style genome IDs and adapters, not real SharpNEAT evolution.
- Region, economy, visibility, fitness, telemetry, and cell-control snapshots exist.
- Territory boundary data exists and is rendered visually.

## Rendering Rules

- Do not draw the playfield with Stride UI controls.
- Stride UI is for menus, HUD, buttons, text, standings, and Brinell-accessible click targets.
- The tactical map visuals are rendered through `MapSpriteRenderer`.
- `MapCanvas` should remain stable and only hold interaction hit targets unless there is a strong reason to change that.
- Draw map layers in deterministic order: terrain, territory boundaries, cities, units, selection/highlights, then lightweight labels if needed.
- If the map flickers, disappears, or draws over itself, suspect UI rebuilds or renderer ordering first.

## Testing Rules

- Use core unit tests for simulation rules.
- Use Brinell UI tests for player-visible UI behavior.
- Use Brinell screenshot tests for visual map rendering, boundaries, visibility, and regressions that cannot be proven from UI element names.
- For UI/map changes, run at least the focused Brinell smoke test that covers the changed surface.
- For gameplay-rule changes, run `dotnet test tests/WairOfDots.Tests/WairOfDots.Tests.csproj`.
- For broad UI changes, run `dotnet test tests/WairOfDots.UITests/WairOfDots.UITests.csproj`.
- Normal test runs should not require long-running training generations; use deterministic smoke runs.

## Prompt Roadmap Status

Use `.my/roadmap/11-prompt-gap-roadmap.md` as the prompt coverage tracker.

Implemented or mostly implemented:

- Phase 01 controller interfaces.
- Phase 02 cell territory tax economy.
- Player land boundaries and stable SpriteBatch map rendering.

Partially implemented:

- Phase 03 morale and rout.
- Phase 04 commander regions.
- Phase 05 resource budgets and replenishment.
- Phase 06 general strategy layer.
- Phase 07 visibility and scouting.
- Phase 08 training smoke and fitness.
- Phase 09 alternate human modes.
- Phase 10 emergent behavior instrumentation.

Still missing or intentionally deferred:

- Richer Commander-to-General flow beyond the first visible-pressure, reinforcement, and reserve-recall summaries.
- Command icons/pips beyond Commander numbers, assigned-unit numbers, and reserve pips.
- Actual SharpNEAT package integration and evolutionary training.
- Dedicated headless training runner project.
- Saved genomes, model serialization, and ONNX export.
- Full four-tier General -> Commander -> Unit Nets -> Dots neural hierarchy.
- Dedicated scout role and complete fog-of-war/player-facing intelligence UI.
- Commander protection detail assignment.
- Full rout/rally behavior and stronger morale tests.
- Human General plus one Commander mode.
- Human Dot/chaos mode.
- LLM controller adapter behind the controller interfaces.

## Good Next Slices

- Continue from `.my/roadmap/40b-city-commands-commander-production-steps.md` or the next command-and-control slice before expanding the full `.my/roadmap/40-command-information-neat-roadmap.md` catalog.
- Finish morale/rout behavior with explicit forced retreat, rally, and commander-under-attack penalties.
- Make commander regions more real: fronts, protection details, and stronger commander-death effects.
- Keep deterministic command-and-control as the local MVP path; revisit true SharpNEAT after hierarchy, assignments, and command diagnostics are stable.
- Add scout role/fog UI only after the desired player information model is clear.
