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

- Cities are connected graph nodes.
- Owned cities generate resources and units.
- Unit groups move along edges toward targets.
- Light units are cheaper/faster; Heavy units are slower/stronger.
- Cities over capacity lose morale.
- Multiple players in one city fight until one side remains.
- Capturing a city holding a commander damages or kills that commander.
- Capturing a city holding a general damages or kills that general.
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
- Light/heavy preference.
- Aggression score.

## Testing Contract

Unit tests verify deterministic simulation behavior directly. Brinell UI tests verify the executable, UI controls, automation server, and visible gameplay flow.
