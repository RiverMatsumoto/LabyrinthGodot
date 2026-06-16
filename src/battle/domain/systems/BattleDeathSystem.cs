namespace Labyrinth;

using System;
using System.Collections.Generic;
using System.Linq;

internal sealed class BattleDeathSystem(
    BattleRuntime runtime,
    BattleOutcomeResolver outcome
)
{
    public void Check(DeathCheckOperation operation)
    {
        var newlyDead = runtime.Units.Values
            .Where(unit =>
                !unit.IsAlive
                && runtime.HandledDeaths.Add(unit.Id))
            .OrderBy(unit => unit.Team)
            .ThenBy(unit => unit.Id.Value, StringComparer.Ordinal)
            .ToArray();
        if (newlyDead.Length == 0)
        {
            return;
        }

        var operations = new List<BattleOperation>
        {
            new CueOperation(
                newlyDead
                    .Select(unit =>
                        (BattleCue)new DeathCue(unit.Id))
                    .ToArray()
            ),
        };
        operations.AddRange(newlyDead.Select(unit =>
            (BattleOperation)new WindowOperation(new ReactiveEffectEvent(
                runtime.NextCauseId(),
                ReactiveEffectTrigger.Defeat,
                operation.SourceId,
                unit.Id,
                operation.ActionId,
                Depth: operation.ReactiveEffectDepth
            ))
        ));

        var result = outcome.DetermineOutcome();
        if (result is not null)
        {
            runtime.Operations.Clear();
            operations.Add(new CompleteOperation(result.Value));
        }
        runtime.InsertFront(operations);
    }
}
