# Phase 04 Commander Regions Roadmap

Status: implemented.

## Goal

Add commander-owned regions/fronts so commanders can become meaningful tactical leaders below the General layer.

## Scope

- [ ] Partition the map into regions or fronts.
- [ ] Assign each commander to one region.
- [ ] Track region cities, friendly units, enemy units, border pressure, and control ratio.
- [ ] Track region controlled-cell count and controlled-cell tax value.
- [ ] Track region payroll pressure and spawn access through owned cities.
- [ ] Give commanders region-local target priorities.
- [ ] Add leaderless window when a commander dies.
- [ ] Add commander protection detail assignments.

## Design Decisions

- [x] Build regions on top of the grid model.
- [x] Region value should aggregate controlled cells and tax value, not only cities.
- [ ] Decide whether regions are static at match start or dynamic front lines.
- [ ] Decide whether each player has one commander or eventually multiple commanders.
- [ ] Decide how cities map to regions.

## Implementation Slices

- [ ] Add region model to core.
- [ ] Add deterministic default region generation.
- [ ] Add commander-region assignment.
- [ ] Add region observation builder.
- [ ] Add region economy summary: tax value, controlled cells, payroll pressure, and city spawn anchors.
- [ ] Add commander order output.
- [ ] Route some unit orders through commander region orders.
- [ ] Add commander death leaderless timer.
- [ ] Add protection detail assignment/reservation behavior.

## Test Plan

- [ ] Unit tests for region generation.
- [ ] Unit tests for commander assignment.
- [ ] Unit tests for region observations.
- [ ] Unit tests for region tax/control aggregation.
- [ ] Unit tests for leaderless window.
- [ ] Brinell only if region UI is added.
