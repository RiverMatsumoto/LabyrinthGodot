namespace Labyrinth;

using System;
using System.Linq;

internal sealed class BattleOutcomeResolver(BattleRuntime runtime)
{
    public BattleOutcome? DetermineOutcome()
    {
        var playersAlive = runtime.Units.Values.Any(unit =>
            unit.Team == BattleTeam.Player && unit.IsAlive);
        var enemiesAlive = runtime.Units.Values.Any(unit =>
            unit.Team == BattleTeam.Enemy && unit.IsAlive);
        if (!enemiesAlive)
        {
            return BattleOutcome.Victory;
        }
        if (!playersAlive)
        {
            return BattleOutcome.Defeat;
        }
        return null;
    }

    public void FinishTurn()
    {
        var outcome = DetermineOutcome();
        if (outcome is not null)
        {
            Complete(outcome.Value);
            return;
        }

        runtime.PlayerCommands.Clear();
        runtime.CommandOrder.Clear();
        runtime.ReactiveEffectGuards.Clear();
        runtime.AfterActionFlushStarted = false;
        runtime.EndTurnFlushStarted = false;
        runtime.Turn++;
        runtime.Phase = BattleDomainPhase.SelectingCommands;
    }

    public BattleAdvance Flee()
    {
        if (
            runtime.Phase is BattleDomainPhase.Disabled
                or BattleDomainPhase.Completed
        )
        {
            throw new InvalidOperationException(
                "Cannot flee from the current battle phase."
            );
        }

        Complete(BattleOutcome.Fled);
        return BattleAdvance.Complete(runtime.Result!);
    }

    public void Complete(BattleOutcome outcome)
    {
        var setup = runtime.Setup ?? throw new InvalidOperationException(
            "Battle has not been started."
        );
        runtime.Operations.Clear();
        runtime.Result = new BattleResult(
            outcome,
            setup.EncounterId,
            setup.Reward,
            runtime.Units.Values
                .Where(unit => unit.Team == BattleTeam.Player)
                .ToDictionary(unit => unit.Id, unit => (unit.Hp, unit.Tp))
        );
        runtime.Phase = BattleDomainPhase.Completed;
    }
}
