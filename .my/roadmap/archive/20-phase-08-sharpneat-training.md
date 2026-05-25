# Phase 08 SharpNEAT Training Roadmap

Status: implemented as deterministic training hooks; true SharpNEAT package integration remains a later extension.

## Goal

Move from deterministic NEAT-style archetypes to actual trainable genomes with a headless runner and saved genome loading.

## Scope

- [ ] Choose and add SharpNEAT package/version or document an alternative.
- [ ] Create headless training runner project.
- [ ] Add genome serialization.
- [ ] Add deterministic tiny training smoke tests.
- [ ] Implement Dot net fitness.
- [ ] Implement Commander net fitness.
- [ ] Implement General net fitness.
- [ ] Add tax efficiency, territory control, payroll stability, and army sustainability to fitness inputs.
- [ ] Load generated genomes into the game.
- [ ] Keep current deterministic controllers as fallback.

## Design Decisions

- [ ] Decide SharpNEAT version.
- [ ] Decide genome file format and storage location.
- [ ] Decide how long-running training is excluded from normal tests.
- [ ] Decide if training outputs live in repo, `.my`, or external artifacts.
- [ ] Decide how economy-heavy fitness is balanced against combat and General survival.

## Implementation Slices

- [ ] Add training project to solution.
- [ ] Add simulation batch runner.
- [ ] Add fitness contracts.
- [ ] Add economy metrics to fitness contracts.
- [ ] Add tiny deterministic smoke training.
- [ ] Add genome save/load.
- [ ] Add runtime genome selection.

## Test Plan

- [ ] Unit tests for fitness calculations.
- [ ] Unit tests for economy fitness calculations.
- [ ] Deterministic smoke training test with tiny population/generation count.
- [ ] Runtime load test for saved genomes.
- [ ] No long-running training in normal CI.
