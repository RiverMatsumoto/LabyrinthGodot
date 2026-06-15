namespace Labyrinth;

using System;
using System.Collections.Generic;
using System.Linq;

internal sealed class BattleCommandService(
    BattleRuntime runtime,
    BattleTargetResolver targeting
)
{
    public BattlerId? RequestedPlayerId
    {
        get
        {
            foreach (var unit in OrderedUnits(BattleTeam.Player))
            {
                if (
                    unit.IsAlive
                    && !runtime.PlayerCommands.ContainsKey(unit.Id)
                )
                {
                    return unit.Id;
                }
            }
            return null;
        }
    }

    public CommandValidationResult Validate(BattleCommand command)
    {
        if (runtime.Phase != BattleDomainPhase.SelectingCommands)
        {
            return CommandValidationResult.Invalid(
                "Battle is not selecting commands."
            );
        }
        if (
            !runtime.Units.TryGetValue(command.ActorId, out var actor)
            || actor.Team != BattleTeam.Player
            || !actor.IsAlive
        )
        {
            return CommandValidationResult.Invalid(
                "Command actor is not a living player battler."
            );
        }
        if (
            !runtime.Catalog.TryGetAction(command.ActionId, out var action)
            || !actor.ActionIds.Contains(command.ActionId)
        )
        {
            return CommandValidationResult.Invalid(
                "Battler does not know the requested action."
            );
        }
        if (actor.Tp < action.TpCost)
        {
            return CommandValidationResult.Invalid("Not enough TP.");
        }

        return targeting.ResolveTargets(
            actor,
            action,
            command.TargetId
        ).Count > 0
            ? CommandValidationResult.Valid
            : CommandValidationResult.Invalid(
                "The action has no valid targets."
            );
    }

    public bool Submit(BattleCommand command)
    {
        if (!Validate(command).IsValid)
        {
            return false;
        }

        if (!runtime.PlayerCommands.ContainsKey(command.ActorId))
        {
            runtime.CommandOrder.Add(command.ActorId);
        }
        runtime.PlayerCommands[command.ActorId] = command;
        return true;
    }

    public bool UndoLast()
    {
        if (
            runtime.Phase != BattleDomainPhase.SelectingCommands
            || runtime.CommandOrder.Count == 0
        )
        {
            return false;
        }

        var id = runtime.CommandOrder[^1];
        runtime.CommandOrder.RemoveAt(runtime.CommandOrder.Count - 1);
        return runtime.PlayerCommands.Remove(id);
    }

    public IReadOnlyList<BattleCommand> BuildOrderedCommands(
        IEnemyCommandPlanner enemyCommandPlanner
    )
    {
        var commands = new List<BattleCommand>(
            runtime.PlayerCommands.Values
        );
        var snapshot = runtime.Snapshot();
        foreach (var enemy in OrderedUnits(BattleTeam.Enemy))
        {
            if (!enemy.IsAlive)
            {
                continue;
            }
            var command = enemyCommandPlanner.Plan(
                snapshot,
                enemy.Id,
                runtime.Catalog,
                runtime.Random
            );
            if (
                command is not null
                && IsValidEnemyCommand(command, enemy)
            )
            {
                commands.Add(command);
            }
        }

        return commands
            .Where(command => runtime.Units.ContainsKey(command.ActorId))
            .OrderByDescending(command =>
                runtime.Catalog.GetAction(command.ActionId).Priority)
            .ThenByDescending(command =>
                runtime.Units[command.ActorId].Stats.Agility)
            .ThenBy(
                command => command.ActorId.Value,
                StringComparer.Ordinal
            )
            .ToArray();
    }

    private bool IsValidEnemyCommand(
        BattleCommand command,
        BattleUnit enemy
    )
    {
        if (
            command.ActorId != enemy.Id
            || !runtime.Catalog.TryGetAction(
                command.ActionId,
                out var action
            )
            || !enemy.ActionIds.Contains(command.ActionId)
            || enemy.Tp < action.TpCost
        )
        {
            return false;
        }

        return targeting.ResolveTargets(
            enemy,
            action,
            command.TargetId
        ).Count > 0;
    }

    private IEnumerable<BattleUnit> OrderedUnits(BattleTeam team) =>
        runtime.Units.Values
            .Where(unit => unit.Team == team)
            .OrderBy(unit => unit.Position.Row)
            .ThenBy(unit => unit.Position.Index)
            .ThenBy(unit => unit.Id.Value, StringComparer.Ordinal);
}
