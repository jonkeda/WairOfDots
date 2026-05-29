# Gameplay Visualization Roadmap

Status: partially implemented. Slices 1, 2, 3, and 4 were implemented on 2026-05-25.

## Implementation Notes

- Added Brinell `GetMapVisualDiagnostics` coverage for unit health ratio, morale band, visual state, role flags, current target, selected state, and recent telemetry event markers.
- Added deterministic visual marker ordering by unit id and recent event ordering by tick, player, event type, and label.
- Added a narrow Brinell `SetUnitMorale` diagnostic command so low-morale and routed visuals can be tested deterministically.
- Added unit health arcs and morale arcs/rings in `MapSpriteRenderer`.
- Added distinct blue rout-risk and routed visual states: segmented blue morale arc for rout risk, stronger blue broken ring for routed.
- Expanded selected-unit HUD details with health/max health, morale percent/band, role flags, target city, and target region.
- Added recent combat-hit and death snapshots, Brinell diagnostics, short-lived impact flashes, leader threat pulses, and compact 4-tick death-burst map markers.
- Capped recent combat/death visual marker history so 8x simulation cannot flood the renderer.
- Centered resting unit markers on their grid cells.
- Added HUD overlay mode controls for Normal, Economy, Command, Visibility, and AI Debug.
- Added Economy overlay tax heat, recent territory swing borders, city spawn pips, and payroll stress rings through `MapSpriteRenderer`.
- Added Brinell overlay diagnostics for tax heat cells, spawn capacity, payroll stress, and recent territory swings.
- Expanded AI-only standings rows with compact treasury, tax, upkeep, and payroll deficit bars.
- Added Brinell tests for overlay toggles, economy diagnostics, standings economy bars, and stable HUD stack ordering.
- Verified with `dotnet test tests\WairOfDots.UITests\WairOfDots.UITests.csproj`.

## Goal

Make the current simulation state readable on the battlefield without turning the map into a spreadsheet.

The player should be able to answer these questions at a glance:

- Which units are healthy, shaken, routing, or protected?
- Where is combat happening?
- Which land is valuable, threatened, or economically stressed?
- What are Commanders and Generals trying to do?
- What can the human player currently see?
- Why is an AI player winning or collapsing?

## Problem

Many important systems already exist in snapshots, telemetry, and tests, but the map only shows a small part of them. Morale, payroll stress, commander regions, visibility, AI intent, protection details, and training fitness are mostly invisible unless a developer reads diagnostics.

This makes the game harder to play and harder to watch in AI-only mode. The systems are there, but the player cannot yet feel their shape.

## Design Principles

- [x] Keep tactical map visuals in `MapSpriteRenderer`.
- [x] Keep Stride UI for HUD, menus, buttons, standings, toggles, and automation targets.
- [x] Use "glance first, detail on selection" as the core rule.
- [x] Keep owner color reserved for player identity.
- [x] Use shape, ring style, line style, icon, or opacity for gameplay state so player colors remain readable.
- [x] Prefer small repeatable visual markers over permanent text labels on the map.
- [ ] Add debug overlays as opt-in spectator/developer layers, not always-on player noise.
- [x] Preserve deterministic rendering order: terrain, territory boundaries, cities, units, selection/highlights, lightweight labels.
- [x] Use Brinell map-state and screenshot tests for anything that can disappear, overlap, or become unreadable.

## Visual Grammar

### Units

- [x] Owner color: main unit fill.
- [x] Unit kind: existing size/shape language stays, with leaders using rings.
- [x] Health: tiny top arc on each unit.
- [x] Morale: lower arc, ring segment, or small base chevron under each unit.
- [x] Selected unit: larger health and morale readout in the HUD.
- [x] Engaged unit: existing light combat ring stays.
- [ ] Protection detail: thin tether or shield pip near the protected leader.
- [ ] Scout: small eye or forward marker, visible only when selected or debug overlay is on.

### Morale

Morale should be visible without needing numbers over every unit.

- [x] Aggressive: bright solid morale arc.
- [x] Normal: neutral solid morale arc.
- [x] Cautious: soft blue arc.
- [x] Rout risk: saturated blue segmented arc.
- [x] Routed: strong blue broken ring plus retreat arrow or away-facing chevron.
- [ ] Rallying: soft pulse near a Commander or General aura.
- [ ] Commander under attack: brief morale shock pulse on nearby friendly units.
- [ ] Payroll morale loss: small coin-slash marker in the HUD or economy overlay, not a map-wide warning on every unit.

