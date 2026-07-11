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
    void SelectMenuAction(int index);
    void SelectAttack();
    void SelectSkill();
    void SelectSkillAction(int index);
    void SelectTarget(int index);
    void SelectTargetAnchor(BattlerId anchorId);
    void MoveTarget(int rowDelta, int slotDelta);
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

internal readonly record struct BattlePresenterCommandMemory(
    ActionId ActionId,
    BattlerId? TargetAnchorId
);

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
        public int SelectedMenuIndex { get; set; }
        public int SelectedSkillIndex { get; set; }
        public int SelectedTargetIndex { get; set; }
        internal Dictionary<
            BattlerId,
            BattlePresenterCommandMemory
        > CommandMemories { get; } = [];
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

    public void SelectMenuAction(int index) =>
        Input(new BattlePresenterLogicState.Input.MenuActionSelected(index));

    public void SelectAttack() =>
        Input(new BattlePresenterLogicState.Input.AttackSelected());

    public void SelectSkill() =>
        Input(new BattlePresenterLogicState.Input.SkillSelected());

    public void SelectSkillAction(int index) =>
        Input(new BattlePresenterLogicState.Input.SkillActionSelected(index));

    public void SelectTarget(int index) =>
        Input(new BattlePresenterLogicState.Input.TargetSelected(index));

    public void SelectTargetAnchor(BattlerId anchorId) =>
        Input(new BattlePresenterLogicState.Input.TargetAnchorSelected(
            anchorId
        ));

    public void MoveTarget(int rowDelta, int slotDelta) =>
        Input(new BattlePresenterLogicState.Input.TargetMoved(
            rowDelta,
            slotDelta
        ));

    public void Confirm() =>
        Input(new BattlePresenterLogicState.Input.Confirmed());

    public void SelectItem() =>
        Input(new BattlePresenterLogicState.Input.ItemSelected());

    public void SelectDefence() =>
        Input(new BattlePresenterLogicState.Input.DefenceSelected());

    public void SelectMove() =>
        Input(new BattlePresenterLogicState.Input.MoveSelected());

    public void RequestUndo() =>
        Input(new BattlePresenterLogicState.Input.BackRequested());

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
