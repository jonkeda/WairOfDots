# Stride Map Rendering Stability Roadmap

Status: implemented.

## Decision

- [x] Recommended answer: do not use Stride UI as the primary renderer for the game map.
- [x] Keep Stride UI for menus, HUD, buttons, text, standings, settings, and Brinell automation affordances.
- [x] Move terrain, territory boundaries, cities, and units to a dedicated playfield renderer with explicit layers.

## Why

The current map is built as many `UIElement` children on `MapCanvas`. Every frame, `Update` calls `RefreshUi`, then `RefreshMap` clears the canvas and recreates terrain patches, boundary lines, city buttons, and unit text markers. That means the game map is constantly invalidating UI layout and replacing visual children while Stride is measuring, arranging, and batching UI draw calls.

This is a poor fit for a frequently changing tactical map. It explains symptoms like land, lines, and units blinking, drawing over each other, and sometimes not appearing until explicit width/height constraints were added.

## Stride Research Notes

- Stride UI is a layout system. Every `UIElement` has a layout rectangle, and Stride computes layout recursively from element size requirements, constraints, margins, padding, and parent panel rules.
- Stride UI performs a measure pass and an arrange pass before elements have final positions and sizes.
- Stride UI batches multiple UI elements through a sprite batch renderer to reduce draw calls.
- `Canvas` can position children by relative or absolute coordinates and can size children relative to the parent canvas.
- Draw order inside a panel should be controlled explicitly with panel `ZIndex`; relying on insertion order while clearing and recreating children every frame is brittle.
- `SpriteBatch` is Stride's low-level API for efficiently grouping and drawing sprite-like visuals. That is closer to what the map needs than a large pile of UI controls.

Sources:

- https://doc.stride3d.net/4.2/en/manual/ui/layout-system.html
- https://doc.stride3d.net/latest/en/manual/ui/index.html
- https://doc.stride3d.net/latest/en/api/Stride.UI.Panels.Canvas.html
- https://doc.stride3d.net/4.0/en/api/Stride.UI.UIElementExtensions.html
- https://doc.stride3d.net/latest/en/manual/graphics/low-level-api/spritebatch.html

## Current Risk Areas

- [x] `WairOfDotsGame.Update` calls `RefreshUi` every frame.
- [x] `RefreshMap` calls `_mapCanvas.Children.Clear()` every frame.
- [x] Terrain patches and territory boundaries are empty `StackPanel` controls pretending to be drawing primitives.
- [x] Units are `TextBlock` glyphs, cities are `Button` controls, and land/line primitives are panels, so interaction controls and visual primitives are mixed in the same canvas.
- [x] Layering is implied by add order instead of explicit `SetPanelZIndex` or a renderer-owned draw order.
- [x] Relative canvas sizes are mixed with min/max pixel sizes to force rendering.
- [ ] Brinell screenshot capture currently has a window offset issue, so visual tests need map-surface detection until capture is fixed.

## Target Architecture

- [x] Introduce a dedicated `MapRenderSystem` or `MapRenderer` responsible only for playfield visuals.
- [x] Keep `MapCanvas` only as an interaction/HUD overlay, or remove it from the playfield entirely.
- [x] Render fixed layers in one deterministic order:
  - terrain base
  - roads/water/hills/forest/rocks
  - territory boundaries
  - cities
  - unit markers
  - selection/target highlights
- [x] Use one coordinate conversion service for map/world/screen positions.
- [x] Update renderer data from `MatchSnapshot` without rebuilding UI controls.
- [x] Use explicit layer constants, not child insertion order.

## Implementation Slices

### Slice 1: Stabilize Existing UI Map

- [ ] Stop refreshing the whole UI every frame.
- [ ] Split text/HUD refresh from map refresh.
- [ ] Only refresh terrain when map data changes.
- [ ] Only refresh territory boundaries when territory changes.
- [ ] Only refresh unit positions every frame.
- [ ] Add explicit `SetPanelZIndex` to all existing map UI children as a temporary guard.
- [ ] Add diagnostics for map child count by layer.

### Slice 2: Renderer Spike

- [x] Build a small Stride renderer spike that draws terrain rectangles, boundary lines, cities, and unit dots without UI controls.
- [ ] Compare `SpriteBatch` versus simple mesh/primitive rendering for colored rectangles and lines.
- [x] Keep Brinell map-state queries independent from the visual implementation.
- [x] Capture screenshots before and after the spike.

### Slice 3: Replace Playfield Rendering

- [x] Move terrain rendering out of `MapCanvas`.
- [x] Move territory boundary rendering out of `MapCanvas`.
- [x] Move units out of `TextBlock` glyph rendering.
- [x] Keep city click targets as invisible or minimal UI overlays only if needed for mouse interaction.
- [x] Make draw order deterministic in renderer code.

### Slice 4: Brinell and UAT

- [x] Add a focused screenshot test that asserts terrain, boundaries, cities, and units are all visible in the same frame.
- [ ] Add a flicker smoke test that captures several consecutive frames and fails if map pixel coverage changes too much without a simulation reason.
- [ ] Add a layer-order smoke test: units must appear above boundaries, boundaries above terrain.
- [ ] Update `.my/UAT.md` with a map stability section.
- [ ] Keep the existing screenshot crop/map-surface detection until Brinell screenshot offset is fixed.

## Proposed Answers

- [x] Use Stride UI for HUD and controls, not the playfield.
- [ ] Keep everything in Stride UI and only patch `ZIndex`.
- [ ] Use a hybrid forever, with UI terrain/lines and UI units.

Best answer: use Stride UI for HUD and controls, and a dedicated renderer for the playfield.

## Done When

- [x] Map terrain no longer blinks.
- [x] Territory boundaries no longer disappear or draw over units.
- [x] Units do not flicker or vanish during movement.
- [x] UI buttons and HUD still work.
- [x] Brinell screenshot smoke test passes.
