namespace Labyrinth;

using System;
using Chickensoft.LogicBlocks;

public partial record GameLogicState
{
    public record Labyrinth
        : GameLogicState,
            IGet<Input.EnterMainMenu>,
            IGet<Input.EnterTown>,
            IGet<Input.EnterBattle>
    {
        public Labyrinth()
        {
            this.OnEnter(() => Output(new Output.EnteredLabyrinth()));
        }

        public Type On(in Input.EnterMainMenu input) => To<MainMenu>();

        public Type On(in Input.EnterTown input) => To<Town>();

        public Type On(in Input.EnterBattle input) => To<Battle>();
    }
}
