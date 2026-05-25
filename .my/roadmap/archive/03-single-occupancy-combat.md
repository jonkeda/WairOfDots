# Single Occupancy Combat Roadmap

## Goal

Make the grid tactical: each cell can hold only one unit, each city can hold only one unit, commanders and generals count as units, and opposing units can attack each other.

## Definitions

- A `unit` means infantry, tank, commander, or general.
- A cell is occupied when any living unit is on that grid point.
- A city is occupied when any living unit is on the city's grid point.
- A move is legal only if the destination cell is passable and unoccupied, unless the destination contains an enemy unit and the action is an attack.
- Attacks are made from adjacent cells, not by entering the defender's cell.

## Resolved Design Decisions

- [x] A unit is one tactical token: infantry, tank, commander, or general.
- [x] Cities produce onto an adjacent empty cell when possible.
- [x] Cities stop producing while no valid adjacent empty cell exists.
- [x] Commanders and generals can move by their own AI/directive logic, and the human can direct human leaders.
- [x] Attacks happen from adjacent cells.
- [x] Terrain affects both movement and attack/defense.

## Slice 1: Unit Identity

- [x] Replace anonymous city garrisons/moving groups with addressable unit entities.
- [x] Give every unit an id, owner, kind, grid position, health/strength, morale, and role.
- [x] Model commanders and generals as units with special roles rather than separate city-only markers.
- [x] Replace light/heavy stack semantics with tactical unit kinds: infantry and tank.
- [x] Keep commander and general as special tactical unit kinds.

## Slice 2: Occupancy Rules

- [x] Add a deterministic occupancy index keyed by `GridPoint`.
- [x] Enforce one unit per grid cell.
- [x] Enforce one unit per city cell.
- [x] Block friendly movement into occupied cells.
- [x] Produce new city units on adjacent empty cells instead of on occupied city cells.
- [x] Pause city production when no adjacent empty cell is available.
- [x] Allow target selection/pathfinding to reserve intended cells so two units do not move into the same cell on the same tick.
- [x] Decide deterministic conflict ordering when multiple units try to enter or attack the same cell.

## Slice 3: Movement Changes

- [x] Update pathfinding to avoid occupied friendly cells.
- [x] Let enemy-occupied cells be attack targets, not pass-through cells.
- [x] Stop movement when the next cell becomes blocked.
- [x] Re-path units when their route is blocked.
- [x] Add movement behavior for commander and general units.
- [x] Let the human direct human commander/general movement.
- [x] Keep unit positions stable and deterministic between ticks.

## Slice 4: Combat

- [x] Add adjacent-cell attack checks.
- [x] Add attack resolution without moving into the enemy-occupied cell.
- [x] Resolve damage using unit kind, morale, terrain, and commander/general bonuses.
- [x] Add terrain attack/defense modifiers.
- [x] Remove defeated units from occupancy.
- [x] Capture a city when the occupying unit belongs to a new owner and enemies are gone.
- [x] Damage or eliminate a player when their commander/general unit is defeated.

## Slice 5: UI

- [x] Render each unit as one circle on its exact grid cell.
- [x] Render commanders and generals with distinct circle styles while still treating them as units.
- [x] Add occupied-city visual state.
- [x] Add attack/engaged visual state.
- [x] Update Brinell map queries to report unit count, occupied cell count, city occupancy, and active combats.

## Slice 6: Test Coverage

- [x] Unit test: two friendly units cannot occupy the same grid cell.
- [x] Unit test: a city cannot hold more than one unit.
- [x] Unit test: infantry, tank, commanders, and generals count toward occupancy.
- [x] Unit test: production places a unit on an adjacent empty cell.
- [x] Unit test: production pauses when adjacent cells are occupied or blocked.
- [x] Unit test: units re-path or wait when a friendly unit blocks the next cell.
- [x] Unit test: adjacent enemy units can attack each other.
- [x] Unit test: non-adjacent enemy units cannot attack each other.
- [x] Unit test: terrain modifies combat results.
- [x] Unit test: combat removes defeated units and clears occupancy.
- [x] Unit test: general defeat can eliminate a player.
- [x] Brinell smoke test: no cell renders more than one unit marker.
- [x] Brinell gameplay test: after advancing ticks, at least one attack/combat event can be observed.
- [x] Run build, unit tests, and Brinell UI tests.

## Closed Questions

- [x] Should a unit represent one soldier, one army group, or one token containing multiple light/heavy soldiers?
  Answer: one tactical token: infantry, tank, commander, or general.
- [x] Should cities produce units directly onto their cell only when empty, or queue production while occupied?
  Answer: place produced units outside the city on an adjacent empty cell when possible; otherwise stop producing.
- [x] Should commanders/generals be movable by player directive, AI directive, or automatically follow armies?
  Answer: commanders and generals can move themselves through their own logic; the human can direct human leaders.
- [x] Should attacks happen only by moving into a cell, only from adjacent cells, or both?
  Answer: attacks happen from adjacent cells.
- [x] Should terrain affect only movement cost, or also attack/defense?
  Answer: terrain affects both movement and attack/defense.
