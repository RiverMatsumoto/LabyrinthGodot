# Battle Authoring

Battle content is authored as Godot resources and compiled into immutable
domain definitions before a battle starts.

- [`BattleContentResource`](../src/battle/resources/catalog/BattleContentResource.cs)
  is the root catalog.
- [`BattleDefinitions.cs`](../src/battle/domain/BattleDefinitions.cs) contains
  the compiled contracts.
- [`BattleRepo`](../src/battle/domain/BattleRepo.cs) executes the definitions.

## Root Content

[`BattleContent.tres`](../src/battle/resources/BattleContent.tres) contains:

| Field | Contents |
| --- | --- |
| `Actions` | Unique `BattleActionResource.Id` values |
| `Statuses` | Unique `BattleStatusResource.Id` values |
| `ReactiveEffects` | Unique `BattleReactiveEffectResource.Id` values |
| `Encounters` | Unique `BattleEncounterResource.Id` values |
| `Equipment` | Unique `BattleEquipmentResource.Id` values |

Compilation rejects duplicate IDs and references to missing actions, statuses,
or reactive effects.

## Actions

[`BattleActionResource`](../src/battle/resources/actions/BattleActionResource.cs):

| Field | Meaning |
| --- | --- |
| `Id` | Stable action ID used by battlers and reactive effect conditions |
| `DisplayName` | UI name; falls back to `Id` |
| `TargetRule` | Self, one target, one row, or all targets |
| `TpCost` | TP removed when execution begins |
| `Priority` | Primary command-order key, descending |
| `Range` | Melee/ranged modifier input |
| `RetargetPolicy` | Fail or choose the nearest valid target |
| `Effects` | Ordered effect list |

Effects execute in authored order:

| Resource | Fields |
| --- | --- |
| [`DamageBattleEffectResource`](../src/battle/resources/effects/DamageBattleEffectResource.cs) | Damage type, fixed/stat mode, power, crit settings, animation, optional status-stack scaling |
| [`HealBattleEffectResource`](../src/battle/resources/effects/HealBattleEffectResource.cs) | Amount, animation, optional status-stack scaling |
| [`ModifyResourceBattleEffectResource`](../src/battle/resources/effects/ModifyResourceBattleEffectResource.cs) | HP/TP, signed amount, optional status-stack scaling |
| [`ApplyStatusBattleEffectResource`](../src/battle/resources/effects/ApplyStatusBattleEffectResource.cs) | Status ID, stacks, duration override, base chance |
| [`RemoveStatusBattleEffectResource`](../src/battle/resources/effects/RemoveStatusBattleEffectResource.cs) | Status ID |
| [`AnimationBattleEffectResource`](../src/battle/resources/effects/AnimationBattleEffectResource.cs) | Animation ID and wait flag |
| [`WaitBattleEffectResource`](../src/battle/resources/effects/WaitBattleEffectResource.cs) | Seconds |
| [`RegisterReactiveEffectBattleEffectResource`](../src/battle/resources/effects/RegisterReactiveEffectBattleEffectResource.cs) | Catalog reactive effect ID to register on the acting battler |

`ScaleBySourceStatusId` multiplies the authored amount or power by that
status's stack count. A status-owned reactive effect retains the triggering stack
count during removal.

### Create an Action

1. Add a `BattleActionResource` to the root `Actions`.
2. Give it a unique stable ID.
3. Choose targeting, range, cost, priority, and retargeting.
4. Add effect resources in execution order.
5. Add the action ID to enemies or party members that know it.

## Enemies and Encounters

Enemy species data and encounter placement are separate.

[`BattleEnemyResource`](../src/battle/resources/enemies/BattleEnemyResource.cs) owns:

| Field | Meaning |
| --- | --- |
| `Id` | Reusable `EnemyId`; not a runtime battler ID |
| `DisplayName` | Name shared by each placed instance |
| `Stats` | Base battle stats |
| `Hp`, `Tp` | Starting resources |
| `Actions` | External `BattleActionResource` references |
| `ReactiveEffectIds` | Innate catalog reactive effects |
| `StatusResistances`, `StatusWeaknesses` | Status affinity values by status ID |
| `DamageTypeResistances`, `DamageTypeWeaknesses` | Damage affinity values by type |

It intentionally contains no row or slot.

[`BattleEnemyPlacementResource`](../src/battle/resources/encounters/BattleEnemyPlacementResource.cs)
owns encounter-specific data:

| Field | Meaning |
| --- | --- |
| `Enemy` | External reusable enemy resource |
| `BattlerId` | Unique runtime identity for this placement |
| `Row` | Front or back |
| `Slot` | `0-2` |

[`BattleEncounterResource`](../src/battle/resources/encounters/BattleEncounterResource.cs)
contains up to six placements plus experience, currency, and item rewards.
Compilation rejects missing enemy references, duplicate battler IDs, duplicate
positions, invalid slots, empty encounters, and more than six placements.

The same enemy resource may be placed repeatedly with different battler IDs
and positions. See
[`Squirrel.tres`](../src/battle/resources/enemies/Squirrel.tres) and
[`Floor1Squirrel.tres`](../src/battle/resources/floor_1_encounters/Floor1Squirrel.tres).

### Create an Enemy Encounter

1. Create one reusable `BattleEnemyResource`.
2. Reference valid action resources and reactive effect IDs.
3. Add affinity entries as needed.
4. Create an encounter.
5. Add one placement per enemy instance.
6. Assign a unique battler ID and position to every placement.

## Statuses

[`BattleStatusResource`](../src/battle/resources/statuses/BattleStatusResource.cs):

