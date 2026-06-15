namespace Labyrinth;

using System;
using System.Collections.Generic;
using System.Linq;

internal sealed class BattleOperationExecutor(
    BattleRuntime runtime,
    BattleTargetResolver targeting,
    BattleEffectOperationBuilder effects,
    BattleReactionResolver reactions,
    BattleOutcomeResolver outcome,
    BattleDamageSystem damage,
    BattleHealingSystem healing,
    BattleResourceSystem resources,
    BattleStatusSystem statuses,
    BattleDeathSystem deaths
)
{
    public void Execute(BattleOperation operation)
    {
        switch (operation)
        {
            case ExecuteActionOperation execute:
                ExecuteAction(execute.Command);
                break;
            case WindowOperation window:
                reactions.Trigger(window.Event);
                break;
            case DamageOperation damageOperation:
                damage.Apply(damageOperation);
                break;
            case HealOperation heal:
                healing.Apply(heal);
                break;
            case ModifyResourceOperation modify:
                resources.Modify(modify);
                break;
            case ApplyStatusOperation apply:
                statuses.Apply(apply);
                break;
            case RemoveStatusOperation remove:
                statuses.Remove(remove);
                break;
            case RegisterReactionOperation register:
                reactions.Register(register);
                break;
            case UnregisterStatusReactionsOperation unregister:
                reactions.UnregisterStatusReactions(
                    unregister.OwnerId,
                    unregister.StatusId
                );
                break;
            case ActionEndOperation actionEnd:
                EndAction(actionEnd);
                break;
            case FlushAfterActionReactionsOperation:
                runtime.AfterActionFlushStarted = true;
                reactions.Flush(
                    runtime.AfterActionReactions,
                    repeat: false
                );
                break;
            case FlushEndTurnReactionsOperation:
                runtime.EndTurnFlushStarted = true;
                reactions.Flush(
                    runtime.EndTurnReactions,
                    repeat: true
                );
                break;
            case ExpireStatusesOperation:
                statuses.Expire();
                break;
            case DeathCheckOperation deathCheck:
                deaths.Check(deathCheck);
                break;
            case FinishTurnOperation:
                outcome.FinishTurn();
                break;
            case CompleteOperation complete:
                outcome.Complete(complete.Outcome);
                break;
            default:
                throw new InvalidOperationException(
                    $"Unknown battle operation "
                    + $"'{operation.GetType().Name}'."
                );
        }
    }

    private void ExecuteAction(BattleCommand command)
    {
        runtime.AfterActionFlushStarted = false;
        if (
            !runtime.Units.TryGetValue(command.ActorId, out var actor)
            || !actor.IsAlive
            || !runtime.Catalog.TryGetAction(
                command.ActionId,
                out var action
            )
            || actor.Tp < action.TpCost
        )
        {
            return;
        }

        if (actor.PreventsAction(runtime.Catalog))
        {
            return;
        }

        var targets = targeting.ResolveTargets(
            actor,
            action,
            command.TargetId
        );
        if (targets.Count == 0)
        {
            return;
        }

        actor.Tp -= action.TpCost;
        var context = new EffectContext(
            actor.Id,
            targets.Select(target => target.Id).ToArray(),
            action.Range,
            action.Id,
            0,
            null,
            0
        );
        var operations = new List<BattleOperation>
        {
            new WindowOperation(new ReactionEvent(
                runtime.NextCauseId(),
                ReactionTrigger.ActionStarted,
                actor.Id,
                targets[0].Id,
                action.Id,
                Depth: 0
            )),
        };
        foreach (var effect in action.Effects)
        {
            operations.AddRange(effects.Build(effect, context));
        }
        operations.Add(new ActionEndOperation(context));
        runtime.InsertFront(operations);
    }

    private void EndAction(ActionEndOperation operation)
    {
        runtime.InsertFront([
            new WindowOperation(new ReactionEvent(
                runtime.NextCauseId(),
                ReactionTrigger.ActionFinished,
                operation.Context.SourceId,
                operation.Context.TargetIds.Count > 0
                    ? operation.Context.TargetIds[0]
                    : default,
                operation.Context.ActionId,
                Depth: operation.Context.ReactionDepth
            )),
            new FlushAfterActionReactionsOperation(),
        ]);
    }
}
