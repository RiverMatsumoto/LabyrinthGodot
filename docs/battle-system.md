# Battle System

This document explains how the battle system's pieces cooperate. The shortest
mental model is:

1. `Battle` coordinates Godot UI and the surrounding game.
2. `BattleLogic` controls which battle interaction is currently legal.
3. `BattleRepo` owns and mutates battle state.
4. `BattlePresenter` plays domain-produced cues and acknowledges completion.

## System Map

PlantUML sources:

- [Component relationships](diagrams/battle-system-components.puml)
- [Battle and turn sequence](diagrams/battle-turn-sequence.puml)
- [ReactiveEffect operation flow](diagrams/battle-reactive-effect-flow.puml)
- [Generated logic state diagram](../src/battle/state/BattleLogicState.g.puml)

## Responsibilities

| Component | Owns | Does not own |
| --- | --- | --- |
| [`Battle`](../src/battle/Battle.cs) | Scene wiring, command controls, game/party integration, presenter callbacks | Combat rules or authoritative battle state |
| [`BattleLogic`](../src/battle/BattleLogic.cs) | LogicBlocks inputs, outputs, and state transitions | HP, TP, statuses, operation ordering, or cue timing |
| [`BattleRepo`](../src/battle/domain/BattleRepo.cs) | Runtime units, commands, turns, effects, reactive effects, outcomes, operation queue | Godot controls or frame-based playback |
| [`BattlePresenter`](../src/battle/battle_presenter/BattlePresenter.cs) | Battle UI controls, command presentation, cue timing, and temporary cue UI | Battle mutations or state transitions |
| [`BattleContentResource`](../src/battle/resources/BattleContentResource.cs) | Godot-authored actions, statuses, reactive effects, encounters, enemies, and equipment | Runtime battle state |
| `GameLogic` / `GameRepo` | Entering battle, requested encounter, seed, and return mode | Battle resolution |
| `PartyRepo` | Persistent party members and post-battle HP/TP | Temporary enemy or status state |

`BattleLogic` and `BattleRepo` both expose state, but for different purposes:

- The logic state says which input the application may send next.
- `BattleDomainPhase` protects the domain API and records resolver progress.

They should move together through command selection, resolution, cue playback,
and completion.

## Core Contracts

The main contracts are in
[`BattleContracts.cs`](../src/battle/domain/BattleContracts.cs) and
[`BattleDefinitions.cs`](../src/battle/domain/BattleDefinitions.cs).

| Contract | Meaning |
| --- | --- |
| `BattleSetup` | Complete immutable input needed to start one battle |
| `BattleCommand` | One actor's selected action and optional target |
| `BattleSnapshot` | Read-only view used by UI and enemy planning |
| `BattleAdvance` | The next boundary reached by resolution |
| `BattleCue` | View instruction such as animation, popup, status, wait, or death |
| `BattleResult` | Outcome, rewards, return mode, and final player HP/TP |

`BattleAdvance.Kind` has three meanings:

- `CommandRequired`: resolution has returned to player command selection.
- `CuePlaybackRequired`: the view must play `Cues` and acknowledge
  `CueBatchId`.
- `Completed`: the battle has produced a `BattleResult`.

## Battle Lifecycle

### Entering

1. A game state sends `GameLogicState.Input.EnterBattle`.
2. The current game state stores a `BattleRequest` in `GameRepo` and enters
   `GameLogicState.Battle`.
3. `Battle` observes that state entry and calls `StartRequestedBattle`.
4. It resolves the encounter from compiled content and converts party members
   to `BattleBattlerSeed` values.
5. It sends the resulting `BattleSetup` to `BattleLogic.StartBattle`.
6. `BattleRepo.Start` creates runtime units, resets resolver state, and enters
   `SelectingCommands`.
7. `BattleLogic` emits `CommandRequested` for the first living player.

### Selecting Commands

`Battle` builds action and target controls from `BattleSnapshot` and
`BattleCatalog`.

When the player confirms:

1. `Battle` creates a `BattleCommand`.
2. `SelectingCommands` calls `BattleRepo.ValidateCommand`.
3. Invalid commands emit `CommandRejected`.
4. Valid commands are stored by actor.
5. If another living player needs a command, `CommandRequested` is emitted.
6. Undo removes the most recently submitted player command.

After all living players have commands, `BattleRepo.BeginResolution` asks the
`IEnemyCommandPlanner` for each living enemy's command.

Commands are ordered by:

1. Action priority, descending.
2. Actor agility, descending.
3. Actor ID, ordinal ascending, as a deterministic tie-breaker.

### Resolving a Turn

`BeginResolution` seeds the operation queue in this order:

