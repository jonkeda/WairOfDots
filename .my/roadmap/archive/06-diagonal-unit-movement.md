# Diagonal Unit Movement Roadmap

Status: implemented and verified.

## Goal

Let tactical units move diagonally across the grid while preserving single-cell occupancy, terrain costs, smooth visual movement, deterministic AI behavior, and Brinell test coverage.

## Problem

Units currently move only through the four cardinal grid neighbors. That makes movement easy to reason about, but it can look stiff and can create unnatural routes around terrain, cities, and other units.

## Definitions

- `Cardinal movement` means north, east, south, and west.
- `Diagonal movement` means northeast, southeast, southwest, and northwest.
- `Eight-way movement` means cardinal plus diagonal movement.
- `Corner cutting` means moving diagonally through the corner between two blocked, occupied, or reserved cells.
- `Authoritative cell` remains the unit's `Cell` and is still the source of truth for occupancy, capture, combat, scoring, and fingerprints.

## Proposed Design Decisions

- [x] Add eight-way movement to pathfinding.
- [ ] Keep movement cardinal-only.
- [ ] Add diagonal movement only for human units.

- [x] Make diagonal step cost `terrain move cost * sqrt(2)`.
- [ ] Charge the same cost for cardinal and diagonal movement.
- [ ] Add a flat custom diagonal penalty.

- [x] Prevent diagonal corner cutting through blocked terrain and occupied or reserved cells.
- [ ] Allow diagonal corner cutting.
- [ ] Prevent corner cutting only through terrain, not through units.

- [x] Let diagonal movement use the existing smooth visual interpolation.
- [ ] Add a separate diagonal animation system.

- [x] Treat diagonal cells as adjacent for combat after diagonal movement is introduced.
- [ ] Keep combat cardinal-only.
- [ ] Add diagonal combat later as a separate roadmap.

- [x] Keep single occupancy unchanged: a diagonal destination can still hold only one unit.
- [ ] Allow diagonal swaps between two units in one tick.

- [x] Keep city production cardinal-only for this slice.
- [ ] Allow city production on all eight neighboring cells.

- [x] Verify with unit tests and Brinell map-state diagnostics.
- [ ] Verify only by manual playtesting.

## Slice 1: Grid Neighbor Model

- [x] Replace the movement neighbor helper with an explicit eight-direction neighbor model.
- [x] Keep a cardinal-neighbor helper if production or special rules still need cardinal-only behavior.
- [x] Add direction metadata or helper methods so diagonal moves can be detected clearly.
- [x] Update pathfinding ordering to remain deterministic by cost, then coordinates, then direction.
- [x] Update pathfinding heuristic from Manhattan distance to an eight-way-friendly heuristic such as octile distance.

## Slice 2: Movement Cost And Blocking

- [x] Apply terrain cost to every destination cell.
- [x] Multiply diagonal move cost by `sqrt(2)`.
- [x] Prevent diagonal movement when either orthogonal side cell needed for the diagonal step is blocked by terrain.
- [x] Prevent diagonal movement when either orthogonal side cell is occupied or reserved by another unit.
- [x] Keep destination-cell reservation for partially completed smooth moves.
- [x] Keep path clearing safe when a diagonal destination becomes blocked.

## Slice 3: Combat And Tactical Rules

- [x] Update adjacency checks so diagonal neighboring enemy units can fight.
- [x] Ensure commanders and generals use the same diagonal movement and combat adjacency rules.
- [x] Keep city capture based only on occupying the city cell.
- [x] Keep city production on cardinal adjacent empty cells for this slice.
- [x] Keep no-stacking rules for all units, commanders, and generals.

## Slice 4: Rendering And Brinell Diagnostics

- [x] Confirm smooth unit movement renders correctly on diagonal paths.
- [x] Ensure engaged/combat rings follow diagonal movement interpolation.
- [x] Extend or reuse `GetUnitVisuals` to prove diagonal target cells and visual positions differ on both axes.
- [x] Keep map occupancy diagnostics based on authoritative cells.
- [x] Add Brinell checks that diagonal movement does not create duplicate occupied cells.

## Slice 5: Test Coverage

- [x] Unit test: pathfinding can choose a diagonal step when it is the shortest legal route.
- [x] Unit test: diagonal cost is greater than cardinal cost and respects terrain.
- [x] Unit test: diagonal movement is blocked by corner-cutting terrain.
- [x] Unit test: diagonal movement is blocked by occupied or reserved corner cells.
- [x] Unit test: diagonal combat adjacency works.
- [x] Unit test: single occupancy still holds after diagonal moves.
- [x] Unit test: same seed remains deterministic after diagonal pathfinding.
- [x] Brinell gameplay test: at least one moving unit reports a diagonal visual target.
- [x] Brinell smoke test: diagonal movement keeps duplicate occupied cells at `0`.
- [x] Run build, unit tests, and Brinell UI tests.

## Slice 6: Docs

- [x] Update `.my/HowToPlay.md` to mention diagonal movement.
- [x] Update `.my/UAT.md` with a diagonal movement acceptance check.
- [x] Update `.my/DevPrompts/Architecture.md` to document eight-way movement and diagonal combat adjacency.
- [x] Mark this roadmap complete after implementation and verification.

## Open Questions

- [x] Should diagonal movement cost more than cardinal movement?
- [ ] Should diagonal movement cost the same as cardinal movement?
  Proposed answer: diagonal movement should cost `sqrt(2)` times the destination terrain cost so diagonal travel is not a free speed boost.

- [x] Should units be prevented from cutting corners?
- [ ] Should units be allowed to cut corners?
  Proposed answer: prevent corner cutting through blocked terrain, occupied cells, and reserved cells so movement remains readable and does not visually pass through units or water.

- [x] Should diagonal neighbors count for combat?
- [ ] Should combat remain cardinal-only?
  Proposed answer: count diagonal neighbors for combat once movement is eight-way, because players will read diagonal contact as adjacency.

- [x] Should smooth visual movement be reused?
- [ ] Should diagonal movement get a separate animation system?
  Proposed answer: reuse the existing visual interpolation. A diagonal step is still just a visual movement from one grid cell center to another.

- [x] Should city production stay cardinal-only?
- [ ] Should city production use diagonal output cells?
  Proposed answer: keep production cardinal-only for this slice so diagonal movement does not increase production space or city pressure unexpectedly.
