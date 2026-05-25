# Wair of Dots Architecture

## Boundaries

- `src/WairOfDots.Core`: deterministic simulation, AI contracts, NEAT-style genomes, telemetry, and replay fingerprints.
- `src/WairOfDots.Windows`: Stride UI host, player input, visual state, and Brinell automation.
- `tests/WairOfDots.Tests`: pure C# unit tests.
- `tests/WairOfDots.UITests`: Brinell.Stride UI tests.

## Design Rules

- The simulation owns game truth. Stride displays and commands it.
- The human controls one General-level player in human-vs-AI mode.
- AI-only mode replaces the human slot with `AI 0` and runs every player from a deterministic genome.
- AI players use separate deterministic genome controllers sharing one observation/action contract.
- Fixed ticks drive gameplay; rendering is decoupled.
- Unit `Cell` is the authoritative position for occupancy, pathfinding, combat, capture, scoring, and fingerprints.
- Unit visual movement is render-only: Stride interpolates circle markers between recorded map positions, and Brinell can query both the authoritative cell and visual position.
- Movement pathfinding is eight-way. Diagonal steps cost `sqrt(2)` times the destination terrain cost and cannot cut through blocked, occupied, or reserved corners.
- After A* finds a grid path, a line-of-sight smoothing pass removes unnecessary intermediate waypoints. Smoothed waypoints are stored separately from the authoritative grid path. Visual interpolation follows the smoothed line while the simulation still steps through grid cells.
- City interaction is map-first: map city squares are clickable, colored by current owner, and do not show numeric labels or dark backing blocks. City list buttons are not part of the player HUD.
- General death is an ownership boundary: when a player's General is gone, the player is eliminated and every city they controlled immediately becomes neutral.
- AI-only standings are compact horizontal bar rows. The snapshot order is preserved without UI sorting. Metric labels live in a header row, each player row starts with a non-muted player-color dot, and each row exposes named Brinell elements for score, city, unit, and general-health bars.
- Spectator speed changes how many fixed simulation ticks run per rendered tick; it does not make the simulation nondeterministic.
- Brinell tests use Automation IDs and game queries, not manual inspection.
- Oravey is a reference only for Stride/Brinell integration patterns. No Oravey mechanics or content are copied.

## MVP Mechanics

- Cities are command, capture, and production targets on a passable terrain grid.
- Owned cities generate resources and produce infantry/tanks onto adjacent empty cells.
- Infantry, tanks, commanders, and generals are addressable tactical units.
- Each grid cell can hold only one living unit, and each city cell can hold only one living unit.
- Units move across passable grid cells toward city targets using cardinal and diagonal steps.
- Friendly units block movement into occupied cells.
- Enemy units fight from adjacent cardinal or diagonal cells instead of stacking into one cell.
- Terrain affects movement cost and combat attack/defense.
- Commanders and generals are movable units with special health/defeat consequences.
- A player is eliminated when their general falls.
- The match ends when one player remains, the human controls all cities, or the match timer expires.

## AI Contract

AI receives a perception-limited normalized observation:

- Owned city ratio.
- Unit strength ratio.
- Resource level.
- Commander and general health.
- Visible target pressure.
- Current score.
- Match time.

AI returns:

- Directive: Attack, Hold, or Defend.
- Target city.
- Infantry/tank production preference.
- Aggression score.

## Modes

- `HumanVsAi`: player `0` is Human and the configured AI count is added after that.
- `AiOnly`: every configured player slot is AI-controlled, including player `0`.

## Testing Contract

Unit tests verify deterministic simulation behavior directly. Brinell UI tests verify the executable, UI controls, automation server, and visible gameplay flow.
