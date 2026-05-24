# Smooth Unit Movement Roadmap

Status: implemented and verified.

## Goal

Make units move smoothly across the map instead of visually jumping from grid cell to grid cell, while keeping the tactical simulation deterministic and grid-based.

## Problem

Units currently update their rendered position only when their simulation cell changes. This makes movement readable in tests, but it looks jumpy to players because the visual marker snaps from one cell center to the next.

## Definitions

- `Simulation cell` means the authoritative grid cell used for movement, occupancy, combat, and pathfinding.
- `Visual position` means the interpolated map position used only for rendering and UI diagnostics.
- `Movement progress` means the normalized progress from one grid cell center to the next.
- Smooth movement must not allow two units to occupy the same simulation cell.
- Smooth movement must not change combat timing, city capture timing, or deterministic fingerprints unless explicitly intended.

## Proposed Design Decisions

- [x] Keep simulation authority on grid cells.
- [ ] Convert the whole simulation to continuous world coordinates.

- [x] Smooth only the visual marker position between cells.
- [ ] Move occupancy continuously between cells.
- [ ] Let units fight while halfway between cells.

- [x] Drive interpolation from fixed simulation state plus render-time alpha.
- [ ] Add more simulation ticks per second as the only smoothing method.
- [ ] Use random animation offsets to hide snapping.

- [x] Keep fingerprints based on simulation state, not render-only interpolation.
- [ ] Include visual interpolation in fingerprints.

- [x] Verify smoothness with Brinell by sampling unit positions across frames.
- [ ] Verify only by visual/manual inspection.

## Slice 1: Movement State Model

- [x] Add explicit movement state fields for visual interpolation, such as previous cell, next cell, movement start tick, and movement duration.
- [x] Keep `Cell` as the authoritative occupied simulation cell.
- [x] Keep `PathIndex` and pathing logic deterministic.
- [x] Ensure idle units report visual position equal to their current cell center.
- [x] Ensure moving units expose both authoritative cell and interpolated visual position in snapshots or map state.
- [x] Make movement state reset cleanly when a path is blocked, unit dies, combat starts, or the match restarts.

## Slice 2: Simulation Integration

- [x] When a unit begins a step, record the start cell and destination cell.
- [x] Progress movement over time using terrain movement cost and unit speed.
- [x] Move the authoritative `Cell` only when the step completes.
- [x] Reserve or block destination cells while a unit is visually moving toward them.
- [x] Keep adjacent combat checks based on authoritative cells only.
- [x] Stop or cancel interpolation if the destination becomes blocked.
- [x] Keep movement deterministic for identical seed and command inputs.

## Slice 3: Rendering Integration

- [x] Update `WairOfDotsGame` unit marker placement to use visual/interpolated positions.
- [x] Avoid recreating movement jumps when `RefreshMap` rebuilds UI children.
- [x] Keep city and terrain rendering unchanged.
- [x] Ensure commanders and generals use the same smooth movement path as infantry and tanks.
- [x] Ensure engaged/combat rings follow the interpolated unit marker.
- [x] Ensure AI-only spectator speed still looks coherent at higher speeds.

## Slice 4: Automation And Diagnostics

- [x] Extend Brinell map state with unit visual positions and authoritative grid cells.
- [x] Add a query that can return a specific moving unit's cell, target cell, visual position, and movement progress.
- [x] Ensure diagnostics can distinguish smooth visual movement from completed cell movement.
- [x] Keep existing occupancy diagnostics based on authoritative cells.
- [x] Keep screenshots useful for smoke testing the non-jumpy marker positions.

## Slice 5: Test Coverage

- [x] Unit test: a moving unit has a previous cell, next cell, and progress between `0` and `1`.
- [x] Unit test: authoritative `Cell` does not change until movement progress completes.
- [x] Unit test: visual position changes before authoritative `Cell` changes.
- [x] Unit test: blocked movement clears interpolation safely.
- [x] Unit test: combat still uses authoritative adjacent cells.
- [x] Unit test: fingerprints remain deterministic for the same seed.
- [x] Brinell gameplay test: a moving unit reports different visual positions across frames before entering the next cell.
- [x] Brinell smoke test: smooth movement does not produce duplicate occupied cells.
- [x] Brinell AI-only test: faster spectator speed still reports valid visual progress and no duplicate occupancy.
- [x] Run build, unit tests, and Brinell UI tests.

## Slice 6: Docs

- [x] Update `.my/HowToPlay.md` if movement timing or readability changes.
- [x] Update `.my/UAT.md` with a smooth movement visual acceptance check.
- [x] Update `.my/DevPrompts/Architecture.md` to document simulation-vs-visual movement authority.
- [x] Mark this roadmap complete after implementation and verification.

## Open Questions

- [x] Should smoothing be purely visual?
- [ ] Should smoothing change the simulation into continuous movement?
  Proposed answer: keep smoothing purely visual so occupancy, combat, and determinism remain stable.

- [x] Should destination cells be reserved while a unit is visually moving?
- [ ] Should destination cells remain open until the unit arrives?
  Proposed answer: reserve the destination to prevent two units from visually aiming at the same cell and then colliding.

- [x] Should Brinell assert smoothness through map-state coordinates?
- [ ] Should Brinell assert smoothness through image pixel comparisons?
  Proposed answer: use map-state coordinates first because it is deterministic and less brittle; keep screenshot smoke tests for visual regressions.

- [x] Should AI-only speed controls affect smoothness?
- [ ] Should AI-only speed skip animation entirely?
  Proposed answer: keep interpolation coherent at all speeds, but allow very high speeds to complete steps faster rather than faking slow animation.
