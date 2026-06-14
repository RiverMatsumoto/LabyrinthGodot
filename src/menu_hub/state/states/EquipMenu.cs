namespace Labyrinth;

using System;
using Chickensoft.LogicBlocks;

public partial record MenuHubLogicState
{
    public record EquipMenu : MenuHubLogicState,
        IGet<Input.Back>,
        IGet<Input.Close>
    {
        public Type On(in Input.Back input) => To<MenuHub>();

        public Type On(in Input.Close input) => To<Disabled>();
    }
}
