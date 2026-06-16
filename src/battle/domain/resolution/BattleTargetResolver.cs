namespace Labyrinth;

using System;
using System.Collections.Generic;
using System.Linq;

internal sealed class BattleTargetResolver(BattleRuntime runtime)
{
    public IReadOnlyList<BattlerId> GetValidTargets(
        BattlerId actorId,
        ActionId actionId
    )
    {
        if (
            !runtime.Units.TryGetValue(actorId, out var actor)
            || !runtime.Catalog.TryGetAction(actionId, out var action)
        )
        {
            return [];
        }

        return GetTargetCandidates(actor, action)
            .Select(unit => unit.Id)
            .ToArray();
    }

    public List<BattleUnit> ResolveTargets(
        BattleUnit source,
        BattleActionDefinition action,
        BattlerId? selectedId
    )
    {
        var candidates = GetTargetCandidates(source, action);
        if (candidates.Count == 0)
        {
            return [];
        }

        if (action.TargetRule == BattleTargetRule.Self)
        {
            return [source];
        }
        if (
            action.TargetRule is BattleTargetRule.AllAllies
                or BattleTargetRule.AllEnemies
        )
        {
            return candidates;
        }

        var selected = selectedId is { } id
            ? candidates.FirstOrDefault(unit => unit.Id == id)
            : null;
        if (selected is null)
        {
            if (action.RetargetPolicy == RetargetPolicy.Fail)
            {
                return [];
            }
            selected = candidates[0];
        }

        if (
            action.TargetRule is BattleTargetRule.RowAllies
                or BattleTargetRule.RowEnemies
        )
        {
            return candidates
                .Where(unit => unit.Position.Row == selected.Position.Row)
                .ToList();
        }
        return [selected];
    }

    public IReadOnlyList<BattlerId> ResolveReactiveEffectTargets(
        ReactiveEffectInvocation invocation
    )
    {
        var target = invocation.ReactiveEffect.Definition.TargetPolicy switch
        {
            ReactiveEffectTargetPolicy.Owner => invocation.ReactiveEffect.OwnerId,
            ReactiveEffectTargetPolicy.EventSource =>
                invocation.Event.SourceId,
            ReactiveEffectTargetPolicy.EventTarget =>
                invocation.Event.TargetId,
            _ => null,
        };
        return target is { } id && runtime.Units.ContainsKey(id)
            ? [id]
            : [];
    }

    private List<BattleUnit> GetTargetCandidates(
        BattleUnit source,
        BattleActionDefinition action
    )
    {
        var team = action.TargetRule switch
        {
            BattleTargetRule.Self => source.Team,
            BattleTargetRule.SingleAlly => source.Team,
            BattleTargetRule.RowAllies => source.Team,
            BattleTargetRule.AllAllies => source.Team,
            _ => source.Team == BattleTeam.Player
                ? BattleTeam.Enemy
                : BattleTeam.Player,
        };

        return runtime.Units.Values
            .Where(unit =>
                unit.IsAlive
                && unit.Team == team
                && (
                    action.TargetRule != BattleTargetRule.Self
                    || unit.Id == source.Id
                ))
            .OrderBy(unit => unit.Position.Row)
            .ThenBy(unit => unit.Position.Index)
            .ThenBy(unit => unit.Id.Value, StringComparer.Ordinal)
            .ToList();
    }
}
