namespace Labyrinth;

using System;
using Chickensoft.LogicBlocks;

public partial record GameLogicState
{
    public record Battle
        : GameLogicState,
            IGet<Input.EnterMainMenu>,
            IGet<Input.EnterTown>,
            IGet<Input.EnterLabyrinth>
    {
        public Battle()
        {
            this.OnEnter(() =>
            {
                Output(new Output.EnteredBattle());
            });
        }

        public Type On(in Input.EnterMainMenu input) => To<MainMenu>();

        public Type On(in Input.EnterTown input) => To<Town>();

        public Type On(in Input.EnterLabyrinth input) => To<Labyrinth>();
    }
}
