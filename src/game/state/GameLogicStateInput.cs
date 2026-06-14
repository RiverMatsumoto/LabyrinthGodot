namespace Labyrinth;

public partial record GameLogicState
{
    public static class Input
    {
        public readonly record struct EnterMainMenu;
        public readonly record struct EnterTown;
        public readonly record struct EnterLabyrinth;
        public readonly record struct EnterBattle(
            EncounterId EncounterId = default,
            int Seed = 1,
            GameMode ReturnMode = GameMode.Labyrinth
        );
        public readonly record struct SetMovementSettings(
            MapMovementSettings Settings
        );
    }
}
