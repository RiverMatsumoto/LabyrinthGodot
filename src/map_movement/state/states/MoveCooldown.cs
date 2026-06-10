namespace Labyrinth;

using System;
using Chickensoft.LogicBlocks;

public partial record MapMovementLogicState
{
    public record MoveCooldown : MapMovementLogicState,
        IGet<Input.CooldownFinished>
    {
        public MoveCooldown()
        {
            this.OnEnter(() => Get<IMapMovement>().StartCooldownTimer());
        }
        public Type On(in Input.CooldownFinished input) => To<Idle>();
    }
}
