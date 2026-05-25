# General Death Neutralizes Cities Roadmap

Status: implemented and verified.

## Goal

When a player's General dies, every city controlled by that player immediately reverts to neutral ownership.

## Problem

General death already eliminates the player, but their owned cities can still remain marked as controlled by that eliminated player. That makes the map misleading: the player is out, but their city squares can still show that player's color and may continue affecting score, standings, production assumptions, or victory checks.

The map should communicate that a defeated General no longer commands territory. Their cities become neutral and available for remaining players to capture.

## Definitions

- `General death` means a `UnitKind.General` is reduced to zero health, removed, or otherwise leaves the player with no live General.
- `Neutralize cities` means set every city with `OwnerId == defeatedPlayerId` to `GameConstants.NeutralPlayerId`.
- `Defeated player` means a player with `IsEliminated == true` because their General died.
- `Remaining units` means any non-General units that might still exist during the defeat cleanup pass.

## Proposed Design Decisions

- [x] Neutralize all cities owned by the defeated player.
- [ ] Neutralize only the defeated player's home city.
- [ ] Transfer defeated cities to the killing player.

- [x] Apply neutralization immediately during General death/elimination cleanup.
- [ ] Wait until the next production tick.
- [ ] Wait until another player enters the city.

- [x] Keep cities neutral even if defeated-player units were standing on or near them before cleanup.
- [ ] Let defeated-player units keep ownership until removed.

- [x] Stop defeated players from producing or scoring from former cities.
- [ ] Keep city score history after General death.

- [x] Update map city colors through the normal snapshot/UI refresh path.
- [ ] Add a separate defeat animation or special city color now.

## Slice 1: Simulation Rule

- [x] Add a helper such as `NeutralizeCitiesForPlayer(int playerId)`.
- [x] Call the helper when defeated-unit cleanup handles a dead `UnitKind.General`.
- [x] Call the helper in any other path that marks a player eliminated because their General is missing.
- [x] Ensure neutralization happens before score, standings, production, and victory checks run for the tick.
- [x] Ensure the simulation fingerprint changes when cities are neutralized.

## Slice 2: Capture And Production Interactions

- [x] Ensure eliminated players do not produce from formerly owned cities.
- [x] Ensure remaining active players can capture the neutralized cities normally.
- [x] Ensure city occupancy rules still apply after neutralization.
- [x] Ensure a neutralized city occupied by an active enemy is captured through the normal capture path.
- [x] Ensure a neutralized city with no active unit remains neutral.

## Slice 3: UI Behavior

- [x] Ensure city squares change to neutral/yellow after the owning General dies.
- [x] Ensure standings city counts drop to zero for the defeated player.
- [x] Ensure the target text uses `Neutral` for neutralized cities.
- [x] Ensure no stale player-color city markers remain after defeat.

## Slice 4: Test Coverage

- [x] Unit test: killing a General neutralizes all cities owned by that player.
- [x] Unit test: cities owned by other players are unchanged.
- [x] Unit test: eliminated player city count becomes zero in standings.
- [x] Unit test: neutralized cities can be captured later by an active player.
- [x] Brinell UI test only if the existing city ownership diagnostics do not cover map color/ownership refresh.
- [x] Run focused unit tests after implementation.

## Slice 5: Docs

- [x] Update `.my/HowToPlay.md` to explain that a dead General loses all cities.
- [x] Update `.my/UAT.md` with a player acceptance check for city neutralization after General death.
- [x] Update `.my/DevPrompts/Architecture.md` with the elimination ownership rule.
- [x] Mark this roadmap complete after implementation and verification.

## Open Questions

- [x] Should defeated cities become neutral or transfer to the killer?
- [ ] Should defeated cities transfer to the killer?
  Proposed answer: neutral. It is simpler, keeps the battlefield contestable, and matches the request directly.

- [x] Should neutralization happen immediately?
- [ ] Should neutralization wait for the next tick or capture event?
  Proposed answer: immediate during elimination cleanup. The map should never show an eliminated General still controlling cities.

- [x] Should city units from the defeated player matter after General death?
- [ ] Should defeated units hold ownership until removed?
  Proposed answer: no. Once the General dies, command authority is gone and cities become neutral regardless of remaining defeated-player units.

- [x] Should this require a Brinell UI test?
- [ ] Should this be unit-test only?
  Proposed answer: start with unit tests because this is a simulation rule. Add a focused Brinell test only if city ownership diagnostics or map refresh behavior need coverage.
