namespace Labyrinth;

public partial record GameState
{
    public static class Input
    {
        public readonly record struct EnterMainMenu;
        public readonly record struct EnterGame;
    }
}
