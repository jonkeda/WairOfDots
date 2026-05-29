# Brinell Simulation Control Roadmap

Status: implemented.

## Goal

Give Brinell UI tests deterministic control over simulation time so gameplay tests can advance to meaningful states without relying on wall-clock waits, fragile tick guesses, or repeated local polling loops.

## Problem

`StepTicks` and `StepToTick` already exist, which is good. Several UI tests still hand-roll loops like "step 12 ticks until territory changes" or "step 4 ticks until combat happens." That spreads gameplay waiting logic across tests and makes future tests harder to read.

As more visual gameplay state becomes testable, Brinell will need a reusable way to say: advance the deterministic simulation until a known condition is true, or stop at a bounded tick budget with a useful failure result.

## Current State

- [x] `StepTicks` automation query exists.
- [x] `StepToTick` automation query exists.
- [x] UI tests use direct simulation stepping instead of normal real-time waiting in many places.
- [x] Shared `StepUntil` query exists.
- [x] Repeated condition loops in current UI tests are reduced.
- [x] `StepUntil` failures report condition detail, attempts, and ticks stepped.

## Design Direction

- [x] Keep direct `StepTicks` and `StepToTick`; they are still the simplest tools for exact deterministic states.
- [x] Add `StepUntil` for bounded gameplay conditions.
- [x] Keep `StepUntil` deterministic and synchronous.
- [x] Return both the final snapshot and metadata about success, attempts, and ticks stepped.
- [x] Prefer condition names over arbitrary code execution in the automation handler.
- [x] Keep condition names small and tied to real test needs.

## First Conditions

- [x] `TerritoryBoundaryCountNotEqual`: used when waiting for territory ownership shape to change.
- [x] `CityOwnedCountGreaterThan`: used when waiting for neutral cities to be captured.
- [x] `AnyDiagonalVisualTarget`: used by movement visual diagnostics.
- [x] `AnyInterpolatingUnit`: used by smooth movement diagnostics.
- [x] `ActiveCombat`: used by combat map-state tests.
- [x] `TickAtLeast`: useful for simple bounded stepping through the same API.

## Implementation Slices

### Slice 1: Automation Command

- [x] Add `StepUntil` to `WairAutomationHandler`.
- [x] Accept condition name, compare value, max ticks, and step size.
- [x] Clamp max ticks and step size to safe deterministic bounds.
- [x] Evaluate the condition before and after each step.
- [x] Return a `StepUntilResponse` with final snapshot, condition, satisfied flag, ticks stepped, attempts, and detail.

### Slice 2: Test Helpers

- [x] Add `GameQueryHelpers.StepUntil`.
- [x] Keep condition-specific wrappers deferred until tests need more syntax sugar.
- [x] Add `StepUntilDto` test record.

### Slice 3: UI Test Cleanup

- [x] Replace repeated step loops in current UI tests.
- [x] Keep exact `StepTicks` where exact planning or pause behavior matters.
- [x] Keep real-time polling only for visual interpolation across rendered frames.

## Acceptance Criteria

- [x] Current UI tests compile and pass.
- [x] Repeated gameplay polling loops are reduced.
- [x] Failed `StepUntil` assertions can report condition, ticks stepped, and detail.
- [x] `StepTicks` and `StepToTick` behavior remains unchanged.

## Open Questions

- [x] Should `StepUntil` live only in test helpers or in app automation?
  Answer: app automation. That lets real Brinell clients use the same deterministic stepping path.
- [x] Should `StepUntil` support arbitrary predicates?
  Answer: no. Use named conditions so the automation surface stays stable, safe, and easy to version.
