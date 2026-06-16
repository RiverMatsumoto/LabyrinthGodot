namespace Labyrinth;

using System;
using Chickensoft.LogicBlocks;

public abstract partial record BattlePresenterLogicState
{
    public record PlayingCueBatch
        : BattlePresenterLogicState,
            IGet<Input.CueVisualFinished>,
            IGet<Input.ShowCommandPrompt>
    {
        public Type On(in Input.CueVisualFinished input)
        {
            Data.CueIndex++;
            Output(new Output.ClearCueVisual());
            if (Data.CueIndex >= Data.CueBatch.Count)
            {
                Output(new Output.CueBatchFinished());
                return To<Hidden>();
            }

            Output(new Output.BeginCueVisual(Data.CueBatch[Data.CueIndex]));
            return ToSelf();
        }

        public Type On(in Input.ShowCommandPrompt input) =>
            ShowPrompt(input.View, input.Prompt);
    }
}
