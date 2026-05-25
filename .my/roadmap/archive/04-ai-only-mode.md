# AI Only Mode Roadmap

## Goal

Add a spectator-friendly AI-only mode where every player slot is controlled by an AI genome, the match can run without human commands, and the UI focuses on observing, comparing, and testing AI behavior.

## Definitions

- `AI only mode` means no human-controlled player exists in the match.
- Every active player uses an AI controller with its own deterministic genome id.
- The user is a spectator who can start, pause, restart, speed up, inspect, and replay matches.
- AI-only matches must remain deterministic for the same seed, player count, and mode settings.
- Existing human mode must continue to work unchanged.

## Proposed Design Decisions

- [x] Add a game mode setting rather than a separate executable.
- [ ] Create a separate AI-only executable.

- [x] In AI-only mode, player id `0` becomes `AI 0` instead of `Human`.
- [ ] Keep player id `0` as a hidden human with no commands.
- [ ] Reserve player id `0` and start AI players at `1`.

- [x] Keep the same map, tactical units, city rules, and victory conditions.
- [ ] Use a special AI arena map.
- [ ] Remove victory and run endless simulations.

- [x] Replace command controls with spectator controls in AI-only mode.
- [ ] Leave human command controls visible but disabled.
- [ ] Keep command controls and let the spectator override one AI.

- [x] Add simulation speed controls for spectator mode.
- [ ] Always run at normal speed.
- [ ] Always run as fast as possible.

- [x] Use Brinell game queries to verify AI-only mode determinism, HUD state, and AI activity.
- [ ] Test only through pure unit tests.

## Slice 1: Settings And Mode Identity

- [x] Add `GameMode` or equivalent setting with `HumanVsAi` and `AiOnly`.
- [x] Normalize settings so AI-only mode allows all configured player slots to be AI players.
- [x] Preserve the current default mode as human vs AI.
- [x] Include game mode in replay fingerprints.
- [x] Include game mode in snapshots and automation responses.
- [x] Update constructors/factories so tests can create either mode directly.

## Slice 2: Player Creation

- [x] Refactor player creation so human presence is optional.
- [x] In AI-only mode, create player `0` as `AI 0`.
- [x] Give every AI a deterministic genome id derived from seed and player id.
- [x] Ensure AI controllers exist for every non-eliminated player in AI-only mode.
- [x] Remove all assumptions that `GameConstants.HumanPlayerId` is present in every mode.
- [x] Make score, ownership, city snapshots, and unit snapshots mode-neutral.

## Slice 3: Simulation Behavior

- [x] Ensure `Start`, `Step`, `Pause`, `Resume`, victory, production, movement, and combat work without a human player.
- [x] Ensure AI planning runs for every active player.
- [x] Ensure target selection never depends on human command state in AI-only mode.
- [x] Keep deterministic planning order by player id.
- [x] Keep deterministic movement and combat ordering.
- [x] Support match timer winner selection in AI-only mode.
- [x] Preserve existing human-mode behavior and tests.

## Slice 4: UI And Spectator Controls

- [x] Add mode selection to the start menu.
- [x] In AI-only mode, show `AI Only` in the status/HUD.
- [x] Hide or replace Attack, Hold, Defend, city target, and infantry/tank preference controls.
- [x] Add spectator controls: normal speed, faster, slower, pause, restart, menu.
- [x] Add a standings panel showing each AI player, score, cities, units, resources, general health, and status.
- [x] Keep city buttons useful for inspection even when they no longer issue human commands.
- [x] Show selected city details without changing AI orders.
- [x] Make end-state text say which AI won.

## Slice 5: Automation And Brinell

- [x] Add a Brinell query/command to start AI-only mode with seed and player count.
- [x] Expose mode in `GetSnapshot`.
- [x] Expose AI telemetry summaries for all AI players.
- [x] Expose standings through a stable game query.
- [x] Keep screenshot smoke tests checking that the map renders.
- [x] Add UI tests that confirm command controls are absent or hidden in AI-only mode.
- [x] Add UI tests that confirm spectator controls are visible.
- [x] Add UI tests that confirm every player emits AI telemetry after planning.

## Slice 6: Test Coverage

- [x] Unit test: AI-only settings create no human player.
- [x] Unit test: AI-only player `0` is AI-controlled.
- [x] Unit test: all AI players receive unique genome ids.
- [x] Unit test: same AI-only seed produces the same fingerprint.
- [x] Unit test: different AI-only seeds produce different fingerprints after planning.
- [x] Unit test: AI-only match advances, captures cities, and ends.
- [x] Unit test: human mode still creates one human and the configured AI count.
- [x] Brinell smoke test: AI-only starts to a playable map.
- [x] Brinell gameplay test: AI-only emits telemetry for every active AI.
- [x] Brinell UI test: AI-only shows spectator controls instead of human command controls.
- [x] Run build, unit tests, and Brinell UI tests.

## Slice 7: Docs

- [x] Update `.my/HowToPlay.md` with AI-only spectator mode.
- [x] Update `.my/UAT.md` with AI-only acceptance checks.
- [x] Update `.my/DevPrompts/Architecture.md` with optional-human mode.
- [x] Update `.my/DevPrompts/Roadmap.md` once implemented.

## Open Questions

- [x] Should AI-only mode be selected from the existing start menu?
- [ ] Should AI-only mode have its own executable?
- [ ] Should AI-only mode be command-line only?
  Proposed answer: use the existing start menu so players can switch modes easily and Brinell can test both flows.

- [x] Should the spectator be able to inspect cities and units but not issue orders?
- [ ] Should the spectator be able to override one AI temporarily?
- [ ] Should the spectator only watch with no interaction?
  Proposed answer: allow inspection only. It keeps the mode true AI-only while making it useful for debugging and watching.

- [x] Should AI-only use the same match length and victory rules as human mode?
- [ ] Should AI-only run endlessly until manually stopped?
- [ ] Should AI-only use shorter benchmark matches by default?
  Proposed answer: keep the same rules first, then add benchmark presets later if needed.

- [x] Should speed controls be part of this roadmap?
- [ ] Should speed controls be a later roadmap?
  Proposed answer: include basic speed controls because spectator mode needs them to be pleasant and useful.
