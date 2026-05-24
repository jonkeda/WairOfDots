# Dev Prompt Questions

Source target: `prompts.md`

Note: `prompts.md` was not present in this checkout when this file was drafted. The questions below are based on the project direction and comments supplied on 2026-05-24.

## Grounding Comments

- The game is single human, multi AI players.
- The AI players will use different NEATs.
- Brinell should be used for UI testing.
- Oravey should be used as a reference for Stride game technology and Brinell integration only. Its game mechanics are not copyable.

## Questions For `prompts.md`

Each question has clickable proposed answers. Checked items are the recommended defaults.

### Player Model

#### Does the human control one player entity while all other players are AI controlled?

- [X] Yes. The human controls one player entity, and every other active player slot is AI controlled.
- [ ] No. The human controls a team or faction made of several units.
- [ ] No. The first implementation should support multiple human players.

#### Are AI players competitors, collaborators, neutral actors, or a mix?

- [X] AI players are competitive by default, with temporary tactical interactions allowed later if the design needs them.
- [ ] AI players are permanent allies of the human.
- [ ] AI players are neutral simulation actors only.

#### How many AI players should a standard match/session include?

- [X] A standard session uses 1 human plus 4 AI players.
- [ ] A standard session uses 1 human plus 2 AI players.
- [ ] A standard session uses 1 human plus 8 or more AI players.

#### Should the number of AI players be fixed, configurable, or scenario-driven?

- [X] AI player count is scenario-configurable with a sensible default.
- [ ] AI player count is hard-coded.
- [ ] AI player count is fully dynamic during a session.

#### Can the human join, leave, pause, or restart while AI players continue to run?

- [X] Pause freezes the full simulation, including AI. Restart resets the session deterministically.
- [ ] AI keeps running while the human is paused.
- [ ] Human join/leave is supported during an active session.

#### Does the game need local-only play first, or should the prompts leave room for future networked multiplayer?

- [X] Build local single-player first, while keeping clean boundaries for possible network play later.
- [ ] Design networked multiplayer from the first milestone.
- [ ] Treat networking as permanently out of scope.

### AI And NEAT Design

#### What does each AI player's NEAT control?

- [X] NEAT controls high-level strategy and tactical action selection; deterministic systems handle pathfinding, physics, collision, and rule enforcement.
- [ ] NEAT directly controls every low-level input every frame.
- [ ] NEAT only chooses cosmetic personality flavor.

#### Does "different NEATs" mean different trained genomes, different NEAT configurations, different fitness functions, or separate species/archetypes?

- [X] Different NEATs means separate genomes/archetypes using a shared input-output contract, with configurable fitness weights per archetype.
- [ ] Different NEATs means one shared genome with random noise per AI.
- [ ] Different NEATs means fully unrelated implementations per AI player.

#### Should each AI player's NEAT be seeded deterministically so sessions can be reproduced in tests?

- [X] Yes. Every AI player uses deterministic seeds for reproducible sessions and tests.
- [ ] No. AI seeds are random-only.
- [ ] Not initially. Deterministic AI is added after the prototype.

#### Should NEATs evolve during a session, between sessions, or only during offline training?

- [X] NEAT evolution happens in offline training or between-session training modes first. Human-facing matches use locked genomes.
- [ ] NEATs evolve live during every match.
- [ ] NEAT is implemented only as a future placeholder.

#### What telemetry should be captured for NEAT fitness, debugging, and balancing?

- [X] Capture observations, chosen actions, rewards, fitness, genome id, seed, outcome, and key state snapshots.
- [ ] Capture only final win/loss results.
- [ ] Avoid telemetry until balancing starts.

#### How should prompts describe AI personality without hard-coding non-NEAT behavior that bypasses learning?

- [X] AI personality emerges from genome selection, reward shaping, and fitness weights, not hard-coded behavior overrides.
- [ ] AI personality is hard-coded with scripts.
- [ ] AI personality is random labels only.

#### What constraints keep AI behavior readable and fair for the human player?

- [X] Keep AI fair through perception-limited inputs, reaction throttles, shared rules, deterministic replay, and visible feedback.
- [ ] Let AI read complete hidden game state.
- [ ] Balance AI only by reducing its stats.

### Game Loop And State

#### What is the core objective for the human player?

- [X] Control dots/territory, score efficiently, and survive pressure from AI players.
- [ ] Play open-ended sandbox survival.
- [ ] Progress through a story questline.

