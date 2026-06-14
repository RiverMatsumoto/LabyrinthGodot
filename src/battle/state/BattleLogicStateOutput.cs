namespace Labyrinth;

public partial record BattleLogicState
{
    public static class Output
    {
        public readonly record struct BattleStarted(EncounterId EncounterId);
        public readonly record struct CommandRequested(BattlerId BattlerId);
        public readonly record struct CommandRejected(string Error);
        public readonly record struct CommandUndone(BattlerId BattlerId);
        public readonly record struct PresentationRequested(BattleStep Step);
        public readonly record struct BattleCompleted(BattleResult Result);
    }
}
