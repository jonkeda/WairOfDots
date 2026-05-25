# Phase 02 Cell Territory Tax Economy Roadmap

Status: implemented.

## Goal

Change the economy from mostly city ownership to territory ownership: units capture map cells, each controlled cell has a value, controlled cell value generates taxes, taxes pay for new units and existing unit upkeep, and unpaid upkeep damages morale. Cities become the main spawn anchors for new units.

## Why This Is More Fun

Capturing only cities makes the map feel point-based. Capturing cells makes the whole battlefield matter. A front line can advance one cell at a time, raids can damage income without taking a city, and overbuilding an army can become risky if the player cannot tax enough territory to maintain it.

This also creates a useful strategic loop:

- Expand territory to grow tax income.
- Use taxes to buy and maintain units.
- Protect cities because they are spawn anchors.
- Avoid building more units than the economy can pay.
- Starved armies lose morale and become fragile.

## Definitions

- `Controlled cell` means a passable grid cell with an owner id.
- `Cell value` means the tax value of a grid cell.
- `Tax income` means the total value collected from controlled cells each economy tick.
- `Treasury` means spendable player resources.
- `Upkeep` means the recurring cost to maintain living units.
- `Payroll deficit` means upkeep cost is higher than available tax/treasury funding.
- `Morale debt` means morale damage caused by unpaid upkeep.
- `Spawn anchor` means an owned city that can spawn purchased units into nearby empty passable cells.

## Proposed Design Decisions

- [x] Capture passable grid cells, not blocked water cells.
- [ ] Capture only city cells and adjacent cells.
- [ ] Capture every terrain cell including blocked cells.

- [x] Infantry captures only the cell it occupies.
- [x] Tanks capture the cell they occupy plus adjacent passable cells.
- [ ] Every unit type captures the same radius.

- [x] Keep cities as spawn anchors and strategic control points.
- [ ] Remove city ownership entirely.
- [ ] Make cities only visual labels for high-value cells.

- [x] Generate taxes from controlled cells.
- [ ] Generate taxes only from cities.
- [ ] Generate taxes from units standing on cells.

- [x] Use taxes for both buying units and maintaining existing units.
- [ ] Use taxes only for buying units.
- [ ] Use taxes only for upkeep.

- [x] If upkeep cannot be paid, units lose morale first.
- [ ] If upkeep cannot be paid, units die immediately.
- [ ] If upkeep cannot be paid, production pauses only.

- [x] Spawn new units near owned cities on empty passable cells.
- [ ] Spawn units directly on any controlled cell.
- [ ] Spawn units only on the capital/home city.

## Core Model

### Cell Ownership

- [ ] Add a cell control model keyed by `GridPoint`.
- [ ] Store `OwnerId` for each passable cell.
- [ ] Store `TaxValue` for each passable cell.
- [ ] Keep blocked water cells uncapturable and untaxed.
- [ ] Initialize home city areas to each starting player.
- [ ] Initialize the rest of the map as neutral or lightly pre-owned based on scenario design.

Implementation note: prefer a separate `CellControlState` array/dictionary over mutating `GridCell` directly. `GridCell` can stay terrain/pathing truth; cell control can be match state.

### Cell Value

- [ ] Assign base value by terrain.
- [ ] Give city cells and cells near cities higher value.
- [ ] Consider road cells as moderate value because they matter strategically.
- [ ] Keep water value zero.
- [ ] Make cell value deterministic from map seed.

Suggested first values:

- Grass: 1
- Forest: 1
- Road: 2
- Hill: 2
- Rock: 1
- City cell: 8
- Adjacent-to-city cell: +2

### Cell Capture

- [ ] A living unit captures the cell it occupies.
- [ ] Infantry captures only the cell it occupies.
- [ ] Tanks capture the cell they occupy plus adjacent passable cells.
- [ ] Commander and General capture behavior still needs a decision.
- [ ] Enemy occupied cells are not captured until combat clears the defender.
- [ ] Adjacent tank capture cannot flip an enemy-occupied cell.
- [ ] Adjacent tank capture should skip blocked water cells.
- [ ] Adjacent tank capture should not pass through blocked terrain.
- [ ] Neutral cells flip faster than enemy-owned cells if capture progress is added.
- [ ] Capturing a city still changes city ownership.
- [ ] Capturing cells around a city should make the front line readable before the city flips.

First implementation option:

- [x] Capture affected cells immediately at the end of the tick.
- [ ] Add capture progress per cell immediately.

