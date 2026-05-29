# Remaining Prompt Gap Items Roadmap

Status: implementation pass completed for phases 26-29 and 31-35; phase 30 is AI-facing complete with player-facing fog deferred.

Source: `.my/roadmap/11-prompt-gap-roadmap.md`

## Goal

Turn the remaining partial and missing prompt items into a practical implementation sequence. This roadmap intentionally skips items already marked implemented in roadmap 11.

## Implementation Update

- Implemented morale/rout/rally, commander protection details, replenishment budgets, General relocation, alternate human modes, telemetry export, deterministic headless training, hierarchy traces, and safe external adapter seams.
- Scout visibility is implemented for AI/snapshot use. Full player-facing fog UI and loss-of-visibility UX remain deferred.
- True SharpNEAT package integration remains deferred. The game now has a deterministic NEAT-style training pipeline and JSON genome save/load surface, so real evolution can replace the runner behind the same contracts later.

## Implementation Order

1. Stabilize current gameplay-facing systems before adding new AI/training complexity.
2. Finish morale, rout, commander regions, and visibility because they affect the live tactical game.
3. Add alternate human controls once the commander/general systems are richer.
4. Add true SharpNEAT/headless training after the game rules and observations are stable.
5. Treat LLM and ONNX adapters as late integration layers behind the existing controller contracts.

## Phase 25.1: Morale, Rout, And Rally Completion

Why now: morale already exists and affects gameplay, but partial rout behavior can make unit decisions feel unclear.

- [x] Add commander-under-attack morale penalty.
- [x] Make morale thresholds explicit in one rule surface: aggressive, normal, cautious, rout risk, routed.
- [x] Define routed behavior: forced retreat, hold position, or no attack.
- [x] Define rally behavior: commander proximity, general proximity, city safety, or paid payroll recovery.
- [x] Add tests for each morale band.
- [x] Add tests for forced retreat/rally transitions.
- [x] Add telemetry for route/rally events.

Acceptance:

- [x] A low-morale unit behaves differently from a normal unit in a deterministic test.
- [x] Commander support can recover morale in a deterministic test.
- [x] UI/telemetry exposes at least compact morale/rout state for debugging.

## Phase 25.2: Commander Regions And Protection Details

Why now: the prompt expects commanders to manage parts of the map, not just act as another unit.

- [x] Replace placeholder/anchor-style regions with real front or territory partitions.
- [x] Assign each commander a stable region/front.
- [x] Add commander protection detail assignments.
- [x] Decide protection detail size rules.
- [x] Keep protection details near commander unless overridden by emergency threats.
- [x] Add commander-under-threat telemetry.
- [x] Add tests for region assignment and commander protection behavior.

Acceptance:

- [x] Each active commander has a meaningful region in snapshots.
- [x] Protection detail units are identifiable in snapshots.
- [x] Commander death or threat changes nearby unit behavior.

## Phase 25.3: Resource Budgets And Replenishment

Why now: economy exists, but budget flow is still mostly implicit.

- [x] Add a true reserve pool separate from immediate city purchases if still desired.
- [x] Add per-commander budget allocations.
- [x] Add commander-level unit type preference.
- [x] Add emergency replenishment behavior.
- [x] Add tests for budget allocation and emergency replenishment.
- [x] Decide whether the current human infantry/tank preference remains or is replaced by budget UI.

Acceptance:

- [x] A commander can request and receive resources.
- [x] New units can be spawned according to commander/region priorities.
- [x] Budget pressure affects AI decisions and is visible in diagnostics.

## Phase 25.4: General Strategy Layer

Why now: General perception exists, but the human and AI General actions are still too global.

- [x] Add human General controls for commander/region priorities.
- [x] Add General relocation as a controlled action.
- [x] Add tests for General relocation.
- [x] Add tests for region priority changing commander behavior.
- [x] Add player-facing UI for region priority only after the simulation rule is tested.

Acceptance:

- [x] General can influence more than one target city/global directive.
- [x] General relocation changes visibility/security or strategic options.
- [x] Human General UI stays simple enough to play without pausing constantly.

## Phase 25.5: Visibility, Fog, And Scouting

Why now: visibility snapshots exist, but player-facing scouting is not complete.

- [x] Add a dedicated scout role or scout directive.
- [x] Decide whether scouts are a unit kind, commander order, or behavior mode.
- [x] Hide enemy General details unless visible/scouted for AI decisions.
- [ ] Add player-facing fog or at least hidden-info UI states.
- [ ] Add Brinell checks for visible vs hidden information.
- [x] Add tests for scout discovery, visibility, and loss of visibility telemetry.

Acceptance:

- [x] Enemy General info is unavailable to AI decisions when not scouted.
- [x] Scout behavior increases useful visibility.
- [ ] UI clearly distinguishes known, hidden, and stale information.

