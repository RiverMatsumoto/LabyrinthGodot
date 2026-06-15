namespace Labyrinth;

using System;
using Chickensoft.Introspection;
using Chickensoft.LogicBlocks;

[Meta, StateDiagram]
public abstract partial record BattleLogicState : LogicBlockState
{
    protected IBattleRepo BattleRepo => Get<IBattleRepo>();
    protected IEnemyCommandPlanner EnemyCommandPlanner =>
        Get<IEnemyCommandPlanner>();
    protected IBattleSession BattleSession => Get<IBattleSession>();

    protected Type Start()
    {
        var setup = BattleSession.CreateSetup();
        BattleRepo.Start(setup);
        Output(new Output.BattleStarted(setup.EncounterId));
        OutputCommandRequest();
        return To<SelectingCommands>();
    }

    protected void OutputCommandRequest()
    {
        if (BattleRepo.RequestedPlayerId is { } id)
        {
            Output(new Output.CommandRequested(id));
        }
    }

    protected Type Complete(BattleResult result)
    {
        var returnMode = BattleSession.Complete(result);
        Output(new Output.BattleCompleted(result));
        Output(new Output.ReturnModeRequested(returnMode));
        return To<Completed>();
    }
}
