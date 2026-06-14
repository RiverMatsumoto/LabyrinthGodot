namespace Labyrinth;

public partial record MenuHubLogicState
{
    public static class Input
    {
        public readonly record struct OpenMenuHub;
        public readonly record struct OpenSettings;
        public readonly record struct HandleMenuInput;
        public readonly record struct Back;
        public readonly record struct Close;
    }
}
