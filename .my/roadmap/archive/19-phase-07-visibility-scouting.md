# Phase 07 Visibility And Scouting Roadmap

Status: implemented.

## Goal

Add perception limits, scouting, and hidden General information so AI and human players do not always act on perfect information.

## Scope

- [ ] Add visibility radius rules for units.
- [ ] Add commander regional visibility.
- [ ] Add General full-map degraded visibility.
- [ ] Hide enemy General details unless scouted.
- [ ] Add scout role/directive.
- [ ] Filter AI observations through visibility.
- [ ] Decide and implement whether cell ownership and tax value are always visible or visibility-filtered.
- [ ] Decide and implement whether enemy treasury/upkeep pressure is hidden, estimated, or visible.

## Design Decisions

- [ ] Decide whether the human gets fog of war visually.
- [ ] Decide whether AI and human use the same visibility rules.
- [ ] Decide scout unit type: infantry role, new unit, or directive.
- [ ] Decide how long scouted information remains remembered.
- [ ] Decide whether territory/tax information needs scouting memory.

## Implementation Slices

- [ ] Add visibility service in core.
- [ ] Add observed/enemy-known state.
- [ ] Filter unit/city/general observations.
- [ ] Filter or expose controlled-cell ownership and tax values according to the visibility decision.
- [ ] Add scout behavior.
- [ ] Add hidden General mechanics.
- [ ] Add telemetry for scout discoveries.

## Test Plan

- [ ] Unit tests for visibility radius.
- [ ] Unit tests for hidden General filtering.
- [ ] Unit tests for scout discovery.
- [ ] Unit tests for visible/hidden territory economy information.
- [ ] Brinell tests if fog/hidden information changes UI.
