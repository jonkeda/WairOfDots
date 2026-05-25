# Phase 09 Alternate Human Modes Roadmap

Status: implemented for Human Commander mode; General+Commander and Dot/chaos remain core hooks for later UI work.

## Goal

Plan human Commander, General-plus-Commander, and Dot/chaos modes without disrupting the current Human General mode.

## Scope

- [ ] Human Commander mode roadmap.
- [ ] Human General + one Commander mode roadmap.
- [ ] Human Dot/chaos mode roadmap.
- [ ] Shared controller input interfaces.
- [ ] Mode selection UI.
- [ ] Define how each human mode interacts with taxes, treasury, upkeep, and city spawn anchors.
- [ ] UAT docs for each mode that becomes real.

## Design Decisions

- [ ] Decide whether alternate modes are first-class goals or experiments.
- [ ] Decide implementation order.
- [ ] Decide if co-op is in scope.
- [ ] Decide whether Dot mode requires new rendering/camera tech.
- [ ] Decide whether Commander/Dot modes can buy units directly or only request budget/reinforcement.

## Implementation Slices

- [ ] Add mode enum values after controller contracts exist.
- [ ] Add Commander mode input flow.
- [ ] Add General+Commander switching flow.
- [ ] Prototype Dot mode only after unit-level control exists.
- [ ] Add Brinell tests for mode selection and visible controls.

## Test Plan

- [ ] Unit tests for mode-specific command routing.
- [ ] Brinell tests for mode-specific UI.
- [ ] UAT docs per supported mode.
