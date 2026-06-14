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
            var step = BattleRepo.Advance();
            switch (step.Kind)
            {
                case BattleStepKind.Presentation:
                    Output(new Output.PresentationRequested(step));
                    return To<AwaitingPresentation>();
                case BattleStepKind.CommandSelection:
                    OutputCommandRequest();
                    return To<SelectingCommands>();
                case BattleStepKind.Completed:
                    return Complete(step.Result!);
                default:
                    throw new InvalidOperationException(
                        $"Unknown battle step '{step.Kind}'."
                    );
            }
        }

        public Type On(in Input.Flee input) =>
            Complete(BattleRepo.Flee().Result!);
    }
}
