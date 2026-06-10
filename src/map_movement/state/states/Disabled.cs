namespace Labyrinth;

using System;
using Chickensoft.LogicBlocks;

public partial record MapMovementLogicState
{
    public record Disabled
        : MapMovementLogicState,
            IGet<Input.Enable>
    {
        public Type On(in Input.Enable input) => To<Idle>();
    }
}
