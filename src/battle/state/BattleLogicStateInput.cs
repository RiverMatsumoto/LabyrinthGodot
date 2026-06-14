namespace Labyrinth;

public partial record BattleLogicState
{
    public static class Input
    {
        public readonly record struct StartBattle(BattleSetup Setup);
        public readonly record struct SubmitIntent(BattleIntent Intent);
        public readonly record struct UndoIntent;
        public readonly record struct AdvanceResolution;
        public readonly record struct PresentationFinished(
            long PresentationId
        );
        public readonly record struct Flee;
    }
}