First implementation choice: draw morale as a compact lower ring segment around units, then put exact `MoraleBand` and morale percent in the selected-unit HUD.

### Combat

- [x] Keep the current engaged ring for adjacent combat.
- [x] Add short impact flash or damage tick when a unit takes damage.
- [ ] Add a flanking marker when two or more friendly attack vectors exist.
- [x] Add a General/Commander threat pulse when leaders are attacked.
- [x] Add a death-burst marker when a unit is defeated.
- [x] Keep combat effects short-lived so the map does not smear after the fight moves.

### Cities And Production

- [x] City square owner color stays as the main ownership signal.
- [ ] Selected city gets a stronger outline, not a larger label.
- [x] Spawn capacity: small pips around owned cities.
- [ ] Blocked spawn: subtle blocked pip or warning border on selected city only.
- [ ] Purchase intent: tiny infantry/tank icon in HUD or standings, not permanent map text.

### Territory And Economy

- [x] Existing territory boundaries remain the default ownership layer.
- [x] Add optional tax heat overlay with restrained opacity.
- [x] Add optional recent territory swing highlights for a few ticks after capture.
- [x] Show payroll stress in standings/economy panel using a bar or warning chip.
- [ ] Show reserve budget and replenishment request in player rows for AI-only/debug mode.
- [ ] Avoid filling all owned cells with strong color; boundaries and heat overlays should stay readable behind units.

### Commanders, Generals, And Regions

- [ ] General: owner-colored unit with command ring.
- [ ] Commander: owner-colored ring unit with region aura when selected.
- [ ] Region anchor: subtle line or translucent wedge from Commander to target city.
- [ ] Protection detail: tethers from protectors to protected leader in debug/selected view.
- [ ] Leaderless region: muted region outline or dashed anchor line.
- [ ] General relocation: dashed path from General to fallback city.
- [ ] Commander threat: warning pulse around Commander plus HUD event.

### AI Intent And NEAT Identity

- [ ] Standings row shows AI archetype, directive, strategy mode, and target city in compact form.
- [ ] Selected AI player overlay shows target line from leader/region anchor to target city.
- [ ] AI-only mode can toggle all AI intent lines.
- [ ] Human-vs-AI mode should reveal only fair game information by default.
- [ ] Telemetry feed groups events by player color and event type.
- [ ] Fitness/debug panel is AI-only or developer-only.

### Visibility And Scouting

- [ ] Human mode gets a clear visible-area overlay only after fog rules are settled.
- [ ] Selected unit shows its visibility radius.
- [ ] Scout units show extended radius when selected.
- [ ] Last-known enemy positions use ghost markers only if the gameplay rules support memory.
- [ ] Enemy General visible warning appears in HUD and optional map ping.
- [ ] AI-only mode may show omniscient visibility diagnostics because it is a spectator/debug view.

### Paths, Terrain, And Movement

- [ ] Selected unit path remains lightweight and owner-colored.
- [ ] Planned target city gets a target bracket.
- [ ] Retreat path uses dashed red/orange style.
- [ ] Terrain cost overlay appears only on hover/selection/debug.
- [ ] Reserved next cells can show faint ghost markers in debug mode.
- [ ] Never obscure single-cell occupancy with overly large path strokes.

## Implementation Slices

### Slice 1: Visualization Diagnostics

- [x] Define a compact map visual diagnostics response for unit health ratio, morale band, selected role flags, current target, and recent event markers.
- [x] Keep diagnostics derived from `MatchSnapshot` and current simulation state.
- [x] Add deterministic ordering for all visual markers.
- [x] Add Brinell query coverage before drawing new layers.

### Slice 2: Unit Health And Morale MVP

- [x] Add health arc to unit drawing.
- [x] Add morale ring/arc to unit drawing.
- [x] Add routed and rout-risk visual states.
- [x] Add selected-unit HUD details: health, morale percent, morale band, role flags, target city, target region.
- [x] Add screenshot smoke test that morale markers render on active units.
- [x] Add Brinell state test that low-morale/routed units have distinct visual diagnostics.

### Slice 3: Combat Readability

- [x] Keep active combat ring.
- [x] Add short-lived damage flash or hit marker.
- [x] Add leader-under-attack pulse.
- [ ] Add flanking/rout-exploitation marker in AI-only/debug overlay. Deferred until the debug overlay toggle group exists.
- [x] Add death-burst marker when a unit dies.
- [x] Add screenshot smoke test for an active combat frame.