1. Turn-start reactive effect trigger.
2. One action operation per ordered command.
3. Turn-end reactive effect trigger.
4. Deferred end-of-turn reactive effects.
5. Status expiration and status-reactive effect removal.
6. Turn completion.

`BattleRepo.AdvanceResolution` executes operations synchronously until it
reaches an external boundary:

- A `CueOperation` returns `CuePlaybackRequired`.
- `FinishTurnOperation` returns `CommandRequired`.
- A terminal outcome returns `Completed`.

An action expands into `ActionStarted`, effect operations, and
`ActionFinished`. Effects emit `BeforeEffect` and `AfterEffect`; mutations may
also emit damage, healing, defeat, and status events.

ReactiveEffects carry source, target, action, status, and stack metadata. Their
typed conditions are AND-combined. Matches are ordered by priority and
registration order, then scheduled as `Immediate`, `AfterCurrentAction`, or
`EndOfTurn`. Cause guards and `BattleRepo.MaxReactiveEffectDepth` bound recursion.

Innate party/enemy reactive effects register at battle start. Status reactive effects exist
only while their status exists. Register reactive effect effects add catalog
reactive effects dynamically.

### Cue Playback Handshake

The resolver and presenter use a strict pause/acknowledge loop:

1. `BattleRepo.AdvanceResolution` encounters a `CueOperation`.
2. It changes phase to `AwaitingCuePlayback` and returns a
   `BattleAdvance` containing a unique `CueBatchId`.
3. `ResolvingTurn` emits `CuePlaybackRequested` and enters
   `AwaitingCuePlayback`.
4. `Battle` passes the advance to `BattlePresenter.Play`.
5. The presenter plays each cue in order.
6. Its completion callback calls
   `BattleLogic.AcknowledgeCuePlayback(CueBatchId)`.
7. The repository verifies the ID, returns to `ResolvingTurn`, and the scene
   requests the next advance.

Important invariants:

- Resolution cannot advance while a cue batch is awaiting acknowledgement.
- Only the currently awaited `CueBatchId` is accepted.
- The presenter never mutates battle state.
- Domain mutations may occur between cue batches, so the UI refreshes when a
  batch is requested.

### Completing

Victory, defeat, or fleeing creates a `BattleResult` and enters `Completed`.
`BattleLogic` emits `BattleCompleted`.

`Battle` then:

1. Writes final player HP/TP to `PartyRepo`.
2. Cancels presentation and hides the battle scene.
3. Sends the appropriate game-state input using the result's return mode.
4. Overrides the return mode with `MainMenu` after defeat.

Rewards are carried by `BattleResult`; this system currently does not apply
experience, currency, or items.

## Authored Content

Godot resources under
[`src/battle/resources`](../src/battle/resources) are editor-facing data.
`BattleContentResource.Compile` converts them into domain definitions and
validates duplicate IDs and references.

The main distinction is:

- Resource types are mutable Godot authoring objects.
- Definition types are compiled battle content.
- Runtime types in `BattleRepo` are state for one active battle.

`Battle` uses compiled project content when assigned. Its fallback catalog and
debug party exist only to keep a development battle runnable without authored
content.

See [Battle Authoring](battle-authoring.md) for every resource field, enemy
placement, affinities, reactive effects, and creation workflows.

## Where to Make Changes

| Goal | Primary locations |
| --- | --- |
| Add an action using existing effects | Battle `.tres` content and resource classes |
| Add a new effect type | Effect definition, matching resource compiler, and `BattleRepo.BuildEffectOperations` |
| Add status behavior | Status resource plus catalog reactive effects/effects |
| Change enemy decisions | Implement and inject `IEnemyCommandPlanner` |
| Add a visual/audio instruction | Add a `BattleCue`, produce it in resolution, handle it in `BattlePresenter` |
| Change command-selection flow | `BattleLogicState.Input`, state records, outputs, and `Battle` bindings |
| Change combat rules or ordering | `BattleRepo` and focused repository tests |
| Change battle controls/layout | `Battle.tscn`, `Battle.cs`, and presenter scene/code |
| Change entry or return behavior | `GameLogicState`, `GameRepo`, and `Battle.FinishBattle` |

Keep domain mutations inside `BattleRepo`. Prefer emitting a cue instead of
calling Godot presentation code from the resolver.

## Tests

- [`BattleLogicTest`](../test/src/battle/BattleLogicTest.cs) verifies the
  command-resolution-cue acknowledgement state flow.
- [`BattleRepoTest`](../test/src/battle/domain/BattleRepoTest.cs) verifies command
  validation, enemy planning, operation ordering, effects, rows, statuses,
  reactive effects, and cue sequencing.

When changing a domain rule, add a repository test. When changing which input
or output occurs next, add a logic test.
