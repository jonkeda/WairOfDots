# Reserve Commands Steps

Status: implemented.

Parent roadmap: [40-command-information-neat-roadmap.md](40-command-information-neat-roadmap.md)

Previous command slice: [40c-simple-attack-orders-ai-rework-steps.md](40c-simple-attack-orders-ai-rework-steps.md)

Related production slice: [40b-city-commands-commander-production-steps.md](40b-city-commands-commander-production-steps.md)

## Goal

Make reserve movement a complete, explicit General-controlled command flow.

`40a` added reserve state and single-unit assignment/recall. `40c` made attack orders flow down the hierarchy. `40d` should make reserves useful inside that hierarchy:

- Commanders can request help or recall through reports.
- The General decides whether to assign or recall reserve units.
- Reserve assignment and recall are visible as structured command records.
- Group reserve operations exist beside single-unit operations.
- Reserve units never receive Commander-to-unit commands until assigned.

## Design Rule

Reserve authority belongs to the General.

Commanders may report:

- `NeedReinforcements`
- `ReserveRecallRequested`

Commanders must not execute:

- `AssignReserveUnit`
- `AssignReserveGroup`
- `RecallCommanderUnitToReserve`
- `RecallCommanderGroupToReserve`

No sideways transfer is allowed. Moving a unit from Commander `1` to Commander `2` must be:

```text
Commander 1 reports upward
General recalls unit to reserve
General assigns reserve unit to Commander 2
```

## 40d Scope

Implement the full first reserve command set:

General to reserve:

- `AssignReserveUnit`
- `AssignReserveGroup`
- `RecallCommanderUnitToReserve`
- `RecallCommanderGroupToReserve`

Commander to General reports:

- `NeedReinforcements`
- `ReserveRecallRequested`

Keep existing reserve visuals:

- Assigned units show their Commander number.
- Reserve units show a reserve pip.
- Reserve units have no Commander-to-unit command.

Implementation note: for this MVP, "too few assigned units" means zero assigned units. A Commander with visible pressure beyond its assigned strength or low-morale assigned units still reports `NeedReinforcements`.

## Out Of Scope

- Commander production.
- City production commands.
- Multiple active executable commands.
- Direct Commander-to-Commander transfers.
- Automatic sideways unit handoff.
- Full NEAT action outputs.

## Proposed Minor Steps

### Step 1: Document And Normalize Reserve Payloads

- [x] Define the payload fields used by all reserve commands.
- [x] Keep single-unit reserve commands compatible with existing snapshots.
- [x] Add reason codes for manual, reinforcement, recall-request, and cleanup paths.
- [x] Ensure active reserve commands are visible in snapshots and command-chain diagnostics.

Testability:

- Existing `AssignReserveUnit` tests still pass.
- Existing `RecallCommanderUnitToReserve` tests still pass.
- Command records include General source, target unit, Commander number, priority, and reason code.

### Step 2: Add AssignReserveGroup

- [x] Add `AssignReserveGroup` as a General-to-reserve command.
- [x] Select reserve units deterministically.
- [x] Prefer reserve units nearest to the target Commander.
- [x] Assign no more than the requested group size.
- [x] Emit one command record per assigned unit with command type `AssignReserveGroup`.
- [x] Do nothing when no reserve units are available.

Testability:

- A group assignment moves multiple reserve units to one Commander.
- Group assignment does not assign units owned by another player.
- Group assignment does not assign units already owned by another Commander.
- Group assignment records are deterministic for the same seed and state.

### Step 3: Add RecallCommanderGroupToReserve

- [x] Add `RecallCommanderGroupToReserve` as a General-to-reserve command.
- [x] Select assigned units deterministically from one Commander.
- [x] Prefer low-morale, low-health, or far-from-Commander units first.
- [x] Clear assignment, Commander number, scout flag, protection flag, and path.
- [x] Emit one command record per recalled unit with command type `RecallCommanderGroupToReserve`.
- [x] Do nothing when the Commander has no assigned units.

