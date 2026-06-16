namespace Labyrinth;

using System;
using System.Collections.Generic;
using System.Linq;

internal sealed class BattleStatusSystem(
    BattleRuntime runtime,
    BattleReactiveEffectResolver reactiveEffects
)
{
    public void Apply(ApplyStatusOperation operation)
    {
        var definition = runtime.Catalog.GetStatus(
            operation.Effect.StatusId
        );
        var followUps = new List<BattleOperation>();
        foreach (var targetId in operation.Context.TargetIds)
        {
            if (
                !runtime.Units.TryGetValue(targetId, out var target)
                || !target.IsAlive
            )
            {
                continue;
            }
            target.StatusResistances.TryGetValue(
                definition.Id,
                out var resistance
            );
            target.StatusWeaknesses.TryGetValue(
                definition.Id,
                out var weakness
            );
            var chance = Math.Clamp(
                operation.Effect.BaseChance
                    * (1.0 - resistance)
                    * (1.0 + weakness),
                0.0,
                1.0
            );
            if (runtime.Random.NextDouble() >= chance)
            {
                continue;
            }

            var duration = operation.Effect.Duration > 0
                ? operation.Effect.Duration
                : definition.DefaultDuration;
            var isNew = !target.Statuses.TryGetValue(
                definition.Id,
                out var status
            );
            if (!isNew)
            {
                var existingStatus = status!;
                existingStatus.Stacks = Math.Min(
                    definition.MaxStacks,
                    existingStatus.Stacks
                        + Math.Max(1, operation.Effect.Stacks)
                );
                existingStatus.RemainingTurns = Math.Max(
                    existingStatus.RemainingTurns,
                    duration
                );
                existingStatus.Power = Math.Max(
                    existingStatus.Power,
                    operation.Effect.Power
                );
                status = existingStatus;
            }
            else
            {
                status = new RuntimeStatus(
                    definition.Id,
                    Math.Min(
                        definition.MaxStacks,
                        Math.Max(1, operation.Effect.Stacks)
                    ),
                    duration,
                    operation.Effect.Power
                );
                target.Statuses.Add(definition.Id, status);
                reactiveEffects.RegisterStatusReactiveEffects(
                    target.Id,
                    definition
                );
            }

            followUps.Add(new VisualCueOperation([
                new StatusCue(
                    target.Id,
                    definition.Id,
                    Applied: true,
                    status.Stacks
                ),
            ]));
            followUps.Add(new TriggerReactiveEffectsOperation(new ReactiveEffectEvent(
                runtime.NextCauseId(),
                ReactiveEffectTrigger.StatusApplied,
                operation.Context.SourceId,
                target.Id,
                operation.Context.ActionId,
                definition.Id,
                status.Stacks,
                status.Power,
                operation.Context.ReactiveEffectDepth
            )));
        }
        runtime.InsertFront(followUps);
    }

    public void Remove(RemoveStatusOperation operation)
    {
        var followUps = new List<BattleOperation>();
        foreach (var targetId in operation.Context.TargetIds)
        {
            if (
                runtime.Units.TryGetValue(targetId, out var target)
                && target.Statuses.Remove(
                    operation.StatusId,
                    out var removed
                )
            )
            {
                followUps.Add(new VisualCueOperation([
                    new StatusCue(
                        target.Id,
                        operation.StatusId,
                        Applied: false,
                        0
                    ),
                ]));
                followUps.Add(new TriggerReactiveEffectsOperation(new ReactiveEffectEvent(
                    runtime.NextCauseId(),
                    ReactiveEffectTrigger.StatusRemoved,
                    operation.Context.SourceId,
                    target.Id,
                    operation.Context.ActionId,
                    operation.StatusId,
                    removed.Stacks,
                    removed.Power,
                    operation.Context.ReactiveEffectDepth
                )));
                followUps.Add(
                    new UnregisterStatusReactiveEffectsOperation(
                        target.Id,
                        operation.StatusId
                    )
                );
            }
        }
        runtime.InsertFront(followUps);
    }

    public void Expire()
    {
        var operations = new List<BattleOperation>();
        foreach (var unit in runtime.Units.Values)
        {
            foreach (var status in unit.Statuses.Values.ToArray())
            {
                status.RemainingTurns--;
                if (status.RemainingTurns > 0)
                {
                    continue;
                }
                unit.Statuses.Remove(status.Id);
                operations.Add(new VisualCueOperation([
                    new StatusCue(
                        unit.Id,
                        status.Id,
                        Applied: false,
                        0
                    ),
                ]));
                operations.Add(new TriggerReactiveEffectsOperation(new ReactiveEffectEvent(
                    runtime.NextCauseId(),
                    ReactiveEffectTrigger.StatusRemoved,
                    unit.Id,
                    unit.Id,
                    StatusId: status.Id,
                    StatusStacks: status.Stacks,
                    StatusPower: status.Power
                )));
                operations.Add(
                    new UnregisterStatusReactiveEffectsOperation(
                        unit.Id,
                        status.Id
                    )
                );
            }
        }
        runtime.InsertFront(operations);
    }
}
