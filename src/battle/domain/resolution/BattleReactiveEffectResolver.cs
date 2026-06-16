namespace Labyrinth;

using System.Collections.Generic;
using System.Linq;

internal sealed class BattleReactiveEffectResolver(
    BattleRuntime runtime,
    BattleEffectOperationBuilder effects,
    BattleTargetResolver targeting
)
{
    public void RegisterStatusReactiveEffects(
        BattlerId ownerId,
        StatusDefinition status
    )
    {
        foreach (var reactiveEffectId in status.ReactiveEffectIds)
        {
            Register(ownerId, reactiveEffectId, status.Id);
        }
    }

    public void Register(
        BattlerId ownerId,
        ReactiveEffectId reactiveEffectId,
        StatusId? sourceStatusId
    )
    {
        runtime.ReactiveEffects.Add(new RuntimeReactiveEffect(
            runtime.NextReactiveEffectRegistrationId(),
            ownerId,
            runtime.Catalog.GetReactiveEffect(reactiveEffectId),
            sourceStatusId
        ));
    }

    public void Register(RegisterReactiveEffectOperation operation)
    {
        if (
            operation.Context.ReactiveEffectDepth
            >= BattleRules.MaxReactiveEffectDepth
        )
        {
            return;
        }
        Register(
            operation.Context.SourceId,
            operation.ReactiveEffectId,
            null
        );
    }

    public void UnregisterStatusReactiveEffects(
        BattlerId ownerId,
        StatusId statusId
    ) => runtime.ReactiveEffects.RemoveAll(reactiveEffect =>
        reactiveEffect.OwnerId == ownerId
        && reactiveEffect.SourceStatusId == statusId);

    public void Trigger(ReactiveEffectEvent reactiveEffectEvent)
    {
        var matching = runtime.ReactiveEffects
            .Where(reactiveEffect =>
                reactiveEffect.Definition.Trigger == reactiveEffectEvent.Trigger
                && reactiveEffect.HasUses
                && runtime.Units.TryGetValue(
                    reactiveEffect.OwnerId,
                    out var owner
                )
                && (
                    owner.IsAlive
                    || reactiveEffectEvent.Trigger == ReactiveEffectTrigger.Defeat
                )
                && ConditionsMatch(reactiveEffect, reactiveEffectEvent))
            .OrderByDescending(reactiveEffect =>
                reactiveEffect.Definition.Priority)
            .ThenBy(reactiveEffect => reactiveEffect.RegistrationId)
            .ToArray();

        var immediate = new List<ReactiveEffectInvocation>();
        foreach (var reactiveEffect in matching)
        {
            var guard = (
                reactiveEffect.RegistrationId,
                reactiveEffectEvent.CauseId
            );
            if (
                reactiveEffectEvent.Depth >= BattleRules.MaxReactiveEffectDepth
                || !runtime.ReactiveEffectGuards.Add(guard)
            )
            {
                continue;
            }

            reactiveEffect.Consume();
            var invocation = new ReactiveEffectInvocation(
                reactiveEffect,
                reactiveEffectEvent
            );
            switch (reactiveEffect.Definition.Schedule)
            {
                case ReactiveEffectSchedule.Immediate:
                    immediate.Add(invocation);
                    break;
                case ReactiveEffectSchedule.AfterCurrentAction:
                    if (
                        reactiveEffectEvent.ActionId is null
                        || runtime.AfterActionFlushStarted
                    )
                    {
                        immediate.Add(invocation);
                    }
                    else
                    {
                        runtime.AfterActionReactiveEffects.Add(invocation);
                    }
                    break;
                case ReactiveEffectSchedule.EndOfTurn:
                    if (runtime.EndTurnFlushStarted)
                    {
                        immediate.Add(invocation);
                    }
                    else
                    {
                        runtime.EndTurnReactiveEffects.Add(invocation);
                    }
                    break;
            }
        }

        Flush(immediate, repeat: false);
        runtime.ReactiveEffects.RemoveAll(reactiveEffect => !reactiveEffect.HasUses);
    }

    public void Flush(
        List<ReactiveEffectInvocation> reactiveEffects,
        bool repeat
    )
    {
        if (reactiveEffects.Count == 0)
        {
            return;
        }

        var operations = new List<BattleOperation>();
        foreach (var invocation in reactiveEffects)
        {
            var targets = targeting.ResolveReactiveEffectTargets(invocation);
            if (targets.Count == 0)
            {
                continue;
            }
            var context = new EffectContext(
                invocation.ReactiveEffect.OwnerId,
                targets,
                BattleRange.None,
                invocation.Event.ActionId,
                invocation.Event.Depth + 1,
                invocation.ReactiveEffect.SourceStatusId
                    ?? invocation.Event.StatusId,
                ResolveStatusStackContext(invocation),
                ResolveStatusPowerContext(invocation)
            );
            if (
                invocation.ReactiveEffect.SourceStatusId
                    is { } sourceStatusId
                && runtime.Units.TryGetValue(
                    invocation.ReactiveEffect.OwnerId,
                    out var owner
                )
                && owner.Statuses.TryGetValue(
                    sourceStatusId,
                    out var sourceStatus
                )
            )
            {
                operations.Add(new WindowOperation(new ReactiveEffectEvent(
                    runtime.NextCauseId(),
                    ReactiveEffectTrigger.StatusTriggered,
                    invocation.ReactiveEffect.OwnerId,
                    invocation.ReactiveEffect.OwnerId,
                    invocation.Event.ActionId,
                    sourceStatusId,
                    sourceStatus.Stacks,
                    sourceStatus.Power,
                    invocation.Event.Depth + 1
                )));
            }
            foreach (
                var effect in invocation.ReactiveEffect.Definition.Effects
            )
            {
                operations.AddRange(effects.Build(effect, context));
            }
        }
        reactiveEffects.Clear();
        if (repeat && operations.Count > 0)
        {
            operations.Add(new FlushEndTurnReactiveEffectsOperation());
        }
        runtime.InsertFront(operations);
    }

    private bool ConditionsMatch(
        RuntimeReactiveEffect reactiveEffect,
        ReactiveEffectEvent reactiveEffectEvent
    )
    {
        if (
            !runtime.Units.TryGetValue(
                reactiveEffect.OwnerId,
                out var owner
            )
        )
        {
            return false;
        }
        foreach (var condition in reactiveEffect.Definition.Conditions)
        {
            var matches = condition switch
            {
                OwnerHasStatusConditionDefinition status =>
                    owner.Statuses.TryGetValue(
                        status.StatusId,
                        out var runtimeStatus
                    )
                    && runtimeStatus.Stacks >= status.MinimumStacks,
                TriggerActionConditionDefinition action =>
                    reactiveEffectEvent.ActionId == action.ActionId,
                TriggerStatusConditionDefinition status =>
                    reactiveEffectEvent.StatusId == status.StatusId,
                OwnerRelationConditionDefinition relation =>
                    relation.Relation switch
                    {
                        ReactiveEffectOwnerRelation.EventSource =>
                            reactiveEffect.OwnerId
                            == reactiveEffectEvent.SourceId,
                        ReactiveEffectOwnerRelation.EventTarget =>
                            reactiveEffect.OwnerId
                            == reactiveEffectEvent.TargetId,
                        _ => false,
                    },
                _ => false,
            };
            if (!matches)
            {
                return false;
            }
        }
        return true;
    }

    private int ResolveStatusStackContext(
        ReactiveEffectInvocation invocation
    )
    {
        var statusId = invocation.ReactiveEffect.SourceStatusId
            ?? invocation.Event.StatusId;
        if (
            statusId is { } id
            && runtime.Units.TryGetValue(
                invocation.ReactiveEffect.OwnerId,
                out var owner
            )
            && owner.Statuses.TryGetValue(id, out var status)
        )
        {
            return status.Stacks;
        }
        return invocation.Event.StatusId == statusId
            ? invocation.Event.StatusStacks
            : 0;
    }

    private double ResolveStatusPowerContext(
        ReactiveEffectInvocation invocation
    )
    {
        var statusId = invocation.ReactiveEffect.SourceStatusId
            ?? invocation.Event.StatusId;
        if (
            statusId is { } id
            && runtime.Units.TryGetValue(
                invocation.ReactiveEffect.OwnerId,
                out var owner
            )
            && owner.Statuses.TryGetValue(id, out var status)
        )
        {
            return status.Power;
        }
        return invocation.Event.StatusId == statusId
            ? invocation.Event.StatusPower
            : 0;
    }
}
