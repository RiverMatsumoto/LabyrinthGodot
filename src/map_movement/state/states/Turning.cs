namespace Labyrinth;

using System;
using Chickensoft.LogicBlocks;

public partial record MapMovementLogicState
{
    public record Turning : MapMovementLogicState,
        IGet<Input.TurnFinished>,
        IGet<Input.Disable>
    {
        public Type On(in Input.TurnFinished input)
        {
            Output(new Output.TurnFinished());
            return To<MoveCooldown>();
        }

        public Type On(in Input.Disable input)
        {
            Data.DisableRequested = true;
            return ToSelf();
        }
    }
}
