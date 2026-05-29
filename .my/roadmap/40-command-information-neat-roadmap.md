# Command Information NEAT Roadmap

Status: planned. Updated for strict hierarchy, commander numbering, reserve assignment, rectangular regions, commander production, full command catalog, and deterministic command-and-control first.

Unit reference: [.my/UnitProperties.md](../UnitProperties.md)

## Goal

Make the command hierarchy explicit and inspectable:

- Communication is vertical only: General to Commanders, Commanders to assigned units, units back to Commanders, and Commanders back to the General.
- No sideways command flow. Commanders do not command other Commanders, and units do not command or formally report to other units.
- Each Commander has a player-local number starting at `1`.
- Each Commander marker shows its Commander number in the middle of its circle.
- Each assigned unit marker shows the number of its assigned Commander in the middle of its circle.
- Units with no assigned Commander are in reserve.
- The General can assign reserve units to Commanders.
- The General can recall assigned units back to reserve.
- Commanders cannot recall units to reserve. They can only report or request that the General recalls a unit.
- Regions are rectangles.
- A Commander can have zero or more assigned regions.
- Cities can produce infantry, tanks, and Commanders.
- The player can read the hierarchy and current intent without the map becoming noisy.
- NEAT-style controllers can later use the same command and information model as observations and actions.

## Problem

The current controller layers exist, but much of the command chain is implicit. Units move and fight, Commanders and Generals have controller interfaces, cities produce units, and telemetry records some events, but the game does not yet expose a clear command-and-report pipeline.

This makes it harder to answer:

- Which Commander owns this unit?
- Which units are in reserve?
- Which regions belong to each Commander?
- What did the General tell each Commander to do?
- Which reserve units did the General assign this tick?
- Which assigned units did the General recall to reserve?
- Which Commanders requested a reserve recall?
- What did the General ask each city to produce or protect?
- What did each Commander tell assigned units to do?
- What information did units report upward?
- What did a Commander summarize for the General?
- Which observations and actions are NEAT actually using?

## Design Decisions

- Commands and reports use a strict hierarchy. Sideways coordination must travel up through reports and back down through commands.
- A unit has one active executable command at a time.
- A Commander has one active executable directive at a time, plus zero or more assigned rectangular regions.
- Command/report history can keep multiple recent records, but only one command is current for execution unless a later slice adds an explicit queue.
- Commander numbers are stable within a match. Do not renumber living Commanders when another Commander dies.
- New Commanders receive the next unused player-local number.
- Assigned units display their Commander's number. Reserve units display a reserve pip.
- New Commanders start unassigned: no regions and no subordinate units until the General assigns them.
- Commander production cost should be `9` and upkeep should be `0.9`.
- There is no hard Commander count limit in the design, but the first useful target is two Commanders per player.
- Rectangular regions are assigned by the General rather than automatically owned by Commanders at match start.
- Use deterministic command-and-control rules first. NEAT integration should consume and emit the same records later.
- Keep command data compact and deterministic.
- Prefer structured command/report records over parsing text telemetry.
- Commands should affect behavior only after they are explicit in snapshots and tests.
- Human-vs-AI mode should show only fair information.
- AI-only mode can show omniscient command and information flow for debugging.

## Visibility Model

Start with the existing simple vision model and make it explicit in docs, snapshots, and tests:

- Units do have vision.
- Vision is player-owned and is the union of all living friendly unit sight ranges.
- Use Chebyshev grid distance so eight-way movement and sight are consistent.
- Terrain does not block vision in the first pass.
- Infantry vision: `5` cells.
- Tank vision: `6` cells.
- Commander vision: `8` cells.
- General vision: `10` cells.
- Scout role or scout directive adds `+2` cells to that unit's vision.
- Commanders and Generals have their own local vision; they are not omniscient.
- The General also receives bottom-up reports from Commanders, so the General can know summarized facts outside local sight only when they were reported through the hierarchy.
- Human-vs-AI should not reveal hidden enemy information. AI-only debug mode may show full visibility diagnostics.

## Command Vocabulary

This is the full command catalog for the roadmap. The first minor implementation proposal intentionally uses only a tiny subset; see [40a-first-command-control-vision-steps.md](40a-first-command-control-vision-steps.md). The next production-focused slice is [40b-city-commands-commander-production-steps.md](40b-city-commands-commander-production-steps.md). The behavior-first attack-order cleanup is [40c-simple-attack-orders-ai-rework-steps.md](40c-simple-attack-orders-ai-rework-steps.md), and can be implemented before `40b` if command-following behavior is the priority. Reserve group commands and General-owned reserve execution are covered in [40d-reserve-commands-steps.md](40d-reserve-commands-steps.md).

