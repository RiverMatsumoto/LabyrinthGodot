namespace Labyrinth;

using System;
using Chickensoft.LogicBlocks;

public partial record MapMovementLogicState
{
    public record Idle
        : MapMovementLogicState,
            IGet<Input.MoveAccepted>,
            IGet<Input.MoveBlocked>
    {
        public Type On(in Input.MoveAccepted input)
        {
            Output(new Output.MoveStarted(input.Move));
            return To<Moving>();
        }

        public Type On(in Input.MoveBlocked input)
        {
            Output(new Output.MoveBlocked(input.Offset));
            return To<Idle>();
        }
    }
}
