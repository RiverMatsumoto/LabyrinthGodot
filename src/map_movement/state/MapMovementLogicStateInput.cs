namespace Labyrinth;

using Godot;

public partial record MapMovementLogicState
{
    public static class Input
    {
        public readonly record struct Moved(Vector2I Direction);
    }
}
