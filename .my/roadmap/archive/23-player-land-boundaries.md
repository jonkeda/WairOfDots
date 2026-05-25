# Player Land Boundaries Roadmap

Status: implemented.

## Goal

Show the borders between controlled territory cells so players can read each army's land without needing a dense full-cell overlay.

## Problem

Cell ownership now matters for taxes, spawning pressure, and strategy. The map can expose ownership through diagnostics, but the player still needs a readable visual boundary that shows where one player's land ends and another player's land begins.

## Design Direction

- [x] Draw boundaries between neighboring controlled cells with different owners.
- [x] Keep city square ownership colors as-is.
- [x] Keep unit circles visually dominant over territory boundaries.
- [x] Do not fill every owned cell with a strong color.
- [x] Do not add city number labels or the old city side list back.

## Proposed Visual Rules

- [x] Draw thin border segments along grid-cell edges where adjacent owner ids differ.
- [x] Use the owning player's color for the inside side of the border.
- [x] Use neutral yellow boundaries around neutral territory only when it improves readability.
- [x] Use a single thin owner-color line so units and city squares stay dominant.
- [x] Merge adjacent line segments into longer outline strokes.
- [x] Filter tiny isolated ownership islands so the map does not fill with small loops.
- [x] Render boundaries above terrain but below cities and units.
- [x] Hide boundaries for water or uncapturable cells.
- [ ] Consider thicker or brighter boundaries around the human player's territory.

## Boundary Cases

- [x] Owned cell next to neutral cell.
- [x] Owned cell next to enemy-owned cell.
- [x] Owned cell next to blocked water.
- [x] City cell inside owned territory.
- [x] Recently neutralized territory after General death.
- [x] Dense multi-player front where several colors meet.

## Implementation Slices

### Slice 1: Boundary Diagnostics

- [x] Add a core helper or automation response that returns territory boundary segments.
- [x] Include `FromX`, `FromY`, `ToX`, `ToY`, `OwnerId`, and `NeighborOwnerId`.
- [x] Keep segment generation deterministic and sorted for tests.
- [x] Unit test that no boundary is emitted between cells owned by the same player.
- [x] Unit test that a boundary is emitted between different owners.

### Slice 2: Map Rendering

- [x] Render boundary segments on `MapCanvas`.
- [x] Use player colors with restrained opacity.
- [x] Keep terrain patches visible below boundaries.
- [x] Keep city squares and unit circles visible above boundaries.
- [x] Avoid text labels or explanatory UI.

### Slice 3: Brinell Coverage

- [x] Add `GetTerritoryBoundaries` or extend `GetMapState`.
- [x] Add a Brinell test proving boundary diagnostics exist after match start.
- [x] Add a Brinell test proving boundary element count changes after territory changes.
- [x] Add screenshot smoke coverage to catch invisible or overdrawn boundaries.

### Slice 4: Docs

- [x] Update `.my/HowToPlay.md` to explain that colored borders show controlled land.
- [x] Update `.my/UAT.md` with territory boundary checks.

## Test Plan

- [x] Core unit tests for boundary segment generation.
- [x] Core determinism test for boundary ordering.
- [x] Brinell UI test for visible boundary elements.
- [x] Brinell UI test for no city labels or side city list regression.
- [x] Brinell screenshot smoke test for boundaries not blacking out the map.

## Open Questions

- [x] Should boundaries use the owner color on both sides or a neutral dark outline?
  Answer: use one thin owner-color line without a dark outline.
- [ ] Should human territory boundaries be emphasized?
- [x] Should neutral territory boundaries be shown, or only player-versus-player borders?
  Answer: show borders between player territory and neutral land when adjacent.
- [x] Should boundaries appear immediately at match start or only after first capture?
  Answer: show immediately because home territory is already controlled.
- [x] Should AI-only mode use the same boundary style as human mode?
  Answer: yes, use the same style in all modes.
