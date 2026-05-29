# Map Interaction Refactor Roadmap

Status: implemented on 2026-05-25.

## Goal

Replace per-city Stride UI `Button` hit targets with a real tactical map interaction layer that can handle cities, units, cells, overlays, and future selection tools without turning the playfield back into UI controls.

Also remove old pre-`MapSpriteRenderer` map drawing code so the rendering and interaction architecture stays easy to reason about.

## Implementation Summary

- Added `MapInteractionService`, `MapHitResult`, `MapInteractionTarget`, and shared `MapCoordinateSystem`.
- Routed real mouse clicks and Brinell `ClickMap` commands through the same map hit-test path.
- Added unit, city, and cell hit testing with deterministic priority: units, then cities, then cells.
- Removed per-city `MapCity_{id}` UI buttons and old UI map drawing helpers from `WairOfDotsGame`.
- Added selected-unit state, HUD details, and a sprite-rendered selected-unit highlight.
- Migrated Brinell tests away from `MapCity_{id}.Click()` and added coverage for unit, city, and cell hits.
- Kept `SelectTargetCity` automation for deterministic setup.
- Deferred separate core-only coordinate tests because the implemented service lives in the Stride host and is covered through Brinell automation for this slice.
- Verified with `dotnet test tests\WairOfDots.UITests\WairOfDots.UITests.csproj` after implementation.

## Problem

The map currently uses `MapSpriteRenderer` for the visible playfield, but city clicks still depend on transparent Stride UI `Button`s named `MapCity_{id}`. That was acceptable while only cities were clickable, but it does not scale well.

Future gameplay needs clickable units, selected-unit HUD details, city targeting, terrain/cell inspection, Commander/General overlays, and maybe drag/box tools. Creating a UI button for every clickable gameplay object would rebuild the same old problem: tactical playfield objects become UI elements again, ordering gets fragile, and interaction logic becomes tied to visual element names.

There is also old UI-map drawing code still present in `WairOfDotsGame`, such as terrain, edge, city, territory, and unit marker helpers. Those methods are not the current map rendering path and make future work easier to accidentally implement in the wrong layer.

## Implemented State

- [x] `MapSpriteRenderer` draws terrain, territory boundaries, cities, units, engagement rings, and legend.
- [x] `MapCanvas` exists as a Stride UI surface beside the HUD.
- [x] `MapCanvas` no longer gets one transparent city `Button` per city.
- [x] City hits route to the same target-city behavior previously reached by city buttons.
- [x] Brinell tests use `ClickMap`, `HitTestMap`, and interaction targets instead of `MapCity_{id}` buttons.
- [x] Automation can already select cities through `SelectTargetCity`.
- [x] Units are clickable through the map.
- [x] Cells are hit-testable through the map.
- [x] Centralized hit-test priority exists for units, cities, and cells.

## Design Direction

- [x] Keep `MapSpriteRenderer` as the only playfield visual renderer.
- [x] Keep Stride UI for HUD, menus, panels, toggles, and text.
- [x] Keep `MapCanvas` or a single named map interaction surface for layout, focus, and Brinell discoverability.
- [x] Do not create one UI control per city, unit, cell, or overlay marker.
- [x] Add a map interaction service that converts screen/map coordinates into gameplay hits.
- [x] Use deterministic hit-test ordering so tests and player clicks behave the same every run.
- [x] Preserve automation coverage with map queries and commands rather than per-object UI element names.
- [x] Remove unused UI drawing helpers once the new interaction layer is covered.

## Target Architecture

### Map Render Layer

- [x] `MapSpriteRenderer` draws visible map state.
- [x] Renderer uses a shared viewport helper for map coordinate conversion.
- [x] Renderer does not handle gameplay commands.
- [x] Renderer coordinate conversion stays pure and deterministic through `MapCoordinateSystem`.

### Map Interaction Layer

