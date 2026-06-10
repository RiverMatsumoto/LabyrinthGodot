namespace Labyrinth;

using System;
using Chickensoft.LogicBlocks;

public partial record MapMovementLogicState
{
    public record Turning : MapMovementLogicState,
        IGet<Input.TurnFinished>
    {
        public Type On(in Input.TurnFinished input)
        {
            Output(new Output.TurnFinished());
            return To<MoveCooldown>();
        }
    }
}