| Field | Meaning |
| --- | --- |
| `Id` | Stable catalog status ID |
| `DisplayName` | UI name |
| `PreventsAction` | Actor skips command execution while status exists |
| `DefaultDuration` | Turns used when an effect does not override duration |
| `MaxStacks` | Stack cap |
| `ReactiveEffectIds` | Reactive effects registered while the status exists |

Status behavior is entirely composed from these fields and reactive effects.
Expiration removes all reactive effects registered by that status.

Authored examples:

- [`Poison.tres`](../src/battle/resources/statuses/Poison.tres) registers
  `poison_tick`.
- [`Regen.tres`](../src/battle/resources/statuses/Regen.tres) registers
  `regen_tick`.
- [`Stun.tres`](../src/battle/resources/statuses/Stun.tres) sets
  `PreventsAction`.

## ReactiveEffects

[`BattleReactiveEffectResource`](../src/battle/resources/reactive_effects/BattleReactiveEffectResource.cs):

| Field | Meaning |
| --- | --- |
| `Id` | Stable catalog reactive effect ID |
| `Trigger` | Event type to match |
| `Schedule` | Queue insertion timing |
| `TargetPolicy` | Owner, event source, or event target |
| `Priority` | Higher matched reactive effects schedule first |
| `Uses` | `-1` for unlimited; positive values are consumed on match |
| `Conditions` | Typed predicates; all must pass |
| `Effects` | Effects scheduled when matched |

Triggers:

- `TurnStarted`, `TurnEnded`
- `ActionStarted`, `ActionFinished`
- `BeforeEffect`, `AfterEffect`
- `Damage`, `Healing`, `Defeat`
- `StatusApplied`, `StatusTriggered`, `StatusRemoved`

Conditions:

| Resource | Match |
| --- | --- |
| [`OwnerHasStatusReactiveEffectConditionResource`](../src/battle/resources/reactive_effects/conditions/OwnerHasStatusReactiveEffectConditionResource.cs) | Owner has a status with at least the requested stacks |
| [`TriggerActionReactiveEffectConditionResource`](../src/battle/resources/reactive_effects/conditions/TriggerActionReactiveEffectConditionResource.cs) | Event action ID matches |
| [`TriggerStatusReactiveEffectConditionResource`](../src/battle/resources/reactive_effects/conditions/TriggerStatusReactiveEffectConditionResource.cs) | Event status ID matches |
| [`OwnerRelationReactiveEffectConditionResource`](../src/battle/resources/reactive_effects/conditions/OwnerRelationReactiveEffectConditionResource.cs) | Owner is the event source or target |

Conditions are AND-combined. Event metadata carries action ID, status ID,
status stacks, source, target, cause, and reactive effect depth when applicable.

Schedules:

- `Immediate`: insert effects at the operation queue front.
- `AfterCurrentAction`: defer until `ActionFinished`; execute immediately when
  the event has no active action.
- `EndOfTurn`: flush after `TurnEnded` and before status expiration. Reactive effects
  created during this flush are drained before expiration.

Matching preserves priority, then registration order. Cause guards prevent one
registration from handling the same cause twice. Reactive effect depth is capped by
`BattleRepo.MaxReactiveEffectDepth`.

### ReactiveEffect Ownership

ReactiveEffects enter battle through four paths:

- Party `PassiveReactiveEffectIds`: registered at battle start.
- Enemy `ReactiveEffectIds`: registered at battle start.
- Status `ReactiveEffectIds`: registered on first application and removed with the
  status.
- `RegisterReactiveEffectBattleEffectResource`: dynamically registered on the
  effect source; catalog `Uses` still applies.

### Poison and Toxic Recovery

[`PoisonTick.tres`](../src/battle/resources/reactive_effects/PoisonTick.tres):

1. `Poison` registers `poison_tick`.
2. `TurnEnded` matches it.
3. `EndOfTurn` schedules true damage on the owner.
4. Damage power is multiplied by Poison stacks.
5. The status then loses duration and may expire.

[`ToxicRecovery.tres`](../src/battle/resources/reactive_effects/ToxicRecovery.tres)
is a party passive example:

1. Add `toxic_recovery` to a party member's passive reactive effect IDs.
2. `TurnEnded` checks `OwnerHasStatus(poison, 1)`.
3. It targets the owner.
4. Healing scales with Poison stacks.

Poison has higher priority, so its damage is queued before Toxic Recovery.

## Affinities

Status application chance:

```text
clamp(baseChance * (1 - resistance) * (1 + weakness), 0, 1)
```

Final typed damage:

```text
computedDamage * (1 - resistance) * (1 + weakness)
```

`True` damage bypasses damage affinities. Party members persist status
resistances, status weaknesses, damage resistances, damage weaknesses, and
passive reactive effect IDs through
[`PartyData`](../src/party/domain/PartyData.cs).

## Stats and Equipment

[`BattleStatsResource`](../src/battle/resources/stats/BattleStatsResource.cs)
authors HP, TP, primary stats, attack, and defense.

[`BattleEquipmentResource`](../src/battle/resources/equipment/BattleEquipmentResource.cs)
contains an ID, display name, and ordered
[`BattleStatModifierResource`](../src/battle/resources/equipment/BattleStatModifierResource.cs)
entries. Equipment modifiers contribute to party effective stats before the
battle seed is created.

## Verification

Resolver and authoring tests are in
[`BattleRepoTest`](../test/src/battle/domain/BattleRepoTest.cs). Party persistence tests are
in [`PartyRepoTest`](../test/src/party/domain/PartyRepoTest.cs).

The reactive effect operation flow is shown in
[`battle-reactive-effect-flow.puml`](diagrams/battle-reactive-effect-flow.puml).
