namespace Labyrinth;

using System;
using Chickensoft.LogicBlocks;

public partial record BattleLogicState
{
    public record ResolvingTurn :
        BattleLogicState,
        IGet<Input.AdvanceResolution>,
        IGet<Input.Flee>
    {
        public Type On(in Input.AdvanceResolution input)
        {
            var advance = BattleRepo.AdvanceResolution();
            switch (advance.Kind)
            {
                case BattleAdvanceKind.CuePlaybackRequired:
                    Output(new Output.CuePlaybackRequested(advance));
                    return To<AwaitingCuePlayback>();
                case BattleAdvanceKind.CommandRequired:
                    OutputCommandRequest();
                    return To<SelectingCommands>();
                case BattleAdvanceKind.Completed:
                    return Complete(advance.Result!);
                default:
                    throw new InvalidOperationException(
                        $"Unknown battle advance '{advance.Kind}'."
                    );
            }
        }

        public Type On(in Input.Flee input) =>
            Complete(BattleRepo.Flee().Result!);
    }
}
