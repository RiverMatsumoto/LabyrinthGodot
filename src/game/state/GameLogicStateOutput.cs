namespace Labyrinth;

public partial record GameState
{
    public static class Output
    {
        public readonly record struct EnteredMainMenu;
        public readonly record struct EnteredGame;
    }
}
