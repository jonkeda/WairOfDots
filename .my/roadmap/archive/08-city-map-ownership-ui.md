# City Map Ownership UI Roadmap

Status: implemented and verified.

## Goal

Remove the command-panel city list and make city ownership readable directly on the map by coloring each city marker with the controlling player's color. Remove city number labels from the map.

## Problem

The side-panel city list repeats information that should be visible on the tactical map. It takes up HUD space, makes the command panel noisy, and encourages players to read a list instead of scanning the battlefield.

The map currently shows city numbers next to city markers, but those numbers add clutter once city targeting and ownership can be read directly from colored city markers.

## Definitions

- `City list` means the command-panel list of city buttons such as `0: Crown | AI 1 | U 0`.
- `Map city marker` means the large clickable square marker rendered on the tactical map.
- `Owner color` means the same player color used for units and standings.
- `Neutral color` means the existing neutral/yellow city color.
- `Selected city` means the city currently targeted by the human player or inspected by the AI-only spectator.

## Proposed Design Decisions

- [x] Remove the command-panel city list.
- [ ] Keep the list and only recolor map cities.
- [ ] Collapse the list behind a debug toggle.

- [x] Keep map city markers clickable for targeting and spectator inspection.
- [ ] Remove city click interactions entirely.

- [x] Color each map city marker by current owner.
- [ ] Color only captured cities and leave neutral cities unstyled.
- [ ] Use unit count instead of owner for city color.

- [x] Remove numeric city labels from the map.
- [ ] Keep city numbers on hover only.
- [ ] Replace city numbers with city names.

- [x] Keep selected city details in the HUD target text.
- [ ] Add a new selected-city detail panel.
- [ ] Depend on city list text for selected-city details.

- [x] Verify with Brinell game queries and UI visibility checks.
- [ ] Verify only through manual screenshots.

## Slice 1: UI Removal

- [x] Remove `CityListPanel` from the command panel layout.
- [x] Remove `RefreshCityButtons` and its call path if no longer needed.
- [x] Remove page-object accessors and tests that depend on `CityButton_*`.
- [x] Keep the command panel focused on status, score, target, telemetry, controls, and standings/spectator controls.
- [x] Ensure human mode still has a clear way to select targets through map city markers.

## Slice 2: Map City Marker Ownership Color

- [x] Ensure `MapCityFill_*` markers use `PlayerColor(city.OwnerId)` for the current owner.
- [x] Keep neutral city markers visually distinct.
- [x] Ensure ownership color updates after captures without needing a restart.
- [x] Ensure selected target state remains readable without city numbers.
- [x] Consider adding a subtle selected-city ring if target readability drops after removing labels.
- [x] Use filled square city markers so cities remain visible without number labels or dark backing blocks.

## Slice 3: Remove Map City Numbers

- [x] Remove `MapCityLabel_*` elements from map rendering.
- [x] Keep map city marker names stable for Brinell.
- [x] Ensure city markers remain large enough to click without numeric labels.
- [x] Ensure no orphaned layout space or overlapping text remains.

## Slice 4: Automation And Diagnostics

- [x] Extend or reuse map-state diagnostics to report city owner ids and city marker counts.
- [x] Add diagnostics proving no map city number labels are rendered.
- [x] Add diagnostics proving the command-panel city list is absent or hidden.
- [x] Keep `SelectTarget` game query for deterministic tests even after removing city-list buttons.
- [x] Keep `MapCity_*` automation IDs stable.

## Slice 5: Test Coverage

- [x] Brinell UI test: the city list is not visible on the HUD.
- [x] Brinell UI test: map city markers are visible and clickable.
- [x] Brinell UI test: clicking a map city changes the selected target.
- [x] Brinell UI test: no `MapCityLabel_*` number labels are visible.
- [x] Brinell gameplay test: city owner colors/diagnostics update after capture.
- [x] Unit test or query test: map state exposes city ownership for UI assertions.
- [x] Run build, unit tests, and Brinell UI tests.

## Slice 6: Docs

- [x] Update `.my/HowToPlay.md` so city targeting uses map city markers instead of side-list city buttons.
- [x] Update `.my/UAT.md` with checks for owner-colored city markers and no city-number labels.
- [x] Update `.my/DevPrompts/Architecture.md` to describe map-first city interaction.
- [x] Mark this roadmap complete after implementation and verification.

## Open Questions

- [x] Should the city list be removed entirely?
- [ ] Should it be hidden behind a debug toggle?
  Proposed answer: remove it from the player HUD entirely. Debug/automation can use game queries instead.

- [x] Should map city markers remain clickable?
- [ ] Should targeting move to another control?
  Proposed answer: keep map city markers clickable. The map becomes the city interaction surface.

- [x] Should city numbers be removed from the map?
- [ ] Should numbers stay for targeting precision?
  Proposed answer: remove the numbers. Stable automation IDs and map-click targeting cover precision, while the player gets a cleaner map.

- [x] Should selected city information stay in the target text?
- [ ] Should a new selected-city detail panel be added now?
  Proposed answer: keep selected city information in the existing target text first. Add a selected-city detail panel later only if playtesting needs it.
