# Unit Properties

Status: current implementation plus command-and-control roadmap targets.

This document describes the unit kinds in Wair of Dots. Numeric values are current code values unless marked as roadmap target.

## Shared Unit Rules

- All units occupy one grid cell.
- All units can move in eight directions across passable terrain.
- All units block friendly movement and cannot stack with other units.
- All units can fight adjacent enemies, including diagonals.
- All units have health and morale.
- All units contribute to upkeep while alive and participating.
- All units have local vision.
- All units capture their occupied passable cell.
- Assigned non-leader units show their Commander's number.
- Unassigned non-leader units are reserve and show a reserve pip.
- Single-unit and group reserve assignment and recall are General commands only.
- Commanders may request reinforcements or reserve recall through reports, but cannot assign or recall units themselves.

## Current Numeric Properties

| Unit | Health | Speed | Attack | Vision | Current Upkeep | Production Cost |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Infantry | 10 | 1.15 | 2.0 | 5 | 0.20 | 2.0 |
| Tank | 18 | 0.82 | 3.7 | 6 | 0.45 | 4.5 |
| Commander | 24 | 1.00 | 1.8 | 8 | 0.90 | 9.0 |
| General | 32 | 0.72 | 1.4 | 10 | 0.25 | Not produced |

Scout role or scout directive adds `+2` vision to that unit.

## Infantry

Infantry are the cheap, fast baseline unit.

- Production: treasury-funded city production.
- Cost: `2.0`.
- Upkeep: `0.20`.
- Health: `10`.
- Speed: `1.15`.
- Attack: `2.0`.
- Vision: `5`.
- Capture: occupied cell only.
- Command role: can be assigned to one Commander or remain in reserve.
- Good for: early expansion, capturing cells, filling lines, and cheap reinforcement.

## Tank

Tanks are slower, stronger combat and territory-control units.

- Production: treasury-funded city production.
- Cost: `4.5`.
- Upkeep: `0.45`.
- Health: `18`.
- Speed: `0.82`.
- Attack: `3.7`.
- Vision: `6`.
- Capture: occupied cell plus adjacent passable cells when not occupied by enemies.
- Command role: can be assigned to one Commander or remain in reserve.
- Terrain note: tanks deal reduced damage when attacking into forest or rock.
- Good for: breaking contested fronts, holding valuable ground, and rapid territory conversion.

## Commander

Commanders are physical leader units and tactical command anchors.

- Production: treasury-funded city production.
- Cost: `9`.
- Upkeep: `0.9`.
- Health: `24`.
- Speed: `1.00`.
- Attack: `1.8`.
- Vision: `8`.
- Capture: occupied cell only.
- Numbering: each Commander has a stable player-local number starting at `1`.
- Assignment: can have zero or more rectangular regions.
- Subordinates: commands only units assigned to that Commander.
- Reserve authority: cannot recall units to reserve; may request recall from the General.
- Morale role: nearby Commanders improve friendly morale and support attacks.
- Death effect: Commander death damages morale and creates a leaderless window.
- New Commanders start with zero regions and zero subordinate units.
- A later General planning tick assigns regions to new Commanders.

## General

Generals are physical strategic leader units and the player's command authority.

- Production: not produced.
- Cost: none after match start.
- Upkeep: `0.25`.
- Health: `32`.
- Speed: `0.72`.
- Attack: `1.4`.
- Vision: `10`.
- Capture: occupied cell only.
- Command role: assigns regions, assigns reserve units or groups, recalls assigned units or groups to reserve, and issues city production commands.
- Morale role: nearby Generals can rally low-morale units and support attacks.
- Death effect: General death eliminates the player and neutralizes that player's cities and controlled cells.

## Vision

Vision is local and player-owned:

- A player's visible area is the union of all living friendly unit vision ranges.
- Distance uses Chebyshev grid distance.
- Terrain does not block vision in the first command-and-control pass.
- Commanders and Generals are not omniscient.
- Hidden enemy details must stay hidden in human-vs-AI mode.
- AI-only/debug mode may expose full diagnostics.

## Combat Modifiers

Base damage uses attacker attack power, terrain, leader support, and morale:

- Road attack modifier: `1.08`.
- Hill attack modifier: `1.10`.
- Forest attack modifier: `0.95`.
- Rock attack modifier: `0.90`.
- Road defense modifier: `0.92`.
- Forest defense modifier: `1.15`.
- Hill defense modifier: `1.25`.
- Rock defense modifier: `1.40`.
- Nearby Commander support: `+0.15` attack modifier within `2` cells.
- Nearby General support: `+0.10` attack modifier within `3` cells.
- Tank attacking forest or rock: extra `0.85` damage multiplier.

## Economy Notes

- Controlled passable cells generate tax.
- Cities are high-value tax cells and spawn anchors.
- Upkeep is paid from treasury on economy ticks.
- If upkeep cannot be fully paid, units lose morale instead of dying immediately.
- Current production creates infantry or tanks near owned cities when an adjacent passable cell is open.
- Newly produced infantry and tanks start in reserve.
- Roadmap production should add Commanders at cost `9` and upkeep `0.9`.

## Command And Control Notes

- The General owns single-unit and group reserve assignment and reserve recall.
- Commanders command assigned units only.
- Units without an assigned Commander are reserve.
- Commanders can report `NeedReinforcements` and `ReserveRecallRequested`, but this does not change assignment by itself.
- The first command-and-control slice should use one active executable command per Commander and per unit.
