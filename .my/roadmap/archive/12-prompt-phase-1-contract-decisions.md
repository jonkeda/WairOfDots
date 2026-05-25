# Prompt Phase 1 Contract Decisions Roadmap

Status: answered.

Source: `.my/roadmap/11-prompt-gap-roadmap.md`

## Goal

Lock the baseline decisions from the original prompt gap review so later phase roadmaps do not reopen settled MVP choices.

## Decisions

- [x] Use `11-prompt-gap-roadmap.md` as the prompt coverage tracker.
- [x] Do not add a separate prompt coverage doc right now.
- [x] Keep `Infantry` and `Tank` as the code and player-facing unit names.
- [x] Leave room for more unit types later instead of renaming existing units to `Light` and `Heavy`.
- [x] Keep single-cell grid occupancy as the gameplay model.
- [x] Do not implement city stack capacity or city attrition right now.
- [x] Treat city-node path movement from the original prompt as superseded by grid movement.

## Clarification

The city capacity/attrition question came from the original prompt saying each city supports up to 5 units before attrition. The current game changed direction: units live on grid cells, and each city cell can hold only one unit. Those two models conflict. The chosen answer is to keep the newer grid/single-occupancy model.

If the game later needs "crowding" pressure, it should probably be regional or adjacent-cell pressure rather than stacking multiple units inside one city cell.

## Follow-Up Impact

- Phase 01 can use `Infantry` and `Tank` in controller contracts.
- Phase 03 morale/rout should use grid-local neighbors, not stacked city units.
- Phase 04 commander regions should build on grid regions/fronts, not city-edge graphs.
- Phase 08 SharpNEAT training should train against the current grid model.

## Test Plan

- [x] No tests needed; this is a documentation/decision roadmap.
