# Wair of Dots Architecture

## Boundaries

- `src/WairOfDots.Core`: deterministic simulation, AI contracts, NEAT-style genomes, telemetry, and replay fingerprints.
- `src/WairOfDots.Windows`: Stride UI host, player input, visual state, and Brinell automation.
- `tests/WairOfDots.Tests`: pure C# unit tests.
- `tests/WairOfDots.UITests`: Brinell.Stride UI tests.

## Design Rules

- The simulation owns game truth. Stride displays and commands it.
- The human controls one General-level player.
- AI players use separate deterministic genome controllers sharing one observation/action contract.
- Fixed ticks drive gameplay; rendering is decoupled.
- Brinell tests use Automation IDs and game queries, not manual inspection.
- Oravey is a reference only for Stride/Brinell integration patterns. No Oravey mechanics or content are copied.

## MVP Mechanics

- Cities are command, capture, and production targets on a passable terrain grid.
- Owned cities generate resources and produce infantry/tanks onto adjacent empty cells.
- Infantry, tanks, commanders, and generals are addressable tactical units.
- Each grid cell can hold only one living unit, and each city cell can hold only one living unit.
- Units move across passable grid cells toward city targets.
- Friendly units block movement into occupied cells.
- Enemy units fight from adjacent cells instead of stacking into one cell.
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

## Testing Contract

Unit tests verify deterministic simulation behavior directly. Brinell UI tests verify the executable, UI controls, automation server, and visible gameplay flow.