### Slice 4: Economy And Territory Overlays

- [x] Add overlay toggle group in HUD: Normal, Economy, Command, Visibility, AI Debug.
- [x] Economy overlay shows tax heat, payroll stress, spawn capacity, and recent territory swings.
- [x] Standings row shows treasury, tax, upkeep, and payroll deficit in compact bars.
- [x] Keep normal mode visually close to the current map.
- [x] Add Brinell tests for overlay toggles and no-overlap HUD layout.

### Slice 5: Command And Region Overlays

- [ ] Selected Commander shows region anchor and target line.
- [ ] Selected General shows relocation target and security warning if threatened.
- [ ] Protection details show shield/tether markers.
- [ ] Leaderless regions show muted dashed lines.
- [ ] Add tests using `GetRegions` and unit role flags.

### Slice 6: AI Spectator Overlay

- [ ] AI-only mode exposes archetype, genome id, directive, strategy mode, and target city in the spectator panel.
- [ ] Toggle all AI intent lines on the map.
- [ ] Add event counters for telemetry types such as flanking, commander pressure, scout discovery, tax raid, and economic collapse.
- [ ] Add behavior review export link/button only if it fits the current UI style.

### Slice 7: Visibility And Scouting Overlay

- [ ] Show selected unit visibility radius.
- [ ] Add optional fog or visible-cell overlay for human mode.
- [ ] Show scout extended radius and scout discovery ping.
- [ ] Show loss-of-contact only if the player information model includes memory.
- [ ] Add screenshot tests for visible-area overlay once enabled.

### Slice 8: Polish And Accessibility

- [ ] Verify markers are readable for all player colors.
- [ ] Use shape differences for morale and role states, not color alone.
- [ ] Keep text out of tiny map elements.
- [ ] Keep all HUD text inside its containers at current desktop resolution.
- [ ] Add a UAT checklist for battlefield readability.

## Suggested First Build

Start with the smallest useful player-facing change:

1. [x] Add morale rings and health arcs to units.
2. [x] Add selected-unit HUD details.
3. [x] Add one Brinell map-state query for visual diagnostics.
4. [x] Add one screenshot smoke test for the map with morale markers.
5. [x] Add one low-morale/routed setup test that proves the visual diagnostics can distinguish morale bands.

This makes morale visible immediately and creates the pattern for the other gameplay systems.

## Acceptance Criteria

- [x] A player can identify routed or rout-risk units without opening diagnostics.
- [x] A player can select a unit and see exact health, morale, morale band, role, and target.
- [x] A player can see where a recent unit death happened without reading diagnostics.
- [ ] AI-only mode can show each AI player's intent without cluttering human mode.
- [x] Economy state can be inspected visually without hiding units.
- [ ] Commander and General roles are visually distinct from normal units.
- [ ] Visibility/scouting overlays do not reveal unfair information in human mode.
- [x] Map rendering remains in `MapSpriteRenderer`.
- [x] Existing territory boundaries, cities, and unit markers remain visible after overlays.
- [x] Brinell screenshot coverage catches blank, overdrawn, or invisible overlay regressions.

## Do Not Do Yet

- [x] Do not add permanent text labels over every unit.
- [x] Do not draw the tactical playfield with Stride UI controls.
- [x] Do not make every gameplay system always visible at once.
- [ ] Do not expose hidden AI/fog information in normal human mode unless the game rules say it is known.
- [ ] Do not use a full-cell ownership fill as the default ownership visualization.

## Open Questions

- [x] Should morale be shown for every unit all the time, or only below `Normal` plus selected units?
  Proposed answer: show a tiny blue morale arc for every unit, with stronger blue treatment for cautious/rout-risk/routed units.
- [x] Should health be a bar, an arc, or only visible on selection?
  Proposed answer: use a tiny top arc/bar for all units because health is core combat feedback.
- [ ] Should fog of war be part of the first visualization pass?
  Proposed answer: no. Start with selected-unit visibility radius and AI-only diagnostics, then implement player-facing fog after the information model is final.
- [ ] Should AI intent be visible in human-vs-AI mode?
  Proposed answer: only for fair information such as observed target pressure; keep full genome/intent lines in AI-only/debug mode.
- [ ] Should economy overlays use colors independent from player colors?
  Proposed answer: yes. Use neutral heat colors for tax/economy overlays and keep player colors for ownership and units.
