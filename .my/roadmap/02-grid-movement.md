# Grid Movement Roadmap

## Goal

Move Wair of Dots from city-edge travel to grid-based movement, while keeping cities as the primary command, capture, and production points.

## Slice 1: Grid Map Model

- [x] Add a deterministic `GridMap` with passable and blocked cells.
- [x] Convert visual terrain patches into grid terrain cells.
- [x] Keep water blocked.
- [x] Give road, grass, forest, hill, and rock different movement costs.
- [x] Snap every city to a passable grid cell.

## Slice 2: Unit Movement

- [x] Replace city-neighbor routing with grid pathfinding.
- [x] Dispatch units from a source city to the target city across grid cells.
- [x] Move groups cell-by-cell instead of decrementing edge distance.
- [x] Track moving groups by current grid cell and current map position.
- [x] Deposit units into the target city when the grid path completes.

## Slice 3: UI Update

- [x] Render moving unit circles at their current grid position.
- [x] Keep cities, commanders, and generals as circle markers.
- [x] Keep the existing terrain patch rendering while the simulation uses terrain cells underneath.
- [x] Expose grid dimensions and passable cell counts through Brinell game queries.

## Slice 4: Test Coverage

- [x] Add unit tests for grid creation and terrain passability.
- [x] Add unit tests proving dispatched groups receive grid paths.
- [x] Add unit tests proving groups advance across cells.
- [x] Update Brinell UI tests to verify grid map state.
- [x] Run build, unit tests, and Brinell UI tests.

## Future Work

- [ ] Draw faint grid-cell overlays when debug mode is enabled.
- [ ] Let units occupy/interact with non-city cells for interception.
- [ ] Add terrain-specific combat modifiers.
- [ ] Add click-to-cell commands after city targeting feels good.
- [ ] Replace text glyph markers with mesh or sprite circles.
