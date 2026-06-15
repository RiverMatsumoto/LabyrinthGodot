namespace Labyrinth;

public partial record GameLogicState
{
    public static class Output
    {
        public readonly record struct EnteredMainMenu;
        public readonly record struct EnteredTown;
        public readonly record struct EnteredLabyrinth;
        public readonly record struct EnteredBattle;
        public readonly record struct MovementSettingsChanged(
            MapMovementSettings Settings
        );
        public readonly record struct MaxFpsRequested(int MaxFps);
    }
}