### General To Commander

- `AttackRegion`
- `DefendRegion`
- `TakeCity`
- `HoldRegion`
- `RebuildForces`
- `ProtectGeneral`
- `PressureEnemyGeneral`
- `ScoutFront`
- `FallbackToCity`

Initial payload:

- `GeneralUnitId`
- `CommanderUnitId`
- `CommanderNumber`
- `CommandType`
- `RegionIds`
- `TargetCell`
- `TargetCityId`
- `Priority`
- `ResourceBudget`
- `ExpiresOnTick`
- `ReasonCode`

### General To Reserve

- `AssignReserveUnit`
- `AssignReserveGroup`
- `RecallCommanderUnitToReserve`
- `RecallCommanderGroupToReserve`

Initial payload:

- `GeneralUnitId`
- `UnitId`
- `FromCommanderUnitId`
- `ToCommanderUnitId`
- `ToCommanderNumber`
- `Priority`
- `ExpiresOnTick`
- `ReasonCode`

The General can move units from reserve to a Commander or recall units from a Commander to reserve. Commanders can request a recall through reports, but they cannot execute the recall themselves and they do not directly transfer units to another Commander.

### General To City

- `ProduceInfantry`
- `ProduceTank`
- `ProduceCommander`
- `HoldProduction`
- `ReserveTreasury`
- `ReinforceCommander`
- `DefendCity`
- `RecoverPayroll`

Initial payload:

- `GeneralUnitId`
- `CityId`
- `CommandType`
- `UnitKind`
- `Budget`
- `Priority`
- `RallyCommanderUnitId`
- `RallyCell`
- `ExpiresOnTick`
- `ReasonCode`

New Commanders start with a stable Commander number, zero assigned regions, and no subordinate units until the General assigns them.

### Commander To Unit

- `AdvanceToCell`
- `AttackTarget`
- `DefendCell`
- `CaptureTerritory`
- `ProtectLeader`
- `ScoutArea`
- `RetreatToCommander`
- `RallyNearLeader`

Initial payload:

- `CommanderUnitId`
- `CommanderNumber`
- `UnitId`
- `CommandType`
- `TargetCell`
- `TargetUnitId`
- `TargetCityId`
- `Priority`
- `ExpiresOnTick`
- `ReasonCode`

Commanders only command units assigned to them. Reserve units do not receive Commander-to-unit orders.

## Region Model

Regions should be deterministic rectangles:

- `RegionId`
- `BoundsX`
- `BoundsY`
- `Width`
- `Height`
- `OwnerPlayerId`
- `AssignedCommanderUnitId`
- `AssignedCommanderNumber`
- `Priority`

A Commander may have:

- Zero regions, usually just created, rebuilding, protecting the General, or waiting for assignment.
- One region, the normal simple case.
- Multiple regions, useful for a strong Commander or a player with too few Commanders.

Region assignment is a General-level command. Commanders can report that a region is too large, collapsing, stable, or no longer useful, but they do not assign regions to each other.

## Information Layers

### Unit To Commander Reports

Units report local tactical information upward to their assigned Commander.

Candidate report types:

- `EnemySighted`
- `EnemyLeaderSighted`
- `UnderAttack`
- `LowHealth`
- `LowMorale`
- `BlockedPath`
- `CapturedCell`
- `ReachedTarget`
- `RequestRally`
- `RequestProtection`
- `RequestReserveRecall`

Initial payload:

- `Tick`
- `UnitId`
- `CommanderUnitId`
- `CommanderNumber`
- `ReportType`
- `Cell`
- `EnemyUnitId`
- `EnemyPlayerId`
- `HealthRatio`
- `Morale`
- `Confidence`
- `Urgency`

Reports should be capped and sorted deterministically so AI-only mode and Brinell tests stay stable.

### Commander To General Reports

Commanders summarize their assigned rectangular regions and forward important reports to the General.

Candidate report types:

- `RegionStable`
- `RegionContested`
- `FrontAdvancing`
- `FrontCollapsing`
- `NeedReinforcements`
- `PayrollPressure`
- `EnemyGeneralOpportunity`
- `CommanderThreatened`
- `CityThreatened`
- `ScoutDiscovery`
- `ReserveRecallRequested`
- `RegionAssignmentRequested`

Initial payload:

