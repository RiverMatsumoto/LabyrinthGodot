namespace Labyrinth;

using System;
using Chickensoft.LogicBlocks;

public abstract partial record BattlePresenterLogicState
{
    public abstract record ShowingCommandMenu
        : BattlePresenterLogicState,
            IGet<Input.ShowCommandPrompt>,
            IGet<Input.UndoRequested>,
            IGet<Input.EscapeRequested>,
            IGet<Input.ItemSelected>,
            IGet<Input.DefenceSelected>,
            IGet<Input.MoveSelected>
    {
        public Type On(in Input.ShowCommandPrompt input) =>
            ShowPrompt(input.View, input.Prompt);

        public Type On(in Input.UndoRequested input)
        {
            Output(new Output.UndoRequested());
            return ToSelf();
        }

        public Type On(in Input.EscapeRequested input)
        {
            Output(new Output.EscapeRequested());
            return ToSelf();
        }

        public Type On(in Input.ItemSelected input) => ToSelf();
        public Type On(in Input.DefenceSelected input) => ToSelf();
        public Type On(in Input.MoveSelected input) => ToSelf();
    }
}
