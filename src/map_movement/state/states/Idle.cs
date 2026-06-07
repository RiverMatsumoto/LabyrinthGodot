namespace Labyrinth;

using System;
using Chickensoft.LogicBlocks;
using Godot;

public partial record MapMovementLogicState
{
    public record Idle : MapMovementLogicState, IGet<Input.Moved>
    {
        public Idle()
        {
            this.OnEnter(() => GD.Print("Entered Idle"));
        }

        public Type On(in Input.Moved input)
        {
            GD.Print($"Moved: {input.Direction}");
            return To<Moving>();
        }
    }
}
