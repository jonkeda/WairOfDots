# Phase 03 Morale And Rout System Roadmap

Status: implemented.

## Goal

Turn morale from a light combat modifier into an explicit 0-1 gameplay system with thresholds, rallying, and retreat behavior.

## Scope

- [ ] Normalize morale after every mutation.
- [ ] Implement morale loss for taking damage.
- [ ] Implement morale gain for dealing damage.
- [ ] Track nearby friendly deaths.
- [ ] Add outnumbered pressure.
- [ ] Add commander-nearby rally effects.
- [ ] Add commander-under-attack penalty.
- [ ] Add commander-death morale collapse.
- [ ] Add general-death morale collapse before eliminated units are removed.
- [ ] Add payroll deficit morale loss from the cell tax/upkeep economy.
- [ ] Implement morale thresholds: aggressive, normal, cautious, rout risk, forced retreat.
- [ ] Add forced retreat behavior that respects grid occupancy.

## Design Decisions

- [x] Use current grid-local radius checks, not city stack counts.
- [ ] Decide exact radius for nearby deaths and rallying.
- [ ] Decide whether routed units flee toward owned cities, commanders, or map edge.
- [ ] Decide whether morale changes attack power, movement, directive choice, or all three.
- [ ] Decide whether payroll morale damage affects every unit equally or expensive units first.
- [ ] Decide whether morale recovers when payroll becomes healthy again.

## Implementation Slices

- [ ] Add morale helper methods to core simulation.
- [ ] Add event memory for recent friendly deaths.
- [ ] Apply morale changes during combat resolution.
- [ ] Apply morale aura effects during each tick.
- [ ] Apply payroll deficit morale loss during the economy interval.
- [ ] Add retreat/rout movement target selection.
- [ ] Expose morale in snapshots if UI/UAT needs it.

## Test Plan

- [ ] Unit test morale clamping.
- [ ] Unit test damage/dealing damage morale deltas.
- [ ] Unit test nearby friendly death penalty.
- [ ] Unit test commander rally.
- [ ] Unit test payroll deficit morale loss.
- [ ] Unit test paid armies avoid payroll morale loss.
- [ ] Unit test forced retreat at low morale.
- [ ] Brinell only if morale becomes visible in HUD.
