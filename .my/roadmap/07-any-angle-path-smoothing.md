# Any-Angle Path Smoothing Roadmap

Status: proposed.

## Goal

Make movement look more natural than fixed grid directions by smoothing unit paths into longer clear-line segments, while keeping the grid as the authoritative tactical model.

## Problem

Eight-way movement is a strong improvement over cardinal-only movement, but units can still look like they are following a visible grid. Players read smoother lines as more normal movement, especially across open terrain.

## Definitions

- `Any-angle movement` means a unit can visually travel along a straight segment that is not limited to cardinal or diagonal directions.
- `Grid authority` means occupancy, pathfinding legality, combat, capture, scoring, and fingerprints still resolve through grid cells.
- `Path smoothing` means simplifying an existing grid path by removing intermediate waypoints when line of sight is clear.
- `Line of sight` means a straight segment crosses only passable, unblocked, unreserved cells.
- `Waypoint` means a map position or grid cell center selected from the smoothed path.

## Proposed Design Decisions

- [x] Keep grid authority for tactics.
- [ ] Convert the whole game to continuous physics movement.

- [x] Use eight-way A* as the base path.
- [ ] Replace pathfinding with a full navmesh immediately.

- [x] Smooth paths after A* using line-of-sight checks.
- [ ] Let units choose arbitrary headings without a grid path.
- [ ] Add random visual wobble to hide grid movement.

- [x] Keep combat and city capture cell-based.
- [ ] Let combat trigger from continuous ranges.

- [x] Keep Brinell assertions based on path and visual diagnostics.
- [ ] Verify only by screenshots.

## Slice 1: Path Smoothing Model

- [ ] Add a deterministic line-of-sight helper over grid cells.
- [ ] Add a path-smoothing pass that removes unnecessary intermediate cells.
- [ ] Keep the original grid path available for occupancy and movement reservation.
- [ ] Represent smoothed waypoints separately from authoritative grid cells.
- [ ] Ensure smoothing never crosses water, blocked cells, occupied cells, or reserved cells.

## Slice 2: Movement Integration

- [ ] Keep each simulation step moving through legal grid cells.
- [ ] Use smoothed waypoints for visual interpolation where the grid sequence is clear.
- [ ] Update visual state to support longer segments than one cell center.
- [ ] Preserve deterministic step timing from terrain and unit speed.
- [ ] Reset smoothed visual movement when paths are blocked, combat starts, units die, or orders change.

## Slice 3: Tactical Rules

- [ ] Keep one unit per authoritative cell.
- [ ] Keep city capture on the city grid cell.
- [ ] Keep combat based on authoritative grid adjacency or tactical range.
- [ ] Ensure units do not visually pass through enemy or friendly occupied cells.
- [ ] Keep AI observation and fitness based on tactical cells, not visual-only positions.

## Slice 4: Rendering And Diagnostics

- [ ] Update unit visual diagnostics to report smoothed segment start, end, and progress.
- [ ] Keep current cell and next authoritative cell visible in Brinell diagnostics.
- [ ] Add a diagnostic flag that indicates whether a unit is using a smoothed multi-cell segment.
- [ ] Confirm map markers glide cleanly on non-8-way angles.
- [ ] Keep screenshots useful as smoke checks, but use map-state diagnostics for assertions.

## Slice 5: Test Coverage

- [ ] Unit test: line-of-sight passes through clear cells.
- [ ] Unit test: line-of-sight fails on water or blocked terrain.
- [ ] Unit test: path smoothing removes intermediate cells on open terrain.
- [ ] Unit test: path smoothing preserves cells around obstacles and occupied cells.
- [ ] Unit test: authoritative occupancy remains single-cell and deterministic.
- [ ] Brinell gameplay test: a unit reports a smoothed visual segment whose delta is not cardinal or diagonal.
- [ ] Brinell smoke test: smoothing keeps duplicate occupied cells at `0`.
- [ ] Run build, unit tests, and Brinell UI tests.

## Slice 6: Docs

- [ ] Update `.my/HowToPlay.md` to describe natural-looking movement while clarifying grid-based tactics.
- [ ] Update `.my/UAT.md` with any-angle movement visual checks.
- [ ] Update `.my/DevPrompts/Architecture.md` to document grid authority plus smoothed visual waypoints.
- [ ] Mark this roadmap complete after implementation and verification.

## Open Questions

- [x] Should smoothing be visual-only first?
- [ ] Should smoothing change tactical movement into continuous collision?
  Proposed answer: keep smoothing visual-only first. It gives the player the feel of normal movement without destabilizing tests, AI, and occupancy.

- [x] Should line-of-sight smoothing run after A*?
- [ ] Should pathfinding itself be replaced with Theta* immediately?
  Proposed answer: use post-A* smoothing first because it is simpler to test and easier to layer on top of eight-way movement.

- [x] Should Brinell verify any-angle movement through diagnostics?
- [ ] Should Brinell verify by pixel comparison?
  Proposed answer: use diagnostics for reliable assertions and keep screenshots for broad smoke testing.