Reason: immediate capture is simple, deterministic, and makes the territory layer useful before adding more nuance. Infantry advances narrow front lines, while tanks push wider territory control.

## Economy Loop

### Tax Collection

- [ ] Add an economy interval, likely matching or replacing the current production interval.
- [ ] Compute gross tax income from controlled cells.
- [ ] Add gross tax income to player treasury.
- [ ] Exclude eliminated players.
- [ ] Exclude cells owned by eliminated players after General death cleanup.

### Upkeep

- [ ] Add upkeep cost per unit kind.
- [ ] Deduct upkeep after tax collection.
- [ ] If treasury cannot cover upkeep, compute a deficit ratio.
- [ ] Apply morale loss to unpaid units.
- [ ] Keep player treasury from going deeply negative in the first slice.

Suggested first upkeep:

- Infantry: 0.20 per economy tick
- Tank: 0.45 per economy tick
- Commander: 0.30 per economy tick
- General: 0.25 per economy tick

### Morale From Payroll

- [ ] Units fully paid keep morale stable.
- [ ] Units partially unpaid lose morale based on deficit ratio.
- [ ] Units at very low morale should later rout under the morale roadmap.
- [ ] Payroll morale should be deterministic and testable.

First implementation option:

- [x] Apply morale loss only; do not auto-kill unpaid units.
- [ ] Disband units if morale remains too low for too long.

## Buying And Spawning Units

- [ ] Replace automatic city production with purchase/spawn rules, or bridge the current production code into the new tax treasury first.
- [ ] Spend treasury to buy infantry/tanks.
- [ ] Spawn purchased units near owned cities.
- [ ] Use nearest empty passable non-city cell first.
- [ ] If no spawn cell is available, leave the purchase unspent or queue it.
- [ ] Keep single-cell occupancy.
- [ ] Keep current infantry/tank preference as the first buying policy.

Suggested first costs:

- Infantry: 2.0
- Tank: 4.5

## City Role

Cities should stay important, but not be the whole economy.

- [ ] Cities are high-value cells.
- [ ] Cities anchor spawning.
- [ ] Cities remain selectable map targets.
- [ ] Cities remain owner-colored on the map.
- [ ] City ownership changes when the city cell is controlled by a player.
- [ ] General death should neutralize owned cities and likely owned cells.

Open design call:

- [x] General death should neutralize cities.
- [ ] General death should neutralize all cells too.
  Proposed answer: yes, neutralize controlled cells as well. Otherwise defeated players would keep tax territory after losing command authority.

## UI And Feedback

- [ ] Show controlled territory on the map without making the map noisy.
- [ ] Consider a subtle territory tint per controlled cell.
- [ ] Add tax/treasury/upkeep to HUD or standings.
- [ ] Show when a player has a payroll deficit.
- [ ] Avoid drawing every cell as a heavy square unless the map remains readable.
- [ ] Update Brinell diagnostics to expose cell ownership and tax totals.

First UI option:

- [x] Add diagnostics and standings numbers before heavy map rendering.
- [ ] Immediately draw territory overlay for every cell.

Reason: the rule can be tested and balanced before committing to a dense visual overlay.

## AI Impact

- [ ] AI observations need owned cell ratio.
- [ ] AI observations need tax income.
- [ ] AI observations need treasury.
- [ ] AI observations need upkeep pressure.
- [ ] AI decisions need spending discipline.
- [ ] AI target selection should care about high-value cells and city spawn anchors.
- [ ] AI should avoid overbuilding when payroll is already weak.

## Related Roadmaps

These active phase roadmaps depend on this economy work:

- [13-phase-01-controller-interfaces.md](13-phase-01-controller-interfaces.md)
  Add tax, treasury, upkeep, owned-cell ratio, and spawn/purchase actions to controller contracts.

- [15-phase-03-morale-rout-system.md](15-phase-03-morale-rout-system.md)
  Add payroll deficit morale loss as a first-class morale source.

- [16-phase-04-commander-regions.md](16-phase-04-commander-regions.md)
  Region control should aggregate controlled cells and cell value, not only cities.

- [17-phase-05-resource-budgets.md](17-phase-05-resource-budgets.md)
  This roadmap needs the largest rewrite: taxes, treasury, upkeep, purchase, and spawn should become the foundation of resource budgets.

- [18-phase-06-general-strategy.md](18-phase-06-general-strategy.md)
  General strategy should include tax base, upkeep risk, reserve spending, and high-value territory priorities.

