namespace Labyrinth;

using System;
using Chickensoft.LogicBlocks;

public partial record MapLogicState
{
    public record Idle : MapLogicState,
        IGet<Input.PlayerEnteredLabyrinth>
    {
        public Type On(in Input.PlayerEnteredLabyrinth input)
        {
            return ToSelf();
        }
    }
}
