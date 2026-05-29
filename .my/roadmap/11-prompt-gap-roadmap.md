# Prompt Gap Roadmap

Status: updated after implemented phase roadmaps.

Source: `.my/DevPrompts/Prompt.md`

## Goal

Compare the original development prompt against the current Wair of Dots implementation, identify what is already implemented, and define a roadmap for the remaining prompt features.

## Current Coverage Summary

The current game implements a solid playable MVP plus the first architecture/economy/instrumentation slices, but not the full original prompt.

Estimated prompt coverage:

- Core playable game: high coverage.
- UI, Brinell automation, deterministic tests: high coverage.
- Terrain grid, city ownership, territory capture, tax economy, production, combat, commanders/generals as units: high coverage.
- Controller contracts, region/economy/visibility observations, fitness smoke, telemetry: medium coverage.
- Morale/rout, commander regions, resource budgets, hidden General behavior, and alternate human play modes: medium-low coverage.
- Real SharpNEAT training, saved genomes, ONNX export, and full four-tier neural hierarchy: low coverage.

Overall prompt coverage is roughly 60 percent if weighted against the full ambitious prompt, and much higher if weighted only against the current MVP definition.

## Implemented

- [X] C# game and simulation core.
- [X] Stride Windows host.
- [X] Top-down tactical map.
- [X] Terrain grid with grass, water, road, forest, hill, and rock.
- [X] Cities with ownership, production, capture, and owner-colored map markers.
- [X] Infantry/light-like units and tank/heavy-like units.
- [X] Commander and General units as physical map units.
- [X] Unit health.
- [X] Unit morale property.
- [X] Deterministic fixed-tick simulation.
- [X] Eight-way grid movement with terrain costs.
- [X] Smooth visual movement and path smoothing.
- [X] Single-cell occupancy.
- [X] Adjacent combat.
- [X] General death eliminates a player.
- [X] General death neutralizes that player's cities.
- [X] General death neutralizes that player's controlled territory cells.
- [X] Win conditions: last active General, human controls all cities, or timer score.
- [X] Human General-style mode with Attack, Hold, Defend, target city, and infantry/tank preference.
- [X] Basic Human Commander mode toggle.
- [X] AI-only spectator mode.
- [X] Deterministic NEAT-style AI controller archetypes with unique genome IDs.
- [X] General, Commander, and Unit controller contracts.
- [X] General, Commander, and Unit perception/action records.
- [X] Legacy AI controller adapter behind the General controller contract.
- [X] Cell ownership, tax values, tax income, treasury, upkeep, payroll deficit, and morale loss for unpaid units.
- [X] Infantry captures its occupied cell.
- [X] Tanks capture their occupied cell and adjacent passable cells.
- [X] Treasury-funded infantry/tank purchases near owned cities.
- [X] Region snapshots, visibility snapshots, economy snapshots, and cell-control diagnostics.
- [X] Visibility radii for units, commanders, tanks, and generals.
- [X] Fitness evaluator and deterministic training smoke runner.
- [X] Territory boundary data and SpriteBatch map rendering.
- [X] AI telemetry and standings.
- [X] Unit tests for deterministic simulation behavior.
- [X] Brinell UI tests for launch, HUD, controls, map, movement, and AI-only behavior.
- [X] Brinell screenshot smoke test for territory boundaries.
- [X] Player docs and UAT docs in `.my`.

## Partially Implemented

- [~] `Light` and `Heavy` units exist as infantry and tanks, but Heavy is not limited to open terrain only.
- [~] City capacity exists in `CityNode`, but attrition for over-capacity cities is not implemented because the game now uses single-unit grid occupancy.
- [~] Morale now has thresholds, payroll pressure, combat deltas, nearby death effects, commander aura, outnumbered pressure, and retreat intent, but full rally/route behavior still needs stronger rules and tests.
- [~] Commanders and Generals are physical units with region/leaderless effects, but Commander protection detail and richer hidden/rear General behavior are not complete.
- [~] AI has deterministic General/Commander/Unit controller contracts and genome-like IDs, but it is not real SharpNEAT evolution or a full multi-tier neural system.
- [~] AI observations include city ownership, strength, resources, economy, regions, visibility, commander health, general health, and time pressure, but not the final prompt-level dot/commander/general perception contracts.
- [~] Human play supports General mode and a basic Commander mode, but not region budget UI, commander reserve management, General relocation, or Dot/chaos mode.
- [~] Visibility radii and scout-discovery telemetry exist, but there is no dedicated scout unit role or full player-facing fog UI.
- [~] Training has deterministic smoke and fitness scoring, but no actual SharpNEAT package, long-running trainer, saved genomes, or ONNX path.
- [~] Match length is configurable in ticks, but no explicit 5-15 minute scenario preset UI exists.

