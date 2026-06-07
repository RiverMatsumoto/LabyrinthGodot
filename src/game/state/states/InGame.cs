namespace Labyrinth;

using System;
using Chickensoft.LogicBlocks;

public partial record GameState
{
    public record InGame : GameState, IGet<Input.EnterMainMenu>
    {
        public InGame()
        {
            this.OnEnter(() => Output(new Output.EnteredGame()));
        }

        public Type On(in Input.EnterMainMenu input) => To<MainMenu>();
    }
}
