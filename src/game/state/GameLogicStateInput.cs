namespace Labyrinth;

public partial record GameLogicState
{
    public static class Input
    {
        public readonly record struct EnterMainMenu;
        public readonly record struct EnterTown;
        public readonly record struct EnterLabyrinth;
        public readonly record struct EnterBattle;
        public readonly record struct SetMovementSettings(
            MapMovementSettings Settings
        );
    }
}
