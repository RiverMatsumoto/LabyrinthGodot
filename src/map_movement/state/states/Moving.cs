namespace Labyrinth;

using System;
using Chickensoft.LogicBlocks;

public partial record MapMovementLogicState
{
    public record Moving
        : MapMovementLogicState,
            IGet<Input.Arrived>,
            IGet<Input.MoveAccepted>,
            IGet<Input.MoveBlocked>
    {
        public Type On(in Input.Arrived input)
        {
            Output(new Output.MoveFinished());
            return To<Idle>();
        }

        public Type On(in Input.MoveAccepted input) => To<Moving>();

        public Type On(in Input.MoveBlocked input) => To<Moving>();
    }
}
