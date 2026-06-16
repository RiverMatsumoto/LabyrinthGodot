namespace Labyrinth;

using System;
using Chickensoft.Introspection;
using Chickensoft.LogicBlocks;

[Meta, StateDiagram]
public abstract partial record BattlePresenterLogicState
    : LogicBlockState,
        IGet<BattlePresenterLogicState.Input.ScreenUpdated>,
        IGet<BattlePresenterLogicState.Input.CueBatchRequested>,
        IGet<BattlePresenterLogicState.Input.Cancelled>
{
    protected BattlePresenterLogic.Data Data =>
        Get<BattlePresenterLogic.Data>();

    public Type On(in Input.ScreenUpdated input)
    {
        Data.Screen = input.View;
        Output(new Output.RenderBattle(input.View));
        return ToSelf();
    }

    public Type On(in Input.CueBatchRequested input) =>
        StartCueBatch(input.View, input.Cues);

    public Type On(in Input.Cancelled input) => Hide();

    protected Type ShowPrompt(
        BattleScreenView view,
        BattleCommandPrompt prompt
    )
    {
        Data.Screen = view;
        Data.Prompt = prompt;
        Data.SelectedAction = null;
        Data.SelectedSkillIndex = 0;
        Data.SelectedTargetIndex = 0;
        Output(new Output.RenderBattle(view));
        Output(new Output.RenderCommandMenu(prompt));
        return To<CommandMenu>();
    }

    protected Type StartCueBatch(
        BattleScreenView view,
        System.Collections.Generic.IReadOnlyList<BattleCue> cues
    )
    {
        Data.Screen = view;
        Data.CueBatch = cues;
        Data.CueIndex = 0;
        Output(new Output.RenderBattle(view));
        Output(new Output.ClearCueVisual());
        if (cues.Count == 0)
        {
            Output(new Output.CueBatchFinished());
            return To<Hidden>();
        }
        Output(new Output.BeginCueVisual(cues[0]));
        return To<PlayingCueBatch>();
    }

    protected Type Hide()
    {
        Data.Prompt = null;
        Data.SelectedAction = null;
        Data.SelectedSkillIndex = 0;
        Data.SelectedTargetIndex = 0;
        Data.CueBatch = [];
        Data.CueIndex = 0;
        Output(new Output.ClearCueVisual());
        Output(new Output.Hide());
        return To<Hidden>();
    }

    protected void OutputTargets(BattleActionOption option)
    {
        Data.SelectedAction = option;
        Data.SelectedTargetIndex = Math.Clamp(
            Data.SelectedTargetIndex,
            0,
            Math.Max(0, option.TargetOptions.Count - 1)
        );
        Output(new Output.RenderTargets(option, Data.SelectedTargetIndex));
    }

    protected void SubmitSelectedAction()
    {
        if (Data.Prompt is null || Data.SelectedAction is null)
        {
            return;
        }

        var target = Data.SelectedAction.TargetOptions.Count > 0
            ? Data.SelectedAction.TargetOptions[
                Math.Clamp(
                    Data.SelectedTargetIndex,
                    0,
                    Data.SelectedAction.TargetOptions.Count - 1
                )
            ].Id
            : (BattlerId?)null;
        Output(new Output.CommandSubmitted(new BattleCommand(
            Data.Prompt.ActorId,
            Data.SelectedAction.ActionId,
            target
        )));
    }
}
