namespace Labyrinth;

public abstract partial record BattlePresenterLogicState
{
    public static class Output
    {
        public readonly record struct RenderBattle(BattleScreenView View);
        public readonly record struct RenderCommandMenu(
            BattleCommandPrompt Prompt
        );
        public readonly record struct RenderSkillActions(
            BattleCommandPrompt Prompt,
            int SelectedIndex
        );
        public readonly record struct RenderTargets(
            BattleActionOption Option,
            int SelectedIndex
        );
        public readonly record struct BeginCueVisual(BattleCue Cue);
        public readonly record struct ClearCueVisual;
        public readonly record struct CommandSubmitted(BattleCommand Command);
        public readonly record struct UndoRequested;
        public readonly record struct EscapeRequested;
        public readonly record struct CueBatchFinished;
        public readonly record struct Hide;
    }
}
