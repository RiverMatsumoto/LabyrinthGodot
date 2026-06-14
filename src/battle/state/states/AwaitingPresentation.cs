namespace Labyrinth;

using System;
using Chickensoft.LogicBlocks;

public partial record BattleLogicState
{
    public record AwaitingPresentation :
        BattleLogicState,
        IGet<Input.PresentationFinished>,
        IGet<Input.Flee>
    {
        public Type On(in Input.PresentationFinished input)
        {
            BattleRepo.AcknowledgePresentation(input.PresentationId);
            return To<ResolvingTurn>();
        }

        public Type On(in Input.Flee input) =>
            Complete(BattleRepo.Flee().Result!);
    }
}
