namespace Labyrinth;

using System;
using Chickensoft.LogicBlocks;

public partial record MenuHubLogicState
{
    public record Disabled : MenuHubLogicState,
        IGet<Input.OpenMenuHub>,
        IGet<Input.OpenSettings>,
        IGet<Input.HandleMenuInput>
    {
        public Disabled()
        {
            this.OnEnter(() =>
            {
                Output(new Output.Closed());
                Get<IGameRepo>().SetIsInMenu(false);
            });
        }

        public Type On(in Input.OpenMenuHub input)
        {
            if (CanOpen())
            {
                return To<MenuHub>();
            }
            return ToSelf();
        }

        public Type On(in Input.OpenSettings input)
        {
            if (CanOpen())
            {
                return To<Settings>();
            }
            return ToSelf();
        }

        public Type On(in Input.HandleMenuInput input)
        {
            if (CanOpen())
            {
                return To<MenuHub>();
            }
            return ToSelf();
        }

        private bool CanOpen() =>
            Get<IGameLogic>().State is GameLogicState.Town
                or GameLogicState.Labyrinth
                or GameLogicState.Battle;
    }
}