## Not Implemented

- [ ] Actual SharpNEAT dependency and evolutionary training.
- [ ] Dedicated headless training runner project.
- [ ] Bottom-up training phases for Dot, Commander, and General nets.
- [ ] Co-evolution/self-play training phase.
- [ ] Separate LightUnitNet and HeavyUnitNet.
- [ ] Commander net and General net.
- [ ] Four-tier General -> Commander -> Unit Nets -> Dots neural hierarchy.
- [~] Strict vertical command/report hierarchy between tiers, with no sideways command flow.
- [ ] Full upward/downward information flow between tiers.
- [x] Multiple per-player Commanders with stable visible Commander numbers.
- [x] Reserve unit and group assignment and reserve recall by the General.
- [x] Rectangular regions with zero or more regions assigned per Commander.
- [ ] Commander production beside infantry and tank production.
- [ ] Compact command visualization through Commander numbers, reserve pips, and command icons/pips.
- [ ] Commander protection detail size and assignment.
- [ ] Dedicated scout unit role.
- [ ] Scout roles.
- [ ] Full rout and rally behavior.
- [ ] Casualty-rate tracking.
- [ ] Saved genomes and model serialization.
- [ ] Human General + one Commander mode.
- [ ] Human Dot/chaos mode.
- [ ] LLM controller adapter behind the controller interfaces.
- [ ] ONNX export.

## Roadmap Strategy

Do not try to implement the rest of the prompt as one large change. Build it in compatibility-preserving slices around the current playable MVP.

Keep these constraints:

- [X] Preserve one human, multiple AI players as the main mode.
- [X] Keep AI-only mode as the main training/debug spectator mode.
- [X] Keep Brinell coverage for UI behavior.
- [X] Keep pure core simulation tests for game rules.
- [X] Keep `.my` as the home for future docs and roadmaps.

Current command-and-control direction:

- Build deterministic command-and-control before real NEAT integration.
- Use strict hierarchy: General to Commanders, Commanders to assigned units, reports upward only.
- Give each Commander a stable player-local number starting at `1`.
- Show Commander numbers in Commander circles and assigned-unit circles.
- Treat unassigned units as reserve.
- Let the General assign reserve units to Commanders.
- Let the General recall assigned units back to reserve.
- Let Commanders request recall through reports, but not execute recall directly.
- Use rectangular regions, with zero or more regions assigned per Commander.
- Add Commander production beside infantry and tank production.
- Prefer compact unit/Commander icons, center labels, and pips over always-visible command lines.
- Track the detailed plan in [40-command-information-neat-roadmap.md](40-command-information-neat-roadmap.md).

## Completed Foundation: Prompt Contract Baseline

- [x] Add prompt coverage notes to `.my/DevPrompts/Architecture.md`.
  Answer: no need; this overview roadmap is the coverage tracker.
- [x] Add explicit `PromptCoverage` section or doc that tracks implemented/partial/missing prompt features.
  Answer: no need; use this roadmap.
- [x] Decide naming alignment: keep `Infantry/Tank` in code or rename user-facing units to `Light/Heavy`.
  Answer: keep `Infantry` and `Tank`; new unit types can be added later.
- [x] Decide whether city capacity/attrition should coexist with single-cell occupancy or be replaced by regional/adjacent pressure.
  Answer: keep the current single-cell occupancy model. Do not reintroduce stacked city capacity/attrition now.
- [x] Decide whether "city nodes connected by paths" remains historical prompt text or becomes visible road/region topology on top of the grid.
  Answer: not needed anymore; grid movement is the source of truth.

## Implementation Phase Roadmaps

