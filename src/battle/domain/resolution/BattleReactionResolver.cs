namespace Labyrinth;

using System.Collections.Generic;
using System.Linq;

internal sealed class BattleReactionResolver(
    BattleRuntime runtime,
    BattleEffectOperationBuilder effects,
    BattleTargetResolver targeting
)
{
    public void RegisterStatusReactions(
        BattlerId ownerId,
        StatusDefinition status
    )
    {
        foreach (var reactionId in status.ReactionIds)
        {
            Register(ownerId, reactionId, status.Id);
        }
    }

    public void Register(
        BattlerId ownerId,
        ReactionId reactionId,
        StatusId? sourceStatusId
    )
    {
        runtime.Reactions.Add(new RuntimeReaction(
            runtime.NextReactionRegistrationId(),
            ownerId,
            runtime.Catalog.GetReaction(reactionId),
            sourceStatusId
        ));
    }

    public void Register(RegisterReactionOperation operation)
    {
        if (
            operation.Context.ReactionDepth
            >= BattleRules.MaxReactionDepth
        )
        {
            return;
        }
        Register(
            operation.Context.SourceId,
            operation.ReactionId,
            null
        );
    }

    public void UnregisterStatusReactions(
        BattlerId ownerId,
        StatusId statusId
    ) => runtime.Reactions.RemoveAll(reaction =>
        reaction.OwnerId == ownerId
        && reaction.SourceStatusId == statusId);

    public void Trigger(ReactionEvent reactionEvent)
    {
        var matching = runtime.Reactions
            .Where(reaction =>
                reaction.Definition.Trigger == reactionEvent.Trigger
                && reaction.HasUses
                && runtime.Units.TryGetValue(
                    reaction.OwnerId,
                    out var owner
                )
                && (
                    owner.IsAlive
                    || reactionEvent.Trigger == ReactionTrigger.Defeat
                )
                && ConditionsMatch(reaction, reactionEvent))
            .OrderByDescending(reaction =>
                reaction.Definition.Priority)
            .ThenBy(reaction => reaction.RegistrationId)
            .ToArray();

        var immediate = new List<ReactionInvocation>();
        foreach (var reaction in matching)
        {
            var guard = (
                reaction.RegistrationId,
                reactionEvent.CauseId
            );
            if (
                reactionEvent.Depth >= BattleRules.MaxReactionDepth
                || !runtime.ReactionGuards.Add(guard)
            )
            {
                continue;
            }

            reaction.Consume();
            var invocation = new ReactionInvocation(
                reaction,
                reactionEvent
            );
            switch (reaction.Definition.Schedule)
            {
                case ReactionSchedule.Immediate:
                    immediate.Add(invocation);
                    break;
                case ReactionSchedule.AfterCurrentAction:
                    if (
                        reactionEvent.ActionId is null
                        || runtime.AfterActionFlushStarted
                    )
                    {
                        immediate.Add(invocation);
                    }
                    else
                    {
                        runtime.AfterActionReactions.Add(invocation);
                    }
                    break;
                case ReactionSchedule.EndOfTurn:
                    if (runtime.EndTurnFlushStarted)
                    {
                        immediate.Add(invocation);
                    }
                    else
                    {
                        runtime.EndTurnReactions.Add(invocation);
                    }
                    break;
            }
        }

        Flush(immediate, repeat: false);
        runtime.Reactions.RemoveAll(reaction => !reaction.HasUses);
    }

    public void Flush(
        List<ReactionInvocation> reactions,
        bool repeat
    )
    {
        if (reactions.Count == 0)
        {
            return;
        }

        var operations = new List<BattleOperation>();
        foreach (var invocation in reactions)
        {
            var targets = targeting.ResolveReactionTargets(invocation);
            if (targets.Count == 0)
            {
                continue;
            }
            var context = new EffectContext(
                invocation.Reaction.OwnerId,
                targets,
                BattleRange.None,
                invocation.Event.ActionId,
                invocation.Event.Depth + 1,
                invocation.Reaction.SourceStatusId
                    ?? invocation.Event.StatusId,
                ResolveStatusStackContext(invocation)
            );
            if (
                invocation.Reaction.SourceStatusId
                    is { } sourceStatusId
                && runtime.Units.TryGetValue(
                    invocation.Reaction.OwnerId,
                    out var owner
                )
                && owner.Statuses.TryGetValue(
                    sourceStatusId,
                    out var sourceStatus
                )
            )
            {
                operations.Add(new WindowOperation(new ReactionEvent(
                    runtime.NextCauseId(),
                    ReactionTrigger.StatusTriggered,
                    invocation.Reaction.OwnerId,
                    invocation.Reaction.OwnerId,
                    invocation.Event.ActionId,
                    sourceStatusId,
                    sourceStatus.Stacks,
                    invocation.Event.Depth + 1
                )));
            }
            foreach (
                var effect in invocation.Reaction.Definition.Effects
            )
            {
                operations.AddRange(effects.Build(effect, context));
            }
        }
        reactions.Clear();
        if (repeat && operations.Count > 0)
        {
            operations.Add(new FlushEndTurnReactionsOperation());
        }
        runtime.InsertFront(operations);
    }

    private bool ConditionsMatch(
        RuntimeReaction reaction,
        ReactionEvent reactionEvent
    )
    {
        if (
            !runtime.Units.TryGetValue(
                reaction.OwnerId,
                out var owner
            )
        )
        {
            return false;
        }
        foreach (var condition in reaction.Definition.Conditions)
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
                    reactionEvent.ActionId == action.ActionId,
                TriggerStatusConditionDefinition status =>
                    reactionEvent.StatusId == status.StatusId,
                OwnerRelationConditionDefinition relation =>
                    relation.Relation switch
                    {
                        ReactionOwnerRelation.EventSource =>
                            reaction.OwnerId
                            == reactionEvent.SourceId,
                        ReactionOwnerRelation.EventTarget =>
                            reaction.OwnerId
                            == reactionEvent.TargetId,
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
        ReactionInvocation invocation
    )
    {
        var statusId = invocation.Reaction.SourceStatusId
            ?? invocation.Event.StatusId;
        if (
            statusId is { } id
            && runtime.Units.TryGetValue(
                invocation.Reaction.OwnerId,
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
}
