# Phase 01 Controller Interfaces Roadmap

Status: implemented.

## Goal

Introduce controller interfaces for General, Commander, and Unit layers while preserving the current AI behavior.

## Scope

- [ ] Add `IGeneralController`.
- [ ] Add `ICommanderController`.
- [ ] Add `IUnitController`.
- [ ] Add perception/action records for each layer.
- [ ] Add economy fields to perception records: controlled-cell ratio, tax income, treasury, upkeep, payroll deficit, and spawn capacity.
- [ ] Add purchase/spawn intent to action records without forcing UI changes yet.
- [ ] Adapt the current deterministic `GenomeNeatController` into the General-level controller path.
- [ ] Keep current `Attack`, `Hold`, `Defend`, target city, and infantry/tank preference behavior unchanged.
- [ ] Keep human controls working through the same General-level contract.
- [ ] Keep AI-only mode deterministic.

## Design Decisions

- [x] Preserve `Infantry` and `Tank` names in contracts.
- [x] Use the current grid/city model, not city-edge movement.
- [x] Use the cell territory/tax economy as the resource model for new contracts.
- [x] Treat the current AI as a General-level placeholder until true hierarchy exists.
- [ ] Decide whether old `IAiController` remains as an adapter or is renamed.
- [ ] Decide where controller contracts live: `WairOfDots.Core` root or a subfolder/namespace.

## Implementation Slices

- [ ] Define General perception and directive records from existing `AiObservation` / `AiDecision`.
- [ ] Extend General perception with tax base, treasury, upkeep, and controlled-cell pressure.
- [ ] Extend Commander and Unit perceptions with local controlled-cell state where relevant.
- [ ] Add adapter from current AI controller to the new General interface.
- [ ] Add no-op/deterministic Commander and Unit controllers if needed for compilation seams.
- [ ] Route `GameSimulation.UpdateAiPlan` through the General controller interface.
- [ ] Add unit tests proving old and new decisions match for fixed seeds.
- [ ] Add determinism/fingerprint regression test.

## Test Plan

- [ ] Unit tests for deterministic controller output.
- [ ] Unit tests for economy-aware perception fields.
- [ ] Unit tests for AI-only fingerprint stability.
- [ ] No Brinell test unless UI behavior changes.