- Completed baseline: [12-prompt-phase-1-contract-decisions.md](archive/12-prompt-phase-1-contract-decisions.md)
- Implemented/archived: [13-phase-01-controller-interfaces.md](archive/13-phase-01-controller-interfaces.md)
- Implemented/archived: [14-phase-02-cell-territory-tax-economy.md](archive/14-phase-02-cell-territory-tax-economy.md)
- Partially implemented/archived: [15-phase-03-morale-rout-system.md](archive/15-phase-03-morale-rout-system.md)
- Partially implemented/archived: [16-phase-04-commander-regions.md](archive/16-phase-04-commander-regions.md)
- Partially implemented/archived: [17-phase-05-resource-budgets.md](archive/17-phase-05-resource-budgets.md)
- Partially implemented/archived: [18-phase-06-general-strategy.md](archive/18-phase-06-general-strategy.md)
- Partially implemented/archived: [19-phase-07-visibility-scouting.md](archive/19-phase-07-visibility-scouting.md)
- Partially implemented/archived: [20-phase-08-sharpneat-training.md](archive/20-phase-08-sharpneat-training.md)
- Partially implemented/archived: [21-phase-09-alternate-human-modes.md](archive/21-phase-09-alternate-human-modes.md)
- Partially implemented/archived: [22-phase-10-emergent-behavior-instrumentation.md](archive/22-phase-10-emergent-behavior-instrumentation.md)

## Phase 01: Controller Interfaces

- [x] Add `IGeneralController`, `ICommanderController`, and `IUnitController` interfaces.
- [x] Move the current `IAiController` into a General-level adapter or rename it to reflect its actual tier.
- [x] Add perception/action records for General, Commander, and Unit layers.
- [x] Add human and deterministic AI adapters behind the same controller contracts.
- [x] Keep current AI behavior equivalent after the interface refactor.
- [x] Unit test deterministic controller decisions through the new interfaces.

## Phase 02: Cell Territory Tax Economy

- [x] Add passable-cell ownership and tax values.
- [x] Capture occupied cells each tick.
- [x] Let tanks capture their current cell and adjacent passable cells.
- [x] Let infantry capture only the cell it occupies.
- [x] Compute tax income from controlled cells.
- [x] Add treasury, upkeep, payroll deficit, and morale loss for unpaid units.
- [x] Adapt production into treasury-funded purchases.
- [x] Spawn purchased units near owned cities.
- [x] Add tests for capture, tax income, upkeep, payroll deficit, and city-adjacent spawning.

## Phase 03: Morale And Rout System

- [x] Normalize morale as an explicit 0-1 gameplay value.
- [x] Implement morale deltas for taking damage and dealing damage.
- [x] Track nearby friendly deaths for morale penalties.
- [x] Add outnumbered morale pressure.
- [x] Add commander-nearby rally/morale bonus.
- [ ] Add commander-under-attack morale penalty.
- [x] Add commander-death and general-death morale collapse events.
- [~] Implement morale thresholds: aggressive, normal, cautious, rout risk, forced retreat.
- [~] Add unit tests for morale thresholds and forced retreat.

## Phase 04: Commander Regions

- [~] Partition the map into regions or fronts.
- [ ] Replace or extend current region snapshots with deterministic rectangular regions.
- [x] Assign each commander to a region.
- [ ] Allow each Commander to have zero or more assigned rectangular regions.
- [x] Track region control, friendly/enemy unit count, city control, and border pressure.
- [x] Give each commander a region-local target and directive.
- [x] Add leaderless window when a commander dies.
- [ ] Add commander protection detail assignments.
- [~] Unit test regional ownership and commander-death effects.

## Phase 05: Resource Budgets And Replenishment

- [~] Add reserve pool separate from city production.
- [ ] Treat unassigned units as reserve in snapshots, AI observations, and map visualization.
- [ ] Let the General assign reserve units to Commanders.
- [ ] Let the General recall assigned units back to reserve.
- [ ] Let Commanders request reserve recall through reports, without changing assignments directly.
- [x] Add commander replenishment requests.
- [x] Add General budget allocation across commanders.
- [~] Add unit type preference per commander.
- [~] Add emergency replenishment behavior.
- [x] Keep the current human infantry/tank preference as a temporary General-level control until the budget UI replaces it.
- [ ] Add Commander production beside infantry and tank production.

## Phase 06: General Strategy Layer