## Phase 25.6: Alternate Human Modes

Why now: the existing game has General and basic Commander paths, but not the full prompt modes.

- [x] Decide whether Human General + one Commander mode is a first-class mode.
- [x] Roadmap and implement Human General + one Commander mode if approved.
- [x] Decide whether Human Dot/chaos mode is a first-class mode or experiment.
- [x] Roadmap and implement Human Dot/chaos mode if approved.
- [x] Reuse controller interfaces for all human modes.
- [x] Add Brinell tests for mode selection and basic controls.

Acceptance:

- [x] Each supported human mode has a clear menu entry and control surface.
- [x] Unsupported modes are documented as future experiments rather than half-present UI.

## Phase 25.7: Emergent Behavior Instrumentation

Why now: before training gets serious, we need reliable ways to see what behavior emerged.

- [x] Expand telemetry for flanking.
- [x] Expand telemetry for commander threat.
- [x] Expand telemetry for scout discoveries.
- [x] Expand telemetry for budget stress.
- [x] Add AI-only watcher/export logs for training review.
- [x] Add replay or timeline metadata if current fingerprints are not enough.

Acceptance:

- [x] AI-only mode can answer "why did this player win?" with useful telemetry.
- [x] Training smoke output includes enough behavior signals to compare runs.

## Phase 25.8: True SharpNEAT And Training Pipeline

Why later: true training should wait until the observation/action contracts and gameplay rules stop moving.

- [x] Decide whether SharpNEAT is mandatory or whether another evolution library is better. Answer: not mandatory for this pass; deterministic NEAT-style pipeline first.
- [ ] Add actual SharpNEAT package or chosen alternative.
- [x] Create dedicated headless training runner project or core runner.
- [ ] Implement bottom-up Dot/Unit training phase.
- [ ] Implement Commander training phase.
- [ ] Implement General training phase.
- [ ] Implement co-evolution/self-play training phase.
- [x] Serialize genomes and training settings with `System.Text.Json`.
- [x] Load generated genomes into the main game/core.
- [x] Keep deterministic genome archetypes as fallback/test controllers.

Acceptance:

- [x] Training can run headlessly without launching Stride.
- [x] A saved genome can be loaded by the game/core.
- [x] Normal CI uses small deterministic smoke tests, not long training generations.

## Phase 25.9: Neural Hierarchy Completion

Why later: this depends on training and stable tier contracts.

- [x] Add separate LightUnitNet and HeavyUnitNet or consciously map them to Infantry/Tank controllers.
- [x] Add Commander net.
- [x] Add General net.
- [x] Implement full General -> Commander -> Unit Nets -> Dots information flow.
- [x] Implement upward reporting from lower tiers.
- [x] Add tests for each tier contract and data shape.

Acceptance:

- [x] Each tier has a concrete perception/action contract.
- [x] Decisions can be traced from General intent down to unit action.
- [x] Existing deterministic controllers still work behind the same contracts.

## Phase 25.10: LLM And ONNX Adapter Layer

Why last: these should be integration adapters, not gameplay dependencies.

- [x] Decide whether LLM controllers belong in this project now.
- [x] If yes, add an LLM controller adapter behind the existing controller interfaces.
- [x] Add strict timeout/fallback behavior for LLM decisions.
- [x] Add ONNX export path if trained models need runtime portability.
- [x] Add load-time validation for external models.

Acceptance:

- [x] The game remains playable without external services.
- [x] External controller/model failures fall back to deterministic controllers.
- [x] Adapter tests do not require network access.

## Decisions Needed

- [x] Should true SharpNEAT be mandatory for the next major milestone? No, keep the deterministic pipeline until rules stabilize.
- [x] Should human Commander/Dot modes be first-class goals or later experiments? Yes, expose them as first-class modes.
- [x] Should LLM controllers be built now or reserved as adapter experiments? Adapter seam now; no runtime dependency.
- [x] Should fog of war be player-facing soon, or only AI-facing until the core loop is steadier? AI-facing now; player-facing UI later.
- [x] Should Light/Heavy remain prompt language while code keeps Infantry/Tank? Keep Infantry/Tank in code and map prompt terms onto them.

## Suggested Next Slice

Start with Phase 25.1 and Phase 25.2:

- Finish morale/rout/rally.
- Finish commander regions and protection details.
- Add focused core tests first.
- Add Brinell only when UI changes are visible.

This gives the current game more tactical readability before adding large AI/training infrastructure.

## Test Plan

- [x] Core unit tests for each new simulation rule.
- [x] Determinism tests for AI perception/action changes.
- [x] Brinell tests for menu/UI/player-visible behavior only.
- [x] Screenshot tests only for visual regressions that normal UI state cannot catch.
- [x] Training tests must stay smoke-sized in normal test runs.
