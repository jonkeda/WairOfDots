# Phase 34: Neural Hierarchy

Status: implemented as deterministic hierarchy contracts and trace records. Separate trained nets per tier remain future training work.

Parent: `25-remaining-prompt-gap-items.md`

## Scope

Make the prompt-level tier hierarchy explicit behind current deterministic controllers.

## Tasks

- [x] Light/Heavy unit controller labels map to Infantry/Tank controller roles.
- [x] Commander net adapter contract.
- [x] General net adapter contract.
- [x] Downward intent and upward report records.
- [x] Tests for tier data shape.

## Acceptance

- [x] Decisions can be traced General -> Commander -> Unit.
- [x] Deterministic controllers still satisfy the same contracts.
