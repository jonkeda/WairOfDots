# Standings Horizontal Bars Roadmap

Status: implemented and verified.

## Goal

Replace the text-heavy standings list with multiple compact horizontal bar rows, one row per player, while preserving the current standings order.

## Problem

The current standings panel shows dense text such as score, city count, unit count, general health, and status in a single line per player. It works for automation and debugging, but it is harder to scan during a match than a visual comparison.

The player should be able to glance at the standings and quickly compare who is ahead, who has cities, who has units, and whose general is weak.

## Definitions

- `Standing row` means one player's full horizontal standings entry.
- `Metric bar` means a labeled horizontal fill bar for a single metric.
- `Player color` means the same color used for that player's units, city squares, and existing standings text.
- `Current order` means the standings order returned by the game snapshot and `GetStandings` query. The UI must not re-sort it.

## Proposed Design Decisions

- [X] Keep the current standings order.
- [ ] Re-sort by score after the visual redesign.
- [ ] Re-sort by active players first.
- [X] Render each player as one horizontal row with side-by-side metric bars so the same metric can be compared across players.
- [ ] Render one stacked bar per player.
- [ ] Render a chart separate from the player labels.
- [X] Scale each metric consistently across rows: score and units against the current largest visible value, cities against total cities, and general health against max health.
- [ ] Give every row independent bar scales.
- [X] Keep score, cities, units, and general health visible.
- [X] Remove repeated player name/status text from each row and use a player-color dot instead.
- [ ] Show only score and general health.
- [ ] Hide raw numbers and show bars only.
- [X] Use player color for the primary bar fill and label.
- [ ] Use a different color per metric.
- [ ] Use neutral UI colors and show player color only as a small swatch.
- [X] Keep eliminated players visible but muted.
- [ ] Hide eliminated players.
- [ ] Move eliminated players to a separate collapsed section.

## Slice 1: UI Layout

- [x] Replace each standings text line with a compact row container.
- [x] Replace the player label area with a player-color dot.
- [x] Add horizontal bars for score, city count, unit count, and general health.
- [x] Add header labels above the metric columns.
- [x] Keep raw values near or inside the bars when they remain readable.
- [x] Ensure rows fit inside the current command panel width.
- [x] Ensure no row text overlaps or clips at the current game resolution.

## Slice 2: Bar Scaling

- [x] Scale score bars relative to the highest visible score.
- [x] Scale city bars relative to total city count.
- [x] Scale unit bars relative to the highest visible unit count.
- [x] Scale general health bars relative to max general health.
- [x] Handle zero or negative scores without breaking layout.
- [x] Keep eliminated/out rows readable even when values are zero.

## Slice 3: Visual States

- [x] Use player color for active player bar fills.
- [x] Use lower opacity or gray text for eliminated metric values and bars.
- [x] Keep player-color dots fully colored, even for eliminated players.
- [x] Remove `in`/`out` status text from the standings rows.
- [x] Make AI-only spectator standings easy to scan without human command controls.
- [x] Keep human-vs-AI mode focused on the command panel without crowding target or telemetry text.

## Slice 4: Automation And Diagnostics

- [x] Keep standings automation element names stable enough for Brinell tests.
- [x] Add named elements for each row, such as `StandingRow_{playerId}`.
- [x] Add named elements for metric bars, such as `StandingScoreBar_{playerId}` and `StandingGeneralBar_{playerId}`.
- [x] Add named elements for player dots and metric header labels.
- [x] Reuse `GetStandings` game query for deterministic assertions.
- [x] Use existing visible-element diagnostics; no new diagnostics were needed.

## Slice 5: Test Coverage

- [x] Brinell UI test: standings render one row per player.
- [x] Brinell UI test: each row contains score, cities, units, and general health bar elements.
- [x] Brinell UI test: standings order preserves the snapshot order without UI sorting.
- [x] Brinell UI test: rows include player-color dot elements and do not show AI names or `in`/`out` text.
- [x] No unit test needed because bar scaling stayed inside the UI rendering layer.
- [x] Run the smallest relevant Brinell UI tests after implementation.

## Slice 6: Docs

- [x] Update `.my/HowToPlay.md` to describe standings bars.
- [x] Update `.my/UAT.md` with a checklist item for standings bar readability.
- [x] Update `.my/DevPrompts/Architecture.md` if the standings UI introduces reusable UI helpers.
- [x] Mark this roadmap complete after implementation and verification.

## Open Questions

- [X] Should the standings order change?
- [ ] Should the standings be re-sorted as part of the redesign?
  Proposed answer: keep the snapshot order exactly. The request specifically says no order change is needed, so the UI should not sort standings.
- [X] Should raw numbers stay visible?
- [ ] Should the bars replace the numbers entirely?
  Proposed answer: keep raw numbers visible in compact labels. Bars help scanning, while numbers keep debugging and UAT precise.
- [X] Should eliminated players stay in the list?
- [ ] Should eliminated players be hidden?
  Proposed answer: keep eliminated players visible with muted metric values/bars, but keep the player-color dot vivid and remove `out` text from the row.
- [X] Should each metric get its own horizontal bar?
- [ ] Should each player have one combined stacked bar?
  Proposed answer: use multiple short, side-by-side bars per row. Each metric should use one shared scale across all players so comparisons are easy: score and units use the current largest visible value, cities use total city count, and general health uses max health.
