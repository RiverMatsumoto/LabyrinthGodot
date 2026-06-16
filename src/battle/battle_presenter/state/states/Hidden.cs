namespace Labyrinth;

using System;
using Chickensoft.LogicBlocks;

public abstract partial record BattlePresenterLogicState
{
    public record Hidden
        : BattlePresenterLogicState,
            IGet<Input.ShowCommandPrompt>
    {
        public Type On(in Input.ShowCommandPrompt input) =>
            ShowPrompt(input.View, input.Prompt);
    }
}