- [19-phase-07-visibility-scouting.md](19-phase-07-visibility-scouting.md)
  Decide whether cell ownership and tax value are fully visible or need scouting/fog rules.

- [20-phase-08-sharpneat-training.md](20-phase-08-sharpneat-training.md)
  Fitness should include tax efficiency, territory control, payroll stability, and army sustainability.

- [22-phase-10-emergent-behavior-instrumentation.md](22-phase-10-emergent-behavior-instrumentation.md)
  Add telemetry for tax raids, payroll deficits, overbuilding, territory swings, and economic collapse.

- [10-general-death-neutralizes-cities.md](10-general-death-neutralizes-cities.md)
  Extend General death cleanup from cities to controlled cells if accepted.

- [09-standings-horizontal-bars.md](09-standings-horizontal-bars.md)
  Consider whether standings need tax income, treasury, upkeep, or controlled-cell count.

- [08-city-map-ownership-ui.md](08-city-map-ownership-ui.md)
  City ownership color may need to coexist with territory overlays.

- [03-single-occupancy-combat.md](03-single-occupancy-combat.md)
  Cell capture must respect single-cell occupancy and adjacent combat.

- [02-grid-movement.md](02-grid-movement.md)
  Movement and pathing become economic because every traversed/occupied cell can affect territory control.

## Suggested Implementation Slices

### Slice 1: Core Territory State

- [ ] Add passable-cell ownership and tax values.
- [ ] Initialize cell ownership.
- [ ] Capture occupied cells each tick.
- [ ] Add core diagnostics for controlled cell counts and tax totals.
- [ ] Unit tests for capture, neutral cells, enemy cells, and blocked cells.

### Slice 2: Tax And Upkeep

- [ ] Compute tax income from controlled cells.
- [ ] Add treasury/income/upkeep fields to player snapshots or standings.
- [ ] Deduct upkeep.
- [ ] Apply morale loss on payroll deficit.
- [ ] Unit tests for paid and unpaid armies.

### Slice 3: Buying And City Spawning

- [ ] Adapt current production into treasury-funded purchases.
- [ ] Spawn near owned cities.
- [ ] Queue or skip purchase when no spawn cell is open.
- [ ] Unit tests for spawn location and spending.

### Slice 4: AI And Balance

- [ ] Add economy signals to AI observation.
- [ ] Update deterministic AI buying policy.
- [ ] Balance tax values, upkeep, and unit costs.
- [ ] Add determinism tests.

### Slice 5: UI And Docs

- [ ] Add HUD/standing values for tax, treasury, upkeep, or payroll deficit.
- [ ] Add optional territory overlay after the core rules feel good.
- [ ] Update `.my/HowToPlay.md`.
- [ ] Update `.my/UAT.md`.
- [ ] Add Brinell tests for visible UI changes.

## Test Plan

- [ ] Unit test: passable cell capture changes ownership.
- [ ] Unit test: infantry captures only its occupied cell.
- [ ] Unit test: tank captures occupied plus adjacent passable cells.
- [ ] Unit test: tank adjacent capture does not flip enemy-occupied cells.
- [ ] Unit test: blocked water cells cannot be captured or taxed.
- [ ] Unit test: tax income equals sum of controlled cell values.
- [ ] Unit test: upkeep is deducted from treasury.
- [ ] Unit test: payroll deficit lowers morale.
- [ ] Unit test: units spawn near owned cities.
- [ ] Unit test: defeated General neutralizes cities and controlled cells if that decision is accepted.
- [ ] Determinism test: same seed produces same economy fingerprint.
- [ ] Brinell UI tests only after UI/diagnostics are exposed to the player.

## Open Questions

- [ ] Should units capture only their occupied cell, or occupied plus adjacent uncontested cells?
- [x] Should tanks capture adjacent cells?
  Proposed answer: yes. Tanks capture their own cell plus adjacent passable, uncontested cells.
- [x] Should infantry capture adjacent cells?
  Proposed answer: no. Infantry captures only the cell it stands on.
- [ ] How should Commanders and Generals capture cells?
- [ ] Should cell capture be immediate or progress-based?
- [ ] Should every passable cell have a value, or only cells near cities/roads?
- [ ] Should General death neutralize all controlled cells?
- [ ] Should automatic production remain, or should all units be explicitly purchased from treasury?
- [ ] Should payroll deficit affect every unit equally, or expensive units first?
- [ ] Should morale recover when payroll becomes healthy again?
- [ ] Should territory ownership be visible as a full map overlay from the start?
