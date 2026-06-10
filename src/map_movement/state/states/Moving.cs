namespace Labyrinth;

using System;
using Chickensoft.LogicBlocks;

public partial record MapMovementLogicState
{
    public record Moving
        : MapMovementLogicState,
            IGet<Input.MoveFinished>
    {
        public Type On(in Input.MoveFinished input)
        {
            Output(new Output.MoveFinished());
            return To<MoveCooldown>();
        }
    }
}
