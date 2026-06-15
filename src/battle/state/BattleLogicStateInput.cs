namespace Labyrinth;

public partial record BattleLogicState
{
    public static class Input
    {
        public readonly record struct StartBattle(BattleSetup Setup);
        public readonly record struct SubmitCommand(BattleCommand Command);
        public readonly record struct UndoCommand;
        public readonly record struct AdvanceResolution;
        public readonly record struct CuePlaybackFinished(
            long CueBatchId
        );
        public readonly record struct Flee;
    }
}