- [x] New `MapInteractionService` or equivalent host helper owns hit testing.
- [x] Input is screen/canvas coordinates for real clicks and normalized map coordinates for automation.
- [x] Output is a `MapHitResult` such as `Unit`, `City`, `Cell`, or `None`.
- [x] Hit results include stable ids, map cell, distance, and hit priority.
- [x] Hit testing uses current snapshot/simulation state, not UI element tree scanning.
- [x] Hit-test priority defaults to: units, cities, passable cells, terrain/background.
- [x] Unit hit radius uses rendered unit size and leader ring slack.
- [x] City hit radius uses rendered city square size.
- [x] Cell hit resolves to grid cell for terrain/economy/visibility inspection.

### Command Routing

- [x] Human General mode: city hit sets target city.
- [ ] Human Commander mode: city or region hit sets region/anchor target. Deferred until commander-region interaction is implemented.
- [x] Unit inspection: unit hit selects a unit and updates HUD details.
- [ ] Future Dot/chaos mode: unit or cell hit can issue direct local orders. Deferred until Dot/chaos mode exists.
- [x] AI-only mode: clicks inspect units/cities instead of issuing human orders.
- [x] Background/cell hit can clear selection or inspect terrain, depending on active tool.

### Automation

- [x] Keep `GetMapState`.
- [x] Add `HitTestMap` automation command that accepts normalized map coordinates.
- [x] Add `ClickMap` automation command that routes through the same hit-test path as real input.
- [x] Keep `SelectTargetCity` as a direct command for deterministic setup.
- [ ] Add `SelectUnit` or `InspectUnit` direct command when selected-unit HUD exists. Deferred because `ClickMap` now covers real unit selection.
- [x] Replace Brinell `MapCity_{id}.Click()` usage with `ClickMap` or direct `SelectTargetCity`, depending on what the test is proving.
- [x] Keep one stable visible map surface element named `MapCanvas` for screenshot and layout tests.

## Implementation Slices

### Slice 1: Inventory And Safety Net

- [x] Identify every old map UI drawing method in `WairOfDotsGame`.
- [x] Confirm which helpers are unused by production code.
- [x] Confirm current Brinell tests that depend on `MapCity_{id}`.
- [x] Add or update a test that proves city selection can happen through automation without relying on a city button.
- [x] Document the expected replacement behavior before deleting old code.

Known old-code candidates:

- [x] `AddTerrainPatches`
- [x] `AddMapLegend`
- [x] `AddTerritoryBoundaries`
- [x] `AddTerritoryBoundary`
- [x] `AddEdges`
- [x] `AddCityMarkers`
- [x] `AddUnitMarkers`
- [x] `AddCircleMarker`
- [x] any old marker offset helpers used only by UI-map drawing

### Slice 2: Shared Map Coordinate Model

- [x] Extract map viewport and coordinate conversion into a small shared helper.
- [x] Ensure renderer and hit testing use the same map-to-screen math.
- [ ] Add tests for map point to normalized viewport conversion. Deferred; covered indirectly by Brinell hit tests in this slice.
- [ ] Add tests for grid cell hit conversion near map edges. Deferred; covered indirectly by Brinell hit tests in this slice.
- [x] Keep map constants in one place to prevent visual and interaction drift.

### Slice 3: Hit-Test Service

- [x] Add `MapHitResult` records for unit, city, cell, and none.
- [x] Implement city hit testing.
- [x] Implement unit hit testing.
- [x] Implement cell hit testing.
- [x] Use deterministic sorting for overlapping hits.
- [x] Prefer unit hits over city hits when a unit is standing on or near a city.
- [ ] Add core or host-level tests for hit priority and edge cases. Deferred; Brinell coverage exercises the host path.

### Slice 4: Real Map Click Routing

- [x] Route map surface pointer/click events into the hit-test service.
- [x] City hit calls existing `SelectTargetCity`.
- [x] Unit hit stores selected unit id and updates selected-unit HUD placeholder.
- [x] Cell hit stores inspected cell or clears selection.
- [x] AI-only mode uses clicks for inspection only.
- [x] Preserve current human city-target behavior.

### Slice 5: Automation Migration

- [x] Add `HitTestMap` command.
- [x] Add `ClickMap` command.
- [x] Add helper methods in `GameQueryHelpers`.
- [x] Replace `game.MapCity(3).Click()` in UI tests.
- [x] Replace tests that count `MapCity_` element names with map-state or hit-test diagnostics.
- [x] Keep `MapCanvas` screenshot and visibility assertions.
- [x] Remove `MapCity(int cityId)` page-object helper after tests no longer use it.

