# Wair Of Dots Agent Notes

These notes are for future coding agents working in this repo. Keep them current when project direction changes.

## Project Shape

- Wair of Dots is a single-human, multi-AI tactical game.
- The main mode is one human player against multiple AI players.
- AI players should have different NEAT-style controller identities and behavior.
- AI-only mode is important for debugging, training smoke tests, and watching emergent behavior.
- Oravey may be used only as a Stride and Brinell technical reference. Do not copy Oravey game mechanics, control overlays, or gameplay rules.

## Documentation Rules

- Put future project docs under `.my`.
- Roadmaps go in `.my/roadmap`.
- Implemented roadmaps can be moved to `.my/roadmap/archive`.
- RCA docs go in `.my/rca`.
- Player-facing docs belong in `.my`, such as `.my/HowToPlay.md` and `.my/UAT.md`.
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
- AI-only spectator mode exists.
- Basic Human General and Human Commander mode controls exist.
- General, Commander, and Unit controller interfaces exist.
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

- Finish morale/rout behavior with explicit forced retreat, rally, and commander-under-attack penalties.
- Make commander regions more real: fronts, protection details, and stronger commander-death effects.
- Decide whether true SharpNEAT is needed now or whether deterministic NEAT-style AI remains the local MVP.
- Add scout role/fog UI only after the desired player information model is clear.
