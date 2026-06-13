namespace Labyrinth;

using System;
using Chickensoft.LogicBlocks;

public partial record GameLogicState
{
    public record Town
        : GameLogicState,
            IGet<Input.EnterMainMenu>,
            IGet<Input.EnterLabyrinth>,
            IGet<Input.EnterBattle>
    {
        public Town()
        {
            this.OnEnter(() =>
            {
                Get<IGameRepo>().EnterTown();
                Output(new Output.EnteredTown());
            });
        }

        public Type On(in Input.EnterMainMenu input) => To<MainMenu>();

        public Type On(in Input.EnterLabyrinth input) => To<Labyrinth>();

        public Type On(in Input.EnterBattle input) => To<Battle>();
    }
}
