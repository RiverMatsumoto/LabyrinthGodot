namespace Labyrinth;

using System.Collections.Generic;

public abstract partial record BattlePresenterLogicState
{
    public static class Input
    {
        public readonly record struct ShowCommandPrompt(
            BattleScreenView View,
            BattleCommandPrompt Prompt
        );

        public readonly record struct ScreenUpdated(BattleScreenView View);
        public readonly record struct MenuActionSelected(int Index);
        public readonly record struct AttackSelected;
        public readonly record struct SkillSelected;
        public readonly record struct SkillActionSelected(int Index);
        public readonly record struct TargetSelected(int Index);
        public readonly record struct TargetAnchorSelected(BattlerId AnchorId);
        public readonly record struct TargetMoved(int RowDelta, int SlotDelta);
        public readonly record struct Confirmed;
        public readonly record struct ItemSelected;
        public readonly record struct DefenceSelected;
        public readonly record struct MoveSelected;
        public readonly record struct BackRequested;
        public readonly record struct EscapeRequested;
        public readonly record struct CueBatchRequested(
            BattleScreenView View,
            IReadOnlyList<BattleCue> Cues
        );
        public readonly record struct CueVisualFinished;
        public readonly record struct Cancelled;
    }
}