#### What is the core objective for each AI player?

- [X] AI players pursue the same match objective as the human, with different NEAT-driven styles.
- [ ] AI players have unrelated objectives.
- [ ] AI players only obstruct the human without trying to win.

#### Do all players share the same win condition?

- [X] All players share the same win condition for the first playable version.
- [ ] Each AI has a separate asymmetric win condition.
- [ ] The human has a unique win condition.

#### Should the world update in real time, turns, ticks, or a hybrid loop?

- [X] Use a fixed-tick simulation for deterministic gameplay, with rendering decoupled from simulation updates.
- [ ] Use fully frame-rate-dependent real-time simulation.
- [ ] Use pure turn-based updates.

#### What game state must be exposed for AI decision-making?

- [X] Expose normalized, perception-limited state: nearby dots/entities, owned resources, threats, scores, cooldowns, and legal actions.
- [ ] Expose the full world state to AI.
- [ ] Expose only raw pixels to AI in the first implementation.

#### What game state must be hidden from AI players to avoid unfair omniscience?

- [X] Hide information the human could not reasonably know: unexplored areas, hidden opponent intent, future spawns, and private opponent internals.
- [ ] Hide nothing from AI.
- [ ] Hide all opponent state, even visible nearby state.

#### What parts of the simulation must be deterministic for replay and testing?

- [X] Deterministic replay covers seeds, tick order, player inputs, AI observations, AI actions, scoring, and win/loss results.
- [ ] Only UI navigation needs to be deterministic.
- [ ] Replay waits until after feature-complete gameplay.

### Stride Technology Direction

#### Which Stride version should be targeted?

- [X] Target the same modern Stride line already proven by Oravey unless compatibility requires a pinned version.
- [ ] Pick a Stride version independently without checking Oravey.
- [ ] Avoid Stride-specific decisions until late in development.

#### Which project layout should be used for the game, core logic, platform launcher, content, and tests?

- [X] Use a layered layout: pure game logic, Stride game/core integration, Windows launcher, content/assets, unit tests, and Brinell UI tests.
- [ ] Put all code in one game project.
- [ ] Split every feature into separate assemblies from the start.

#### Which systems should stay pure C# and which should be Stride-specific components or processors?

- [X] Keep rules, AI contracts, NEAT evaluation, scoring, and deterministic simulation in pure C#; keep rendering, input, audio, scene setup, and UI automation hosting in Stride-facing code.
- [ ] Put all gameplay logic directly in Stride scripts.
- [ ] Keep Stride only as a final renderer around an unrelated app.

#### What assets, scene setup, rendering, input, audio, and save/load architecture should the prompts require?

- [X] First milestone requires minimal real assets, scene setup, player/AI spawn, input, HUD, restart, deterministic seed, and saveable settings.
- [ ] Start with asset-heavy production content.
- [ ] Start with backend logic only and no playable scene.

#### Should the initial implementation favor a minimal playable loop or a broader architecture skeleton?

- [X] Favor a minimal playable loop plus architecture skeleton.
- [ ] Build the full architecture before the first playable loop.
- [ ] Build a throwaway prototype with no reusable structure.

### Brinell UI Testing

#### Which UI and gameplay surfaces need Automation IDs from the first implementation pass?

- [X] Add Automation IDs to start menu, settings, seed entry, player count selector, start/restart buttons, HUD score/state labels, pause overlay, and end-state UI.
- [ ] Add Automation IDs only after UI polish.
- [ ] Add Automation IDs only to menus.

#### Should Brinell tests drive only menus and HUD, or also gameplay input and world-state queries?

- [X] Brinell drives menus, HUD checks, gameplay input, deterministic restart, and world-state test queries where useful.
- [ ] Brinell tests menus only.
- [ ] Brinell is replaced with manual testing for gameplay.

#### What named pipe, automation server, or Brinell.Stride setup should be required by default?

- [X] Use Brinell.Stride with a hosted automation server, stable pipe/endpoint configuration, page objects, fixtures, and game-specific query commands.
- [ ] Use ad hoc input simulation without Brinell page objects.
- [ ] Use only screenshots for UI testing.

#### What smoke tests should exist before feature work is considered complete?

