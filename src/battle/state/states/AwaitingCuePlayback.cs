namespace Labyrinth;

using System;
using Chickensoft.LogicBlocks;

public partial record BattleLogicState
{
    public record AwaitingCuePlayback :
        BattleLogicState,
        IGet<Input.CuePlaybackFinished>,
        IGet<Input.Flee>
    {
        public Type On(in Input.CuePlaybackFinished input)
        {
            BattleRepo.AcknowledgeCuePlayback(input.CueBatchId);
            return To<ResolvingTurn>();
        }

        public Type On(in Input.Flee input) =>
            Complete(BattleRepo.Flee().Result!);
    }
}
