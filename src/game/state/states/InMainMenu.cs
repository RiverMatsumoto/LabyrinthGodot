namespace Labyrinth;

using System;
using Chickensoft.LogicBlocks;

public partial record GameState
{
    public record MainMenu : GameState, IGet<Input.EnterGame>
    {
        public MainMenu()
        {
            this.OnEnter(() => Output(new Output.EnteredMainMenu()));
        }

        public Type On(in Input.EnterGame input) => To<InGame>();
    }
}
