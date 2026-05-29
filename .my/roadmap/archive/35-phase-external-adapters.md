# Phase 35: External Controller Adapters

Status: implemented as safe adapter seams.

Parent: `25-remaining-prompt-gap-items.md`

## Scope

Add safe adapter seams for future LLM and ONNX integrations without making the game depend on external services.

## Tasks

- [x] LLM/external controller adapter interface/fallback.
- [x] ONNX export manifest placeholder.
- [x] Model validation metadata.
- [x] Tests that external adapter failure falls back deterministically.

## Acceptance

- [x] External adapters are optional.
- [x] Runtime failures fall back to deterministic controllers.