Testability:

- A group recall moves multiple assigned units into reserve.
- Recalled units have no assigned Commander id or number.
- Recalled units are visually reserve units.
- Recalled units stop following Commander commands.
- Group recall records are deterministic for the same seed and state.

### Step 4: Make Commander Reports Drive Reserve Decisions

- [x] Emit `NeedReinforcements` when a Commander has too few assigned units, visible enemy pressure, or low morale in its region.
- [x] Emit `ReserveRecallRequested` when assigned units are low morale or should be pulled back.
- [x] Keep both reports Commander-to-General only.
- [x] Do not mutate reserve state while creating reports.

Testability:

- A Commander with visible pressure and low assigned strength reports `NeedReinforcements`.
- A Commander with low-morale assigned units reports `ReserveRecallRequested`.
- Reports do not assign or recall any unit by themselves.

### Step 5: Let The General Execute Reserve Reports

- [x] On a planning tick, let the General inspect recent Commander reports.
- [x] Use available reserves to satisfy `NeedReinforcements`.
- [x] Use group recall to satisfy `ReserveRecallRequested`.
- [x] Prefer urgent reports first.
- [x] Keep execution deterministic.
- [x] Never let a Commander execute these operations directly.

Testability:

- Recent `NeedReinforcements` can produce `AssignReserveGroup`.
- Recent `ReserveRecallRequested` can produce `RecallCommanderGroupToReserve`.
- If no reserve units exist, `NeedReinforcements` creates no assignment command.
- If no assigned units exist, recall creates no recall command.
- The same seed and state produce the same reserve command records.

### Step 6: Human And Brinell Reserve Controls

- [x] Extend human commands or automation helpers for group reserve assignment.
- [x] Extend human commands or automation helpers for group reserve recall.
- [x] Keep UI text compact; selected General details may show active reserve commands.
- [x] Add command-chain diagnostics for active reserve commands grouped by player.

Testability:

- Brinell can issue or query group reserve commands.
- Selected General exposes active reserve commands.
- Reserve command diagnostics show target unit, Commander number, and reason code.

### Step 7: Update Docs And Agent Notes

- [x] Mark this roadmap implemented after the slice is complete.
- [x] Update `AGENTS.md` implemented baseline.
- [x] Update player-facing docs only if reserve group behavior becomes visible to the player.

Testability:

- Docs match implementation.
- Future agents can see that Commanders request reserve action but cannot execute it.

## First Tests

Core tests:

- `AssignReserveGroup` assigns up to the requested number of reserve units.
- `AssignReserveGroup` selects units deterministically.
- `RecallCommanderGroupToReserve` recalls up to the requested number of assigned units.
- Recalled units are reserve, have no assigned Commander, and have no path.
- Commanders can emit `NeedReinforcements`.
- Commanders can emit `ReserveRecallRequested`.
- Reports alone do not mutate assignments.
- General planning can convert `NeedReinforcements` into assignment commands.
- General planning can convert `ReserveRecallRequested` into recall commands.
- Reserve units do not receive Commander-to-unit commands.
- No command record transfers a unit directly from one Commander to another.

Brinell/UI tests:

- Command-chain diagnostics expose active reserve commands.
- Selected General displays reserve assignment and recall commands.
- Reserve unit visuals remain stable after group recall.

## Done Criteria

- The full first reserve command set exists.
- Commanders can request reserve action only through reports.
- The General can assign one reserve unit or a group.
- The General can recall one assigned unit or a group.
- Group commands are deterministic.
- Group commands produce structured command records.
- Reserve units remain outside Commander-to-unit order flow.
- Tests prove no sideways transfer exists.

## Next After 40d

- Implement `40b` city commands and Commander production if still deferred.
- Add richer selected General reserve controls.
- Feed reserve pressure and reserve command results into NEAT-style observations.
