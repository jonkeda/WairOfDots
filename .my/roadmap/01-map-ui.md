# Map UI Roadmap

## Goal

Update the Stride UI from a text-first command panel into a playable tactical map view inspired by the provided reference image.

## Slice 1: Visible Map

- [x] Add a large map canvas to the running game screen.
- [x] Keep controls visible beside the map.
- [x] Render land as a green base layer.
- [x] Render terrain patches for water, roads, hills, rocks, and forest.
- [x] Render graph edges between cities.
- [x] Render cities as selectable circles/buttons.

## Slice 2: Army Markers

- [x] Render unit groups as colored circle markers.
- [x] Render commanders as ringed circle markers.
- [x] Render generals as filled/ringed circle markers.
- [x] Color markers by player ownership.
- [x] Offset stacked circles so armies are readable.

## Slice 3: Acceptance

- [x] Update Brinell UI tests to assert the map is visible.
- [x] Add game-query coverage for terrain and marker counts.
- [x] Build and run unit/UI tests.

## Future Map Work

- [ ] Replace simple UI glyph circles with generated bitmap sprites or mesh discs.
- [ ] Animate moving groups along edges.
- [ ] Add terrain effects to pathfinding and combat.
- [ ] Add pan/zoom controls.
- [ ] Add hover/selection details for cities and armies.
