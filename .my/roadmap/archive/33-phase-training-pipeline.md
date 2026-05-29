# Phase 33: Training Pipeline

Status: implemented with deterministic NEAT-style controllers. True long-running SharpNEAT evolution remains a later replacement/upgrade.

Parent: `25-remaining-prompt-gap-items.md`

## Scope

Add a headless, deterministic training pipeline with saved genome support.

## Tasks

- [x] Headless training project or core runner.
- [x] Genome/settings JSON serialization.
- [x] Saved genome loading.
- [x] Deterministic smoke tests.
- [x] Keep real long-running evolution out of normal tests.

## Acceptance

- [x] Training can run without Stride.
- [x] Main game can load a saved deterministic genome descriptor.
