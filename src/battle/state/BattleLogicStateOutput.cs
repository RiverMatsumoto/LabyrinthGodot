namespace Labyrinth;

public partial record BattleLogicState
{
    public static class Output
    {
        public readonly record struct BattleStarted(EncounterId EncounterId);
        public readonly record struct CommandRequested(BattlerId ActorId);
        public readonly record struct CommandRejected(string Error);
        public readonly record struct CommandUndone(BattlerId ActorId);
        public readonly record struct CuePlaybackRequested(
            BattleAdvance Advance
        );
        public readonly record struct BattleCompleted(BattleResult Result);
    }
}
