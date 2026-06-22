namespace Labyrinth;

using System;
using System.Collections.Generic;
using Chickensoft.Introspection;
using Chickensoft.LogicBlocks;
using Chickensoft.LogicBlocks.Auto;

public interface IBattlePresenterLogic : ILogicBlock
{
    void ShowCommandPrompt(
        BattleScreenView view,
        BattleCommandPrompt prompt
    );

    void UpdateScreen(BattleScreenView view);
    void SelectAttack();
    void SelectSkill();
    void SelectSkillAction(int index);
    void SelectTarget(int index);
    void Confirm();
    void SelectItem();
    void SelectDefence();
    void SelectMove();
    void RequestUndo();
    void RequestEscape();
    void PlayCueBatch(BattleScreenView view, IReadOnlyList<BattleCue> cues);
    void CueVisualFinished();
    void Cancel();
}

[Meta]
public partial class BattlePresenterLogic
    : AutoBlock,
        IBattlePresenterLogic
{
    public sealed class Data
    {
        public BattleScreenView? Screen { get; set; }
        public BattleCommandPrompt? Prompt { get; set; }
        public BattleActionOption? SelectedAction { get; set; }
        public int SelectedSkillIndex { get; set; }
        public int SelectedTargetIndex { get; set; }
        public IReadOnlyList<BattleCue> CueBatch { get; set; } = [];
        public int CueIndex { get; set; }
    }

    public BattlePresenterLogic()
    {
        Preallocate<BattlePresenterLogicState>();
        Set(new Data());
    }

    public void ShowCommandPrompt(
        BattleScreenView view,
        BattleCommandPrompt prompt
    ) => Input(new BattlePresenterLogicState.Input.ShowCommandPrompt(
        view,
        prompt
    ));

    public void UpdateScreen(BattleScreenView view) =>
        Input(new BattlePresenterLogicState.Input.ScreenUpdated(view));

    public void SelectAttack() =>
        Input(new BattlePresenterLogicState.Input.AttackSelected());

    public void SelectSkill() =>
        Input(new BattlePresenterLogicState.Input.SkillSelected());

    public void SelectSkillAction(int index) =>
        Input(new BattlePresenterLogicState.Input.SkillActionSelected(index));

    public void SelectTarget(int index) =>
        Input(new BattlePresenterLogicState.Input.TargetSelected(index));

    public void Confirm() =>
        Input(new BattlePresenterLogicState.Input.Confirmed());

    public void SelectItem() =>
        Input(new BattlePresenterLogicState.Input.ItemSelected());

    public void SelectDefence() =>
        Input(new BattlePresenterLogicState.Input.DefenceSelected());

    public void SelectMove() =>
        Input(new BattlePresenterLogicState.Input.MoveSelected());

    public void RequestUndo() =>
        Input(new BattlePresenterLogicState.Input.UndoRequested());

    public void RequestEscape() =>
        Input(new BattlePresenterLogicState.Input.EscapeRequested());

    public void PlayCueBatch(
        BattleScreenView view,
        IReadOnlyList<BattleCue> cues
    ) => Input(new BattlePresenterLogicState.Input.CueBatchRequested(
        view,
        cues
    ));

    public void CueVisualFinished() =>
        Input(new BattlePresenterLogicState.Input.CueVisualFinished());

    public void Cancel() =>
        Input(new BattlePresenterLogicState.Input.Cancelled());
}
