# Wair of Dots UAT

Use this checklist for player acceptance testing.

## Setup

- [ ] Build the solution successfully.
- [ ] Launch `src/WairOfDots.Windows/bin/Debug/net10.0-windows/WairOfDots.Windows.exe`.
- [ ] Confirm the main menu appears.

## Main Menu

- [ ] The title says `Wair of Dots`.
- [ ] The seed field defaults to `1337`.
- [ ] The AI player count defaults to `4`.
- [ ] The `Human` checkbox defaults to checked.
- [ ] The `Role` control is visible and defaults to `General`.
- [ ] `Settings` opens a settings panel.
- [ ] `Back` returns to the main menu.
- [ ] `Start Match` starts the game.

## Match HUD

- [ ] The play screen shows a large tactical map.
- [ ] The map has multiple terrain types: land, water, road, hill/rock, and forest.
- [ ] Thin colored territory borders are visible above terrain and below cities/units.
- [ ] Cities appear as large clickable markers colored by their controlling player.
- [ ] Neutral city square markers are visually distinct from player-owned city square markers.
- [ ] City number labels are not shown on the map.
- [ ] The command-panel city list is not shown.
- [ ] Units, commanders, and generals appear as circle markers.
- [ ] No grid cell visibly holds more than one unit marker.
- [ ] At most one unit marker appears on a city square.
- [ ] Moving units travel across the map grid rather than along city-to-city edges.
- [ ] Moving unit markers glide between adjacent grid cells instead of snapping.
- [ ] Moving units can travel diagonally across open grid corners.
- [ ] Units do not move diagonally through blocked water/terrain corners or occupied unit corners.
- [ ] Units crossing open terrain follow smooth straight lines rather than zigzagging cell by cell.
- [ ] Smoothed movement does not cause units to visually pass through water, obstacles, or other units.
- [ ] No Oravey/debug camera instruction overlay appears over the map or HUD.
- [ ] The HUD shows tick, phase, seed, AI count, score, treasury, tax, upkeep, general health, directive, target, role, last event, and AI telemetry.
- [ ] Map city square markers are visible and clickable.

## Territory And Economy

- [ ] Infantry capture only the grid cell they occupy.
- [ ] Tanks capture the grid cell they occupy and adjacent passable uncontested cells.
- [ ] Water cells do not become owned territory.
- [ ] Controlled cells contribute tax income.
- [ ] Territory boundaries appear between cells with different owners.
- [ ] Territory boundaries change as units capture more cells.
- [ ] Territory boundaries do not hide city squares or unit circles.
- [ ] The treasury changes after economy ticks.
- [ ] Existing units create upkeep pressure.
- [ ] If payroll cannot be paid, unit morale decreases rather than units instantly disappearing.
- [ ] New units spawn near owned cities when adjacent passable cells are available.
- [ ] If city-adjacent spawn cells are blocked, spawning waits.

## Player Commands

- [ ] Select a city marker on the map and confirm the target text changes.
- [ ] Select `Attack` and confirm the directive changes to Attack.
- [ ] Select `Hold` and confirm the directive changes to Hold.
- [ ] Select `Defend` and confirm the directive changes to Defend.
- [ ] Select `More Infantry` and confirm the Infantry percentage increases.
- [ ] Select `More Tanks` and confirm the Infantry percentage decreases.
- [ ] Start a match in `Commander` role and confirm the HUD role text says `Commander`.

## Simulation

- [ ] Let the match run for at least 30 seconds.
- [ ] Confirm ticks advance.
- [ ] Confirm at least one moving unit marker changes grid position over time.
- [ ] Confirm combat, city capture, and unit blocking still use grid cells while markers animate smoothly.
- [ ] Confirm diagonal enemy neighbors can fight without stacking.
- [ ] Confirm city ownership changes over time.
- [ ] Confirm that when a General dies, all cities and controlled cells owned by that player become neutral.
- [ ] Confirm AI telemetry appears after the first planning interval.
- [ ] Confirm combat/capture events appear in the last event text.
- [ ] Confirm adjacent enemy units can fight without stacking onto the same cell.
- [ ] Confirm telemetry can report AI plans, payroll deficits, tax raids, territory swings, and decapitation attempts during longer AI-only runs.

## AI-Only Mode

- [ ] Return to the main menu.
- [ ] Uncheck `Human`.
- [ ] Start a match with seed `2026` and 4 AI players.
- [ ] Confirm the HUD status includes `AiOnly`.
- [ ] Confirm no human command buttons are visible.
- [ ] Confirm spectator speed controls are visible.
- [ ] Confirm standings are visible and list AI players as horizontal metric bars.
- [ ] Confirm standings have metric labels above the columns for score, cities, units, and general health.
- [ ] Confirm each standings row starts with a player-colored dot and does not show AI names or `in`/`out` text.
- [ ] Confirm Brinell diagnostics expose cell controls, economies, regions, visibility, telemetry, and deterministic training smoke results.
- [ ] Confirm Brinell diagnostics expose territory boundary segments.
- [ ] Confirm city markers inspect cities without changing human orders.
- [ ] Let the match run until the first planning interval and confirm AI telemetry appears.
- [ ] Restart and confirm the match stays AI-only with the same seed.

## Pause And Restart

- [ ] Select `Pause` and confirm the phase changes to Paused.
- [ ] Wait five seconds and confirm the tick does not advance.
- [ ] Select `Resume` and confirm ticks advance again.
- [ ] Select `Restart` and confirm the tick resets to 0 with the same seed.

## Determinism

- [ ] Start seed `2026` with 4 AI players.
- [ ] Let the match run for 60 seconds and note the broad city ownership pattern.
- [ ] Restart seed `2026` with 4 AI players.
- [ ] Confirm the same early match pattern repeats.

## End State

- [ ] Let a short match run to completion or use automation to step ticks.
- [ ] Confirm the end-state panel appears.
- [ ] Confirm the end-state panel states victory or defeat.
- [ ] Confirm `Restart` starts a new match.
- [ ] Confirm `Menu` returns to the main menu.

## Acceptance

- [ ] The game is understandable without developer help.
- [ ] The human can make meaningful strategic choices.
- [ ] AI players behave differently but reproducibly.
- [ ] Brinell UI tests pass.
- [ ] Unit tests pass.
