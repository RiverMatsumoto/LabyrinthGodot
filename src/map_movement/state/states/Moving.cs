namespace Labyrinth;

using System;
using Chickensoft.LogicBlocks;

public partial record MapMovementLogicState
{
    public record Moving
        : MapMovementLogicState,
            IGet<Input.MoveFinished>,
            IGet<Input.Disable>
    {
        public Moving()
        {

        }

        public Type On(in Input.MoveFinished input)
        {
            Output(new Output.MoveFinished());
            return To<MoveCooldown>();
        }

        public Type On(in Input.Disable input)
        {
            Data.DisableRequested = true;
            return ToSelf();
        }
    }
}
