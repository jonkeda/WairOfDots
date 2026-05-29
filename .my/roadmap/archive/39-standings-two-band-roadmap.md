# Standings Two-Band Roadmap

Status: implemented on 2026-05-25.

## Goal

Make the AI-only standings easier to compare by splitting the current eight-metric player rows into two main comparison bands:

- Battlefield band: `Score`, `Cities`, `Units`, `General`.
- Economy band: `Treasury`, `Tax`, `Upkeep`, `Deficit`.

Each band should show the same players in the same order, with four bars per player. This keeps related metrics together and makes it easier to scan one topic at a time.

## Problem

The current standings row packs battlefield and economy values into one stacked player row. It is compact, but the eye has to bounce vertically inside each player row and horizontally across all eight metrics.

That makes direct comparison harder:

- Comparing all players by army strength means ignoring the economy line.
- Comparing all players by tax or deficit means ignoring the battlefield line.
- The two-line-per-player shape is dense enough that the panel reads as a block of small bars instead of two clear stories.

## Target Layout

The standings panel should become two stacked sections.

First section:

```text
Battlefield
       Score   Cities   Units   General
P0     bar     bar      bar     bar
P1     bar     bar      bar     bar
P2     bar     bar      bar     bar
P3     bar     bar      bar     bar
```

Second section:

```text
Economy
       Treasury   Tax   Upkeep   Deficit
P0     bar        bar   bar      bar
P1     bar        bar   bar      bar
P2     bar        bar   bar      bar
P3     bar        bar   bar      bar
```

The player dot remains at the start of each player row in both sections.

## Design Rules

- Keep the existing player color dot as the identity signal.
- Keep four bars per player row.
- Keep exact values visible above or inside each metric group.
- Keep bar scaling per metric column, not across unrelated metrics.
- Keep AI-only mode compact enough to fit the current right-side HUD.
- Do not reintroduce player names if they make the panel too wide.
- Do not turn the tactical map into UI controls.

## Implementation Slices

### Slice 1: Layout Split

- [x] Create a reusable standings section builder.
- [x] Render a `Battlefield` section with `Score`, `Cities`, `Units`, and `General`.
- [x] Render an `Economy` section with `Treasury`, `Tax`, `Upkeep`, and `Deficit`.
- [x] Keep player ordering identical across both sections.

### Slice 2: Compact Visual Polish

- [x] Tune section titles, header heights, row heights, and spacing.
- [x] Make the economy section visually distinct without using a heavy card.
- [x] Preserve fixed metric widths so bars do not jitter between refreshes.
- [x] Ensure long labels like `Treasury` and `Deficit` fit cleanly.

### Slice 3: Brinell Coverage

- [x] Update standings UI tests to expect two section containers.
- [x] Assert both sections contain the same player rows in the same order.
- [x] Assert each player has exactly four bars in each section.
- [x] Add layout smoke coverage that all standings elements are visible and non-empty.

### Slice 4: Player Docs

- [x] Update `.my/StandingsPanel.md` to describe the two-band layout.
- [x] Update `.my/HowToPlay.md` if the short standings summary changes.

## Proposed Automation IDs

- `StandingBattlefieldSection`
- `StandingBattlefieldHeaderRow`
- `StandingBattlefieldRow_{playerId}`
- `StandingEconomySection`
- `StandingEconomyHeaderRow`
- `StandingEconomyRow_{playerId}`

Existing metric IDs can stay stable where possible, such as `StandingScoreBar_{playerId}` and `StandingTreasuryBar_{playerId}`.

## Acceptance Criteria

- [x] AI-only standings show two clear sections: battlefield and economy.
- [x] Each section shows all players in snapshot order.
- [x] Each player row in each section has four metric bars.
- [x] The battlefield section contains only `Score`, `Cities`, `Units`, and `General`.
- [x] The economy section contains only `Treasury`, `Tax`, `Upkeep`, and `Deficit`.
- [x] Bars remain readable at the current desktop resolution.
- [x] Normal gameplay HUD controls still fit without overlapping standings.
- [x] Brinell tests prove the split layout and ordering.

## Open Questions

- Should the economy section always be visible, or only when Economy overlay is selected?
  Proposed answer: always visible in AI-only mode, because standings are already a spectator/debug panel.

- Should `Deficit` bars be red even when the value is `0`?
  Proposed answer: keep zero deficit muted, and use red only for nonzero deficit.

- Should the two sections repeat player dots?
  Proposed answer: yes. Repeating the dot keeps each row independently readable.
