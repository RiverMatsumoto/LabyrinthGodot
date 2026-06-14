namespace Labyrinth;

using System;
using Chickensoft.LogicBlocks;

public partial record MenuHubLogicState
{
    public record MenuHub : MenuHubLogicState,
        IGet<Input.OpenSettings>,
        IGet<Input.Close>,
        IGet<Input.HandleMenuInput>
    {
        public MenuHub()
        {
            this.OnEnter(() =>
            {
                Output(new Output.OpenedMenuHub());
                Get<IGameRepo>().SetIsInMenu(true);
            });
        }

        public Type On(in Input.OpenSettings input) => To<Settings>();

        public Type On(in Input.Close input) => To<Disabled>();

        public Type On(in Input.HandleMenuInput input) => To<Disabled>();
    }
}
