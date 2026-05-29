# Death Visualization Simplification Roadmap

Status: implemented.

Related code:

- `src/WairOfDots.Windows/MapSpriteRenderer.cs`
- `src/WairOfDots.Windows/MapVisualDiagnostics.cs`
- `tests/WairOfDots.UITests/MenuAndGameplayTests.cs`
- `tests/WairOfDots.UITests/UiSmokeTests.cs`

## Goal

Simplify killed-unit map visualization in small, reversible steps.

Death events should stay available in simulation snapshots for diagnostics, replays, and tests. This roadmap only changes the player-facing tactical map marker.

The same visual simplification applies to every killed unit kind:

- Infantry
- Tank
- Commander
- General

Commander and General deaths may still have gameplay consequences, but they should not get extra map death effects in this roadmap.

## Current Baseline

- Recent deaths are stored in `MatchSnapshot.RecentDeaths`.
- The map does not render recent death markers.
- Recent deaths remain visible in snapshots and diagnostics with marker kind `Hidden`.
- Death rendering avoids command numbers, health arcs, morale arcs, rings, labels, crosses, and burst-style effects.

## Rules

- Do not add animation complexity while simplifying.
- Do not reintroduce arcs, numbers, labels, or burst shapes.
- Keep marker color tied to the defeated unit's player.
- Use the same marker language for line units, Commanders, and Generals.
- Do not add special leader death rings, pulses, labels, or numbers.
- Keep `RecentDeaths` snapshot data even if the marker is eventually hidden.
- Keep each step independently testable.

## Proposed Steps

### Step 1: Ring Plus Circle

- [ ] Replace the current circle-plus-cross marker with a ring plus a small filled circle.
- [ ] Use the defeated player's color for both ring and circle.
- [ ] Remove the cross texture from death rendering.
- [ ] Keep fatal-hit flash suppression when a matching death marker exists.
- [ ] Update diagnostics marker kind to `DeathRingCircle`.
- [ ] Verify infantry, tank, Commander, and General deaths use the same marker style.
- [ ] Update screenshot smoke sampling to look for player-colored death pixels.

Expected result:

- Death is visible but quieter than the current crossed marker.

Test:

- Focused death visual diagnostics test.
- Screenshot smoke for recent death marker visibility.

### Step 2: Ring Only

- [ ] Remove the filled center circle.
- [ ] Render only a player-colored ring at the death cell.
- [ ] Update diagnostics marker kind to `DeathRing`.
- [ ] Prefer one shared marker size for infantry, tanks, Commanders, and Generals.

Expected result:

- Death is minimally visible without competing with live units.

Test:

- Focused death visual diagnostics test.
- Screenshot smoke checks for player-colored ring pixels near recent death marker.

### Step 3: Nothing

- [x] Stop drawing death markers on the tactical map.
- [x] Keep `RecentDeaths` in snapshots.
- [x] Expose recent deaths in map visual diagnostics with marker kind `Hidden`.
- [x] Remove screenshot smoke expectations for visible death pixels.
- [x] Keep core tests for recent death snapshot history.

Expected result:

- Death has no direct map visualization.
- Diagnostics and replay data still know that a unit died.
- Commander and General deaths are still represented through game state, standings, elimination, and snapshot history rather than a special map marker.

Test:

- Core recent death snapshot test remains.
- UI test asserts no visible death marker is expected.

## Done Criteria

- Each step can be implemented and tested separately.
- The map never shows death arcs, numbers, labels, or burst effects.
- Simulation death history remains deterministic and capped.
- The player-facing death visualization can be reduced all the way to no marker without losing snapshot data.