- [X] Required smoke tests: app launches, menu starts game, fixed seed reproduces setup, human input changes state, AI tick advances, pause freezes, restart resets, and win/loss screen appears.
- [ ] Smoke tests only verify launch.
- [ ] Smoke tests wait until beta.

#### Which tests should verify deterministic AI behavior and seeded NEAT runs?

- [X] AI tests verify seeded NEAT identity, deterministic action traces for short runs, no illegal actions, and stable match outcome for fixture seeds.
- [ ] AI tests only verify that AI entities exist.
- [ ] AI tests do not inspect deterministic behavior.

#### Should every prompt include explicit acceptance criteria with matching Brinell UI test expectations?

- [X] Yes. Every implementation prompt includes acceptance criteria and matching Brinell expectations when UI or gameplay behavior changes.
- [ ] Brinell tests are optional per prompt.
- [ ] Acceptance criteria are prose-only.

### Oravey Reference Boundary

#### Which Oravey technical patterns are approved references?

- [X] Approved references: Stride bootstrap style, project layering, automation handler pattern, Brinell fixture/page-object approach, deterministic test hooks, and CI-style test commands.
- [ ] Approved references include gameplay loops, content, quests, combat, or world mechanics.
- [ ] Oravey should not be referenced at all.

#### Which Oravey mechanics are explicitly out of bounds so the new game remains original?

- [X] Out of bounds: Oravey mechanics, world/content, quests, combat model, survival systems, town/map gameplay, names, and scenario structure.
- [ ] Only names and content are out of bounds.
- [ ] Nothing is out of bounds if code is rewritten.

#### Should prompts cite Oravey file paths for technical examples, or describe the pattern without direct code transfer?

- [X] Prompts may cite Oravey file paths for technical examples, but must say the reference is for technology and Brinell integration only.
- [ ] Prompts should copy Oravey technical code directly by default.
- [ ] Prompts should avoid file references entirely.

#### How should developers verify that borrowed ideas are limited to technology and Brinell integration?

- [X] Developers list borrowed technical patterns and explicitly confirm no Oravey mechanics/content were copied in the done notes.
- [ ] Developers verify only by running tests.
- [ ] No verification is needed if implementation names differ.

### Prompt Format And Developer Workflow

#### Should `prompts.md` be organized by milestones, features, systems, or implementation phases?

- [X] Organize `prompts.md` by milestones, then feature prompts inside each milestone.
- [ ] Organize `prompts.md` as one long unsorted backlog.
- [ ] Organize `prompts.md` strictly by source folder.

#### Should each prompt include intent, constraints, files to inspect, implementation steps, tests, and done criteria?

- [X] Each prompt includes intent, constraints, files/docs to inspect, implementation steps, tests, Brinell expectations, AI/NEAT impact, and done criteria.
- [ ] Each prompt includes only a high-level feature description.
- [ ] Each prompt includes implementation code snippets as the main artifact.

#### Should prompts require reading existing repo docs before coding?

- [X] Prompts require reading relevant local docs and Oravey/Brinell references before coding.
- [ ] Prompts rely on memory of the reference projects.
- [ ] Prompts avoid local docs to move faster.

#### Should prompts forbid copying Oravey mechanics directly?

- [X] Prompts explicitly forbid copying Oravey gameplay mechanics.
- [ ] Prompts leave the Oravey boundary implicit.
- [ ] Prompts allow mechanics copying if tests pass.

#### Should prompts require running Brinell tests before marking UI-related work complete?

- [X] UI/gameplay prompts require Brinell tests or a documented reason they could not be run.
- [ ] Brinell tests are required only before releases.
- [ ] Manual testing is enough for UI/gameplay prompts.

#### Should prompts include a standard "AI/NEAT impact" section for any gameplay change?

- [X] Every gameplay prompt includes an AI/NEAT impact section covering observations, actions, rewards, determinism, and telemetry.
- [ ] AI/NEAT impact is handled only by dedicated AI prompts.
- [ ] AI/NEAT impact can be inferred from implementation details.

## Suggested Prompt Rules

- Treat the game as one human player against or alongside multiple AI players unless a prompt explicitly says otherwise.
- Make AI-player behavior NEAT-driven where practical, with clear boundaries around deterministic seeds, telemetry, and testability.
- Add Brinell UI testing hooks and tests as part of feature completion, not as a later polish phase.
- Use Oravey as a Stride and Brinell integration reference only; do not copy Oravey gameplay systems, content, objectives, or mechanics.
