# Phase 10 Emergent Behavior Instrumentation Roadmap

Status: implemented.

## Goal

Add telemetry and review tools for the emergent behaviors named in the original prompt.

## Scope

- [ ] Track flanking attempts.
- [ ] Track decapitation attempts.
- [ ] Track rout exploitation.
- [ ] Track commander threat and hiding behavior.
- [ ] Track scout discoveries.
- [ ] Track budget stress.
- [ ] Track tax raids.
- [ ] Track payroll deficits.
- [ ] Track overbuilding.
- [ ] Track territory swings.
- [ ] Track economic collapse.
- [ ] Add AI-only watcher/export tools.

## Design Decisions

- [ ] Decide event schema for emergent behavior telemetry.
- [ ] Decide whether logs are JSONL, CSV, or snapshot-attached records.
- [ ] Decide how cell territory changes are aggregated so telemetry does not become too noisy.
- [ ] Decide whether the game UI needs a watcher panel or file export is enough.
- [ ] Decide how to avoid brittle tests for emergent behavior.

## Implementation Slices

- [ ] Add telemetry event types.
- [ ] Add detectors for each behavior.
- [ ] Add economy detectors for tax raids, payroll deficit, overbuilding, territory swings, and economic collapse.
- [ ] Add export path.
- [ ] Add optional watcher UI.
- [ ] Add docs for interpreting behavior logs.

## Test Plan

- [ ] Unit tests for deterministic detector triggers.
- [ ] Unit tests for deterministic economy detector triggers.
- [ ] Snapshot/fingerprint metadata tests.
- [ ] Brinell only if watcher UI is added.
- [ ] UAT prompts should ask players to observe behaviors without requiring deterministic emergence.