- `Tick`
- `CommanderUnitId`
- `CommanderNumber`
- `GeneralUnitId`
- `RegionIds`
- `ReportType`
- `TargetCityId`
- `EnemyPressure`
- `FriendlyStrength`
- `MoraleRisk`
- `TaxValueAtRisk`
- `ReserveUnitsRequested`
- `ReserveRecallUnitIds`
- `RecommendedDirective`
- `Urgency`

The General should use these summaries instead of scanning every unit directly once the command model is mature.

## Visualization Plan

### Normal Mode

Keep Normal mode readable:

- Show Commander numbers in Commander circles.
- Show assigned Commander numbers in unit circles.
- Show reserve units with a reserve pip.
- Show selected unit details.
- Show selected city ownership and target details.
- Do not draw all command lines by default.

### Command Visualization

Lines are not the primary visualization. Prefer compact symbols attached to the units and leaders:

- Circle center: Commander number for Commanders and assigned units.
- Reserve units: small reserve pip.
- Circle rim or small pip: active command icon.
- Commander ring: region count or region-assignment state.
- Selected unit/Commander HUD: full current command payload.
- Selected General HUD: Commander commands, city commands, reserve assignments, and top reports.

Command lines should be reserved for selected objects or AI debug:

- Selected General can show General-to-Commander or General-to-city links.
- Selected Commander can show links to assigned units.
- AI Debug can show temporary lines for command review and screenshots.

### AI Debug Overlay

AI Debug can show more detail:

- Reason codes and priorities for selected player or selected leader.
- NEAT genome id, archetype, observation vector summary, and action output summary.
- Recent command/report feed grouped by player and layer.
- Visibility diagnostics, including visible cell count and visible enemies.

## NEAT Usage

NEAT-style controllers should use the command/report model directly, but only after deterministic command-and-control is working.

### General NEAT Observation Inputs

Candidate inputs:

- Treasury
- Tax income
- Upkeep
- Payroll deficit ratio
- Controlled cells
- Owned cities
- Spawn capacity
- General security
- Commander count
- Reserve unit count
- Commander region summaries
- Enemy leader visibility
- Reports by urgency band
- Recent territory swing
- Current strategic command success/failure

### General NEAT Action Outputs

Candidate outputs:

- Commander objective choice
- Region assignment choice
- Reserve assignment choice
- Target city choice
- City production priority
- Commander production priority
- Resource budget ratio
- Risk tolerance
- General relocation intent
- Scout emphasis

### Commander NEAT Observation Inputs

Candidate inputs:

- General command type
- Assigned region summaries
- Assigned unit count
- Nearby reserve availability summary from General reports
- Friendly strength
- Enemy pressure
- Nearby low morale count
- Nearby low health count
- Unit reports by type
- City threat level
- Current target distance
- Local tax value at risk

### Commander NEAT Action Outputs

Candidate outputs:

- Unit command choice
- Reserve-recall request
- Attack/hold/retreat balance
- Protection detail request
- Scout assignment
- Rally location
- Tactical target cell or city
- Report urgency to General

### Unit NEAT Observation Inputs

Candidate inputs:

- Commander command type
- Distance to target
- Own health ratio
- Own morale
- Adjacent enemies
- Nearby friends
- Terrain cost
- Cell ownership
- Leader proximity
- Assigned Commander number presence

### Unit NEAT Action Outputs

Candidate outputs:

- Move intent
- Attack preference
- Capture preference
- Retreat preference
- Rally request
- Report emphasis

## Data Model Slices

### Slice 1: Commander Numbers And Reserve State

- Add stable player-local Commander numbers.
- Add optional assigned Commander fields to units.
- Treat units without an assigned Commander as reserve.
- Include Commander number and reserve state in snapshots and diagnostics.
- Render Commander/unit center labels.
- Add core tests for numbering, assignment, death, and non-renumbering.

### Slice 2: Rectangular Regions

- Replace placeholder/front-like region assignment with deterministic rectangular regions.
- Allow zero or more region assignments per Commander.
- Add region assignment snapshots with rectangle bounds.
- Add tests for region rectangle coverage, region ownership summaries, and multi-region Commanders.

### Slice 3: Explicit Command And Report Records

- Add core records for General commands, reserve assignment commands, city commands, Commander commands, unit reports, and Commander reports.
- Add deterministic ordering and history caps.
- Include records in `MatchSnapshot` or dedicated diagnostics responses.
- Add unit tests for record creation and ordering.

### Slice 4: Deterministic Command Behavior

- Convert current strategic decisions into explicit General command records.
- Convert tactical assignments into explicit Commander-to-unit command records.
- Make units prefer their active assigned command when choosing movement and targets.
- Let low morale, blocked path, immediate threat, and rout state override a command.
- Add focused simulation tests for reserve assignment and General-only reserve recall.