### Slice 6: Remove Per-City UI Buttons

- [x] Delete `EnsureMapHitTargets`.
- [x] Delete `_mapHitTargetsReady` if no longer needed.
- [x] Stop adding `MapCity_{id}` buttons to `MapCanvas`.
- [x] Ensure city selection still works through real map clicks.
- [x] Ensure Brinell can still select a city through `ClickMap` or `SelectTargetCity`.
- [x] Verify no tactical object is represented by a Stride UI child.

### Slice 7: Remove Old UI Drawing Code

- [x] Delete unused UI drawing helpers.
- [x] Delete helper constants that only supported UI drawing.
- [x] Delete old marker glyph logic from `WairOfDotsGame`.
- [x] Keep shared color helpers only if still used outside renderer.
- [x] Move duplicate color logic into one place if needed.
- [x] Run compile and focused UI tests after deletion.

### Slice 8: Prepare Unit Selection UI

- [x] Add selected-unit state to `WairOfDotsGame`.
- [x] Add selected-unit HUD surface with unit id, kind, owner, health, and morale.
- [x] Draw selected-unit highlight in `MapSpriteRenderer`.
- [x] Add Brinell query path for selected unit through `ClickMap` and HUD diagnostics.
- [x] Add tests proving a map click can select a unit.

## Test Plan

- [x] Core/host test: normalized coordinate over a city returns that city.
- [x] Core/host test: normalized coordinate over a unit returns that unit.
- [ ] Core/host test: unit over city prioritizes unit for inspection. Deferred to a future dedicated hit-test unit suite.
- [x] Core/host test: empty passable cell returns cell hit.
- [x] Brinell test: city target can be changed through `ClickMap`.
- [x] Brinell test: unit can be selected through `ClickMap`.
- [x] Brinell test: `MapCanvas` remains visible and sized for screenshot tests.
- [x] Brinell test: no `MapCity_` UI elements are required after migration.
- [x] Screenshot smoke: sprite-rendered cities and units remain visible after UI button removal.
- [ ] Run `dotnet test tests/WairOfDots.Tests/WairOfDots.Tests.csproj` for shared coordinate/hit-test rules. Deferred; no core project changes in this slice.
- [x] Run focused Brinell tests for map selection and screenshots.

## Migration Notes

- [x] Do not remove city button tests until replacement `ClickMap` coverage exists.
- [x] Do not remove `SelectTargetCity` automation; it is still valuable for deterministic test setup.
- [x] Do not make `MapSpriteRenderer` depend on Brinell or automation types.
- [x] Do not move gameplay selection state into renderer.
- [x] Do not rebuild per-unit or per-cell UI elements as a replacement.

## Acceptance Criteria

- [x] The player can click a city without a per-city UI button.
- [x] The player can click a unit and get a stable selected unit.
- [x] The same hit-test path supports real input and Brinell `ClickMap`.
- [x] `MapCanvas` no longer contains one UI child per city.
- [x] Tactical visuals remain sprite-rendered.
- [x] Old UI map drawing helpers are removed.
- [x] Existing city targeting behavior is preserved.
- [x] Future gameplay visualization work can add clickable units, cells, and overlays without adding object-specific UI controls.

## Open Questions

- [x] Should map hit-test inputs use normalized map coordinates or screen pixels in automation?
  Proposed answer: use normalized map coordinates for tests, then let the host translate real pointer pixels into the same normalized coordinate system.
- [x] Should a unit standing on a city select the unit or the city?
  Proposed answer: select the unit first; provide city selection through a modifier, target mode, or nearby HUD action later.
- [x] Should AI-only map clicks inspect or retarget AI plans?
  Proposed answer: inspect only. AI-only mode is a spectator/debug surface, not a command mode.
- [x] Should old `MapCity_{id}` names remain as virtual automation elements?
  Proposed answer: no. Prefer `ClickMap`, `HitTestMap`, and direct commands so automation follows the real architecture.
