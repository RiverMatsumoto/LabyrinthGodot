namespace Labyrinth;

public partial record MenuHubLogicState
{
    public static class Output
    {
        public readonly record struct OpenedMenuHub;
        public readonly record struct OpenedSettings;
        public readonly record struct Closed;
    }
}
