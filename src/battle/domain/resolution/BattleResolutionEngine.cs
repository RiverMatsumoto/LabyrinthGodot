namespace Labyrinth;

using System;
using System.Linq;

internal sealed class BattleResolutionEngine(
    BattleRuntime runtime,
    BattleCommandService commands,
    BattleReactiveEffectResolver reactiveEffects,
    BattleOperationExecutor executor
)
{
    public void Start(BattleSetup setup)
    {
        runtime.Reset();

        if (
            setup.Players.Count is < 1
                or > BattleLimits.MaxPlayerBattlers
        )
        {
            throw new ArgumentException(
                "Battle requires one to five player battlers.",
                nameof(setup)
            );
        }
        if (setup.Enemies.Count == 0)
        {
            throw new ArgumentException(
                "Battle requires at least one enemy.",
                nameof(setup)
            );
        }
        if (!setup.Players.Any(player => player.Hp > 0))
        {
            throw new ArgumentException(
                "Battle requires at least one living player battler.",
                nameof(setup)
            );
        }
        if (!setup.Enemies.Any(enemy => enemy.Hp > 0))
        {
            throw new ArgumentException(
                "Battle requires at least one living enemy battler.",
                nameof(setup)
            );
        }

        runtime.Setup = setup;
        runtime.Random = new SeededRandomSource(setup.Seed);
        foreach (var seed in setup.Players.Concat(setup.Enemies))
        {
            if (!runtime.Units.TryAdd(seed.Id, new BattleUnit(seed)))
            {
                throw new ArgumentException(
                    $"Duplicate battler id '{seed.Id}'.",
                    nameof(setup)
                );
            }

            foreach (var actionId in seed.ActionIds)
            {
                _ = runtime.Catalog.GetAction(actionId);
            }
            foreach (var reactiveEffectId in seed.ReactiveEffectIds)
            {
                _ = runtime.Catalog.GetReactiveEffect(reactiveEffectId);
            }
        }
        foreach (var unit in runtime.Units.Values)
        {
            foreach (var reactiveEffectId in unit.ReactiveEffectIds)
            {
                reactiveEffects.Register(unit.Id, reactiveEffectId, null);
            }
        }

        runtime.Turn = 1;
        runtime.Phase = BattleDomainPhase.SelectingCommands;
    }

    public void BeginResolution(
        IEnemyCommandPlanner enemyCommandPlanner
    )
    {
        if (runtime.Phase != BattleDomainPhase.SelectingCommands)
        {
            throw new InvalidOperationException(
                "Battle is not selecting commands."
            );
        }
        if (commands.RequestedPlayerId is not null)
        {
            throw new InvalidOperationException(
                "Every living player requires a command."
            );
        }

        var ordered = commands.BuildOrderedCommands(
            enemyCommandPlanner
        );
        runtime.Operations.Clear();
        runtime.AfterActionFlushStarted = false;
        runtime.EndTurnFlushStarted = false;
        runtime.Operations.AddLast(new TriggerReactiveEffectsOperation(
            new ReactiveEffectEvent(
                runtime.NextCauseId(),
                ReactiveEffectTrigger.TurnStarted
            )
        ));
        foreach (var command in ordered)
        {
            runtime.Operations.AddLast(
                new ExecuteActionOperation(command)
            );
        }
        runtime.Operations.AddLast(new TriggerReactiveEffectsOperation(
            new ReactiveEffectEvent(
                runtime.NextCauseId(),
                ReactiveEffectTrigger.TurnEnded
            )
        ));
        runtime.Operations.AddLast(new FlushEndTurnReactiveEffectsOperation());
        runtime.Operations.AddLast(new ExpireStatusesOperation());
        runtime.Operations.AddLast(new FinishTurnOperation());
        runtime.Phase = BattleDomainPhase.ResolvingTurn;
    }

    public BattleAdvance AdvanceResolution()
    {
        if (runtime.Phase == BattleDomainPhase.AwaitingCuePlayback)
        {
            throw new InvalidOperationException(
                "Cue playback must be acknowledged before advancing."
            );
        }
        if (runtime.Phase == BattleDomainPhase.Completed)
        {
            return BattleAdvance.Complete(
                runtime.Result ?? throw new InvalidOperationException(
                    "Completed battle has no result."
                )
            );
        }
        if (runtime.Phase == BattleDomainPhase.SelectingCommands)
        {
            return BattleAdvance.RequireCommand(
                commands.RequestedPlayerId
                ?? throw new InvalidOperationException(
                    "All player commands are already selected."
                )
            );
        }
        if (runtime.Phase != BattleDomainPhase.ResolvingTurn)
        {
            throw new InvalidOperationException(
                "Battle is not running."
            );
        }

        while (runtime.Operations.First is not null)
        {
            var operation = runtime.Operations.First.Value;
            runtime.Operations.RemoveFirst();

            if (operation is VisualCueOperation cue)
            {
                runtime.AwaitingCueBatchId =
                    runtime.NextCueBatchId();
                runtime.Phase =
                    BattleDomainPhase.AwaitingCuePlayback;
                return BattleAdvance.RequireCuePlayback(
                    runtime.AwaitingCueBatchId,
                    cue.Cues
                );
            }

            executor.Execute(operation);
            if (runtime.Phase == BattleDomainPhase.Completed)
            {
                return BattleAdvance.Complete(runtime.Result!);
            }
            if (runtime.Phase == BattleDomainPhase.SelectingCommands)
            {
                return BattleAdvance.RequireCommand(
                    commands.RequestedPlayerId!.Value
                );
            }
        }

        throw new InvalidOperationException(
            "Resolution ended without a terminal operation."
        );
    }

    public void AcknowledgeCuePlayback(long cueBatchId)
    {
        if (
            runtime.Phase != BattleDomainPhase.AwaitingCuePlayback
            || cueBatchId != runtime.AwaitingCueBatchId
        )
        {
            throw new InvalidOperationException(
                "Cue playback acknowledgement does not match."
            );
        }

        runtime.AwaitingCueBatchId = 0;
        runtime.Phase = BattleDomainPhase.ResolvingTurn;
    }
}
