namespace Labyrinth;

using System;
using Chickensoft.LogicBlocks;

public partial record MapMovementLogicState
{
    public record MoveCooldown : MapMovementLogicState,
        IGet<Input.CooldownFinished>,
        IGet<Input.Disable>
    {
        public MoveCooldown()
        {
            this.OnEnter(() => Get<IMapMovement>().StartCooldownTimer());
        }
        public Type On(in Input.CooldownFinished input)
        {
            if (!Data.DisableRequested)
            {
                return To<Idle>();
            }

            Data.DisableRequested = false;
            return To<Disabled>();
        }
        public Type On(in Input.Disable input)
        {
            Data.DisableRequested = true;
            return ToSelf();
        }
    }
}