- [x] Build a General perception with per-commander status, map control, reserves, time pressure, and General security.
- [x] Build General directives for region priority, resource budget, troop assignment, and strategic pause/rebuild mode.
- [ ] Make General-to-Commander, General-to-reserve, General-to-city, Commander-to-unit, unit-to-Commander, and Commander-to-General records explicit.
- [ ] Enforce one active executable command per Commander and per unit before considering queues or multiple simultaneous commands.
- [~] Allow the human General to set commander/region priorities rather than only one global target city.
- [ ] Add General relocation as a controlled action.
- [x] Add General security level and threat detection.

## Phase 07: Visibility And Scouting

- [x] Add visibility radius rules for units, commanders, and generals.
- [x] Add fog/intelligence filtering for AI observations.
- [~] Hide enemy General details unless scouted.
- [ ] Add scout role or scout directive.
- [~] Add Brinell/UAT checks for visible vs hidden information if UI changes are visible to the player.

## Phase 08: SharpNEAT And Training

- [ ] Add actual SharpNEAT package or a consciously chosen alternative.
- [~] Create a headless training runner project.
- [ ] Serialize genomes and training settings with `System.Text.Json`.
- [~] Implement Dot net fitness.
- [~] Implement Commander net fitness.
- [~] Implement General net fitness.
- [x] Add deterministic fixed-seed training smoke tests.
- [ ] Add generated genome loading into the main game.
- [x] Keep current deterministic genome archetypes as fallback/test controllers.

## Phase 09: Alternate Human Modes

- [x] Roadmap Human Commander mode.
- [ ] Roadmap Human General + one Commander mode.
- [ ] Roadmap Human Dot/chaos mode.
- [~] Define whether these modes are required for this game or reserved for expansion.
- [~] Build shared input interfaces only after the controller contracts exist.

## Phase 10: Emergent Behavior Instrumentation

- [~] Add telemetry for flanking, decapitation attempts, rout exploitation, commander threat, scout discoveries, and budget stress.
- [x] Add replay/fingerprint metadata for emergent behavior analysis.
- [~] Add AI-only watcher panels or export logs for training review.
- [x] Add UAT prompts for observing emergent behaviors without requiring them deterministically.

## Suggested Next Slice

Start by making command-and-control explicit:

- Continue from [40b-city-commands-commander-production-steps.md](40b-city-commands-commander-production-steps.md) or the next focused command-and-control slice; `40a`, `40c`, and `40d` now cover the first vision, hierarchy, attack-order, and reserve-command baseline.
- Keep [40-command-information-neat-roadmap.md](40-command-information-neat-roadmap.md) as the full command catalog and later expansion plan.
- Use the current vision model explicitly: infantry `5`, tank `6`, Commander `8`, General `10`, scout bonus `+2`, using Chebyshev distance.
- Keep command visualization compact with Commander numbers, reserve pips, and command pips/icons. Use lines only for selected/debug overlays.
- Keep real SharpNEAT deferred until deterministic hierarchy, assignments, and command diagnostics are stable.

Then continue the partially implemented prompt systems:

- Finish Phase 03 morale/rout with explicit commander-under-attack penalties, forced retreat tests, and rally behavior.
- Finish Phase 04 commander regions with real front partitioning and protection detail assignments.
- Finish Phase 07 visibility/scouting with a dedicated scout role and player-facing fog rules if desired.
- Revisit whether Phase 08 should use true SharpNEAT after the deterministic command model is stable.

This keeps the game playable while moving toward the original prompt's architecture without turning the next slice into a research project.

## Test Plan

- [x] Core unit tests for implemented simulation rules.
- [x] Determinism tests whenever AI observations, decisions, or training hooks changed.
- [x] Brinell UI tests for player-visible UI changes.
- [x] No SharpNEAT training test should require long-running generations in normal CI; use tiny deterministic smoke runs.

## Open Questions

- [x] Should `Infantry/Tank` remain the code names, or should code and UI move to `Light/Heavy`?
- [x] Should city capacity attrition be revived, or is single-cell grid occupancy the replacement?
- [x] Should true SharpNEAT be mandatory, or is deterministic NEAT-style AI acceptable for the local MVP?
  Answer: deterministic command-and-control and deterministic NEAT-style AI are acceptable for the local MVP. True SharpNEAT remains a later replacement or upgrade.
- [~] Should human Commander/Dot modes be first-class goals or later experiments?
- [ ] Should LLM controllers be part of this project now, or only a future adapter behind controller interfaces?
