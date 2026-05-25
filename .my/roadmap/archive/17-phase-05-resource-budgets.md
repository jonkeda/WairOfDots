# Phase 05 Resource Budgets Roadmap

Status: implemented.

## Goal

Move from one player-wide production preference to a territory tax economy with commander budgets, upkeep pressure, replenishment requests, and reserve allocation.

## Scope

- [ ] Add reserve pool separate from city production.
- [ ] Add controlled-cell tax income as the primary income source.
- [ ] Add player treasury.
- [ ] Add unit upkeep costs.
- [ ] Add payroll deficit state.
- [ ] Add commander replenishment request model.
- [ ] Add General budget allocation per commander/region.
- [ ] Add emergency replenishment.
- [ ] Add per-commander infantry/tank preference.
- [ ] Keep current player-wide infantry/tank preference as a fallback until budget UI exists.

## Design Decisions

- [x] Cities remain spawn anchors, but income comes from controlled cell taxes.
- [ ] Decide whether purchases happen automatically from treasury or through explicit General/Commander budgets.
- [ ] Decide whether city production is removed entirely or bridged into treasury-funded purchases.
- [ ] Decide whether commanders request units, resources, or both.
- [ ] Decide if unused budgets roll over.
- [ ] Decide if human General controls budgets through sliders, steppers, or command buttons.
- [ ] Decide whether payroll deficit affects all units equally or expensive units first.

## Implementation Slices

- [ ] Add reserve/resource budget model.
- [ ] Add tax collection from controlled cells.
- [ ] Add upkeep deduction.
- [ ] Add payroll deficit calculation.
- [ ] Update production into treasury-funded purchases near owned cities.
- [ ] Add commander replenishment requests.
- [ ] Add General allocation decision.
- [ ] Add unit assignment from reserve to commander region.
- [ ] Update AI observations and telemetry.

## Test Plan

- [ ] Unit tests for budget allocation.
- [ ] Unit tests for tax income.
- [ ] Unit tests for upkeep deduction.
- [ ] Unit tests for payroll deficit state.
- [ ] Unit tests for city-adjacent spawning from treasury purchases.
- [ ] Unit tests for emergency replenishment.
- [ ] Determinism tests.
- [ ] Brinell tests when budget UI exists.