### Slice 5: Production Of Commanders

- Add Commander production as a city command and treasury-funded purchase.
- Assign new Commanders the next player-local number.
- Spawn new Commanders near owned cities when space is available.
- Add tests for cost, spawn blocking, numbering, and initial zero-region state.

### Slice 6: Command Visualization

- Add center labels and command pips in `MapSpriteRenderer`.
- Keep text out of the map except small center labels and selected/debug HUD summaries.
- Add Brinell diagnostics and screenshot smoke coverage for labels, reserve pips, and selected command details.

### Slice 7: NEAT Integration

- Feed command/report records into General, Commander, and Unit observation vectors.
- Emit NEAT action outputs as explicit commands.
- Add tests proving controller outputs are visible in command diagnostics.
- Keep deterministic genome IDs until real SharpNEAT integration is chosen.

## Acceptance Criteria

- Commanders have stable per-player numbers starting at `1`.
- Commander circles show their number.
- Assigned unit circles show their Commander number.
- Reserve units are visually distinguishable.
- Units can be assigned from reserve to a Commander by the General.
- Units can be recalled from a Commander to reserve by the General.
- Commanders cannot recall units to reserve directly.
- Regions are deterministic rectangles.
- Commanders can have zero or more assigned regions.
- Commander production exists beside infantry and tank production.
- Current General-to-Commander intent is visible as structured command data.
- Current production intent is visible as General-to-city command data.
- Current Commander-to-unit tactical intent is visible as structured command data.
- Units can report local information to Commanders.
- Commanders can summarize region information to Generals.
- Command and report histories are capped and deterministically ordered.
- Human-vs-AI mode does not reveal unfair hidden information.
- AI-only mode can inspect the full command/report flow.
- NEAT observation/action paths can use the same command/report records as diagnostics.

## Do Not Do Yet

- Do not add real SharpNEAT package integration just to finish this roadmap.
- Do not let Commanders directly command other Commanders.
- Do not let Commanders recall units to reserve; recall is a General command only.
- Do not let units directly coordinate through sideways command records.
- Do not add multiple active executable commands per unit until the single-command model is proven.
- Do not add permanent long text labels over every unit.
- Do not make command lines the default map visualization.
- Do not expose hidden enemy or fog information in human mode.
- Do not replace the current renderer order with UI playfield controls.
- Do not let command/report history grow without caps.

## Answered Questions

- Can units and Commanders have multiple commands?
  Proposed answer: one active executable command each. Keep multiple recent records only as history, reports, or future queued commands.
- Do units have vision, and how far?
  Proposed answer: yes. Start with current Chebyshev radii: infantry `5`, tank `6`, scout bonus `+2`.
- Do Commanders and Generals have vision?
  Proposed answer: yes. Start with Commander `8` and General `10`. They are not omniscient.
- How does vision work?
  Proposed answer: a player's visible area is the union of friendly unit sight ranges. Terrain does not block sight in the first pass. Reports can carry summarized information upward, but hidden enemy details stay hidden in human mode.
- Should we start with a simple small set of commands?
  Proposed answer: yes. Start with the minimal subset in `40a`, then expand into the full catalog in this roadmap.
- How should commands be visualized?
  Proposed answer: use Commander numbers, reserve pips, and small command pips/icons on circles. Use lines only for selected objects or AI debug.
- Should deterministic command-and-control come before NEAT integration?
  Proposed answer: yes. First make hierarchy, assignment, commands, reports, and visuals deterministic and testable. Then let NEAT consume and emit those same records.

## Additional Resolved Questions

- What should Commander production cost and upkeep be?
  Answer: Commander production cost is `9` and upkeep is `0.9`.
- Should the reserve marker be `R`, an empty center, or a small reserve pip?
  Answer: use a reserve pip.
- Should a new Commander have a default protection detail, or start completely unassigned?
  Answer: start completely unassigned.
- What is the maximum useful number of Commanders per player for the local MVP?
  Answer: no hard design limit; begin with two Commanders per player.
- Should rectangular regions cover the whole map at match start, or only claimed/contested areas?
  Answer: rectangular regions are assigned by the General to Commanders.

## Implementation Details To Decide Later

- Should Commander cost or upkeep be rebalanced after Commander production is playable?
- What shape and color should the reserve pip use so it remains readable against all player colors?
- Should the first implementation seed two Commanders per player immediately, or number the existing Commander first and add the second Commander in the next PR?
