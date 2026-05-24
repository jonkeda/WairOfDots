# Wair of Dots Roadmap

## MVP Definition

Build a complete local single-player Stride game where one human General competes against four deterministic AI players. The game uses a terrain grid, city capture, infantry/tank unit production, commander/general defeat conditions, deterministic AI genomes, Brinell UI automation, and unit-tested pure simulation logic.

## Milestone 1: Playable Core

- [x] Pure C# deterministic simulation.
- [x] Fixed-tick game loop.
- [x] Default map with cities, edges, homes, and neutral control points.
- [x] Human player plus scenario-configurable AI players.
- [x] AI-only spectator mode with player `0` controlled by AI.
- [x] Infantry, tank, commander, and general tactical units.
- [x] City production, eight-way grid movement, adjacent combat, capture, and victory.
- [x] Commander and General unit health states.
- [x] NEAT-style deterministic genome controllers for AI players.
- [x] Replay/fingerprint support for test determinism.

## Milestone 2: Stride Host

- [x] Windows Stride host.
- [x] Main menu with seed and AI player count.
- [x] Human checkbox for switching between human-vs-AI and AI-only mode.
- [x] General-mode HUD.
- [x] City target selection.
- [x] Attack, Hold, and Defend directives.
- [x] Infantry/tank preference controls.
- [x] Pause, restart, and end-state UI.
- [x] Brinell automation server and game queries.

## Milestone 3: Tests

- [x] Unit tests for simulation, determinism, AI, occupancy, diagonal movement, combat/capture, and pause behavior.
- [x] Brinell UI tests for launch, start, HUD, deterministic seed, occupancy, diagonal movement, combat, AI-only mode, pause, restart, and directive interaction.

## Milestone 4: Player Acceptance

- [x] `HowToPlay.md` for player-facing instructions.
- [x] `UAT.md` for manual acceptance testing.

## Future Expansion

- [ ] Long-running SharpNEAT training runner.
- [ ] Multi-tier General, Commander, and Dot genome training phases.
- [ ] Any-angle path smoothing beyond eight-way movement.
- [ ] Richer visual map rendering with animated unit travel.
- [ ] Commander-mode and Dot-mode human play.
- [ ] Saved match replay viewer.
