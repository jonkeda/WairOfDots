# Phase 06 General Strategy Layer Roadmap

Status: implemented.

## Goal

Expand the General layer from a global target/directive into full strategic command over regions, commanders, reserves, and General safety.

## Scope

- [ ] Build General perception from commander status reports.
- [ ] Include map control, controlled-cell value, tax income, treasury, upkeep pressure, reserve pool, time pressure, and General security.
- [ ] Output region priorities.
- [ ] Output resource budgets.
- [ ] Output troop assignments.
- [ ] Add strategic pause/rebuild mode.
- [ ] Add General relocation action.

## Design Decisions

- [ ] Decide how many region priorities can change per planning interval.
- [ ] Decide whether General relocation is manual only, AI only, or both.
- [ ] Decide how General security is measured.
- [ ] Decide how the current human city target maps into future region priorities.
- [ ] Decide how the General prioritizes high-value tax cells versus city spawn anchors.
- [ ] Decide when the General should stop buying units because payroll is risky.

## Implementation Slices

- [ ] Add General perception/action records.
- [ ] Build perception from region/commander/resource state.
- [ ] Add economy risk scoring from tax base, treasury, upkeep, and payroll deficit.
- [ ] Add General directive application.
- [ ] Add General relocation pathing/action.
- [ ] Add AI and human adapters.
- [ ] Update HUD after the strategy model exists.

## Test Plan

- [ ] Unit tests for General perception.
- [ ] Unit tests for economy priority scoring.
- [ ] Unit tests for directive application.
- [ ] Unit tests for General relocation.
- [ ] Brinell tests for new human controls.
